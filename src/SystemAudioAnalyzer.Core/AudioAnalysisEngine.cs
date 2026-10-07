using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace SystemAudioAnalyzer.Core;

public sealed class AudioAnalysisEngine : IAsyncDisposable
{
    private static readonly TimeSpan MinimumFrameInterval = TimeSpan.FromSeconds(1d / 30d);
    private readonly object _sync = new();
    private readonly object _queueGate = new();
    private readonly IAudioOutputDeviceProvider _deviceProvider;
    private readonly IAudioCaptureFactory _captureFactory;
    private readonly LevelMeter _levelMeter = new();
    private AnalysisConfiguration _requestedConfiguration = new(4096, SpectrumWindow.Hann);
    private readonly TruePeakMeter _truePeakMeter = new();
    private readonly LoudnessMeter _loudnessMeter = new();
    private readonly object _measurementGate = new();
    private readonly EngineStateMachine _stateMachine = new();
    private Channel<AudioSamplesAvailableEventArgs>? _samples;
    private Channel<AnalysisFrame>? _frames;
    private CancellationTokenSource? _cancellation;
    private IAudioSource? _source;
    private Task? _processingTask;
    private DateTimeOffset _lastFrameTimestamp;
    private OutputDeviceInfo? _currentDevice;
    private long _droppedBufferCount;

    public AudioAnalysisEngine(IAudioOutputDeviceProvider deviceProvider, IAudioCaptureFactory captureFactory)
    {
        _deviceProvider = deviceProvider ?? throw new ArgumentNullException(nameof(deviceProvider));
        _captureFactory = captureFactory ?? throw new ArgumentNullException(nameof(captureFactory));
    }

    public EngineState State
    {
        get
        {
            lock (_sync)
            {
                return _stateMachine.State;
            }
        }
    }

    public OutputDeviceInfo? CurrentDevice
    {
        get
        {
            lock (_sync)
            {
                return _currentDevice;
            }
        }
    }

    public event EventHandler<EngineDiagnostic>? DiagnosticPublished;

    public event EventHandler<EngineFaultedEventArgs>? Faulted;

    public void SetAnalysisConfiguration(AnalysisConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Volatile.Write(ref _requestedConfiguration, configuration);
    }

    public void ResetTruePeak(int channel)
    {
        lock (_measurementGate)
        {
            _truePeakMeter.Reset(channel);
        }
    }

    public void ResetTruePeakMaximum(int channel)
    {
        lock (_measurementGate) { _truePeakMeter.ResetMaximum(channel); }
    }

    public void ResetTruePeakOverload(int channel)
    {
        lock (_measurementGate) { _truePeakMeter.ResetOverload(channel); }
    }

    public void ResetLoudness()
    {
        lock (_measurementGate)
        {
            _loudnessMeter.Reset();
        }
    }

    public void SetLoudnessIntegratedWindow(int seconds)
    {
        lock (_measurementGate)
        {
            _loudnessMeter.SetIntegratedWindowSeconds(seconds);
        }
    }

    public Task StartAsync(OutputDeviceInfo? device = null)
    {
        OutputDeviceInfo selectedDevice;
        lock (_sync)
        {
            selectedDevice = device ?? _deviceProvider.GetDefaultDevice()
                ?? throw new InvalidOperationException("No active audio output device is available.");
        }

        return StartAsync(new AudioCaptureSource(_captureFactory.Create(selectedDevice)), selectedDevice);
    }

    public Task StartAsync(IAudioSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return StartAsync(source, device: null, cancellationToken);
    }

    private async Task StartAsync(IAudioSource source, OutputDeviceInfo? device, CancellationToken cancellationToken = default)
    {
        Channel<AudioSamplesAvailableEventArgs> samples;
        Channel<AnalysisFrame> frames;
        CancellationTokenSource cancellation;
        lock (_sync)
        {
            _stateMachine.BeginStarting();
            samples = Channel.CreateBounded<AudioSamplesAvailableEventArgs>(new BoundedChannelOptions(8)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });
            frames = Channel.CreateBounded<AnalysisFrame>(new BoundedChannelOptions(4)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = true,
            });
            cancellation = new CancellationTokenSource();
            _samples = samples;
            _frames = frames;
            _cancellation = cancellation;
            _source = source;
            _currentDevice = device;
            _lastFrameTimestamp = DateTimeOffset.MinValue;
            Interlocked.Exchange(ref _droppedBufferCount, 0);
            source.SamplesAvailable += OnSamplesAvailable;
            source.Faulted += OnCaptureFaulted;
            _processingTask = ProcessSamplesAsync(samples.Reader, frames.Writer, cancellation.Token);
        }

        try
        {
            await source.StartAsync(cancellationToken).ConfigureAwait(false);
            lock (_sync)
            {
                _stateMachine.MarkRunning();
            }
        }
        catch
        {
            lock (_sync)
            {
                _stateMachine.MarkFaulted();
            }

            throw;
        }

        PublishDiagnostic(CreateDiagnostic(EngineDiagnosticKind.Started, "Audio capture started.", device));
    }

    public async Task SwitchDeviceAsync(OutputDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var previousDevice = CurrentDevice;
        await StopAsync().ConfigureAwait(false);
        await StartAsync(device).ConfigureAwait(false);
        PublishDiagnostic(CreateDiagnostic(
            EngineDiagnosticKind.DeviceChanged,
            previousDevice is null ? "Audio output device selected." : "Audio output device changed.",
            device));
    }

    public async Task StopAsync()
    {
        IAudioSource? source;
        Task? processingTask;
        CancellationTokenSource? cancellation;
        Channel<AudioSamplesAvailableEventArgs>? samples;
        Channel<AnalysisFrame>? frames;
        OutputDeviceInfo? stoppedDevice;

        lock (_sync)
        {
            if (_stateMachine.State == EngineState.Stopped)
            {
                return;
            }

            _stateMachine.BeginStopping();
            source = _source;
            processingTask = _processingTask;
            cancellation = _cancellation;
            samples = _samples;
            frames = _frames;
            stoppedDevice = _currentDevice;
            _source = null;
            _processingTask = null;
            _cancellation = null;
            _currentDevice = null;
        }

        if (source is not null)
        {
            source.SamplesAvailable -= OnSamplesAvailable;
            source.Faulted -= OnCaptureFaulted;
            await source.StopAsync().ConfigureAwait(false);
            await source.DisposeAsync().ConfigureAwait(false);
        }

        samples?.Writer.TryComplete();
        cancellation?.Cancel();
        if (processingTask is not null)
        {
            try
            {
                await processingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        frames?.Writer.TryComplete();
        cancellation?.Dispose();
        lock (_sync)
        {
            _stateMachine.MarkStopped();
        }

        PublishDiagnostic(CreateDiagnostic(EngineDiagnosticKind.Stopped, "Audio capture stopped.", stoppedDevice));
    }

    public async IAsyncEnumerable<AnalysisFrame> ReadFrames([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChannelReader<AnalysisFrame>? reader;
        lock (_sync)
        {
            reader = _frames?.Reader;
        }

        if (reader is null)
        {
            throw new InvalidOperationException("Start the engine before reading analysis frames.");
        }

        await foreach (var frame in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return frame;
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private void OnSamplesAvailable(object? sender, AudioSamplesAvailableEventArgs eventArgs)
    {
        lock (_queueGate)
        {
            var samples = _samples;
            if (samples is null || samples.Writer.TryWrite(eventArgs))
            {
                return;
            }

            if (samples.Reader.TryRead(out _))
            {
                Interlocked.Increment(ref _droppedBufferCount);
                if (samples.Writer.TryWrite(eventArgs))
                {
                    return;
                }
            }

            Interlocked.Increment(ref _droppedBufferCount);
        }
    }

    private void OnCaptureFaulted(object? sender, CaptureFaultedEventArgs eventArgs)
    {
        OutputDeviceInfo? device;
        lock (_sync)
        {
            _stateMachine.MarkFaulted();
            device = _currentDevice;
        }

        Faulted?.Invoke(this, new EngineFaultedEventArgs(eventArgs.Exception));
        PublishDiagnostic(CreateDiagnostic(EngineDiagnosticKind.Faulted, "Audio capture failed.", device, exception: eventArgs.Exception));
    }

    private async Task ProcessSamplesAsync(
        ChannelReader<AudioSamplesAvailableEventArgs> samples,
        ChannelWriter<AnalysisFrame> frames,
        CancellationToken cancellationToken)
    {
        AnalysisConfiguration? activeConfiguration = null;
        SpectrumAnalyzer? spectrumAnalyzer = null;

        // Every buffer feeds the meters and the FFT; only the publishing of frames is throttled. What happened
        // between two published frames (peaks, newest spectrum) is carried over so nothing is lost.
        float[] pendingPeaks = [];
        IReadOnlyList<ChannelLevel>? pendingLevels = null;
        Spectrum? pendingSpectrum = null;
        StereoSpectrum? pendingStereo = null;
        await foreach (var buffer in samples.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            var requestedConfiguration = Volatile.Read(ref _requestedConfiguration);
            if (requestedConfiguration != activeConfiguration)
            {
                spectrumAnalyzer = new SpectrumAnalyzer(requestedConfiguration.FftSize, requestedConfiguration.Window);
                activeConfiguration = requestedConfiguration;
                pendingSpectrum = null;
                pendingStereo = null;
            }

            var timestamp = DateTimeOffset.UtcNow;
            var levels = _levelMeter.Process(buffer.Samples, buffer.Format.Channels);
            pendingLevels = MergeLevels(pendingLevels, levels);
            if (buffer.Format.Channels >= 2)
            {
                if (spectrumAnalyzer!.TryProcessStereo(buffer.Samples, buffer.Format, out var stereo) && stereo is not null)
                {
                    pendingStereo = stereo;
                    pendingSpectrum = stereo.Mono;
                }
            }
            else if (spectrumAnalyzer!.TryProcess(buffer.Samples, buffer.Format, out var mono) && mono is not null)
            {
                pendingSpectrum = mono;
            }

            StereoTruePeakMeasurement truePeak;
            LoudnessMeasurement loudness;
            lock (_measurementGate)
            {
                truePeak = _truePeakMeter.Process(buffer.Samples, buffer.Format.Channels);
                loudness = _loudnessMeter.Process(buffer.Samples, buffer.Format);
            }

            pendingPeaks = MergePeaks(pendingPeaks, truePeak.Current);
            if (timestamp - _lastFrameTimestamp < MinimumFrameInterval)
            {
                continue;
            }

            _lastFrameTimestamp = timestamp;
            var advancedMeasurements = new AdvancedMeasurementFrame(
                new StereoTruePeakMeasurement(pendingPeaks, truePeak.Maximum, truePeak.Overload),
                loudness,
                pendingStereo,
                buffer.Format.Channels >= 2 ? PhaseScopeFrame.FromInterleaved(buffer.Samples, buffer.Format.Channels) : null);
            var droppedBufferCount = Interlocked.Exchange(ref _droppedBufferCount, 0);
            if (droppedBufferCount > 0)
            {
                PublishDiagnostic(CreateDiagnostic(
                    EngineDiagnosticKind.BufferDropped,
                    "Audio buffers were dropped to keep analysis latency low.",
                    CurrentDevice,
                    buffer.Format,
                    droppedBufferCount));
            }

            frames.TryWrite(new AnalysisFrame(timestamp, buffer.Format, pendingLevels!, pendingSpectrum, droppedBufferCount, advancedMeasurements));
            pendingPeaks = [];
            pendingLevels = null;
            pendingSpectrum = null;
            pendingStereo = null;
        }
    }

    private static float[] MergePeaks(float[] pending, IReadOnlyList<float> current)
    {
        var merged = new float[current.Count];
        for (var channel = 0; channel < merged.Length; channel++)
        {
            merged[channel] = Math.Max(channel < pending.Length ? pending[channel] : 0f, current[channel]);
        }

        return merged;
    }

    private static IReadOnlyList<ChannelLevel> MergeLevels(IReadOnlyList<ChannelLevel>? pending, IReadOnlyList<ChannelLevel> latest)
    {
        if (pending is null || pending.Count != latest.Count)
        {
            return latest;
        }

        var merged = new ChannelLevel[latest.Count];
        for (var channel = 0; channel < merged.Length; channel++)
        {
            merged[channel] = new ChannelLevel(Math.Max(pending[channel].Peak, latest[channel].Peak), latest[channel].Rms);
        }

        return merged;
    }

    private static EngineDiagnostic CreateDiagnostic(
        EngineDiagnosticKind kind,
        string message,
        OutputDeviceInfo? device,
        AudioFormat? format = null,
        long droppedBufferCount = 0,
        Exception? exception = null) =>
        new(DateTimeOffset.UtcNow, kind, message, device, format, droppedBufferCount, exception);

    private void PublishDiagnostic(EngineDiagnostic diagnostic) => DiagnosticPublished?.Invoke(this, diagnostic);
}
