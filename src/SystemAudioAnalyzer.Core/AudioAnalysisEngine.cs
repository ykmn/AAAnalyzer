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
    private readonly SpectrumAnalyzer _spectrumAnalyzer = new();
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

    public void ResetTruePeak(int channel)
    {
        lock (_measurementGate)
        {
            _truePeakMeter.Reset(channel);
        }
    }

    public void ResetLoudness()
    {
        lock (_measurementGate)
        {
            _loudnessMeter.Reset();
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
        await foreach (var buffer in samples.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            var timestamp = DateTimeOffset.UtcNow;
            if (timestamp - _lastFrameTimestamp < MinimumFrameInterval)
            {
                continue;
            }

            _lastFrameTimestamp = timestamp;
            var levels = _levelMeter.Process(buffer.Samples, buffer.Format.Channels);
            _spectrumAnalyzer.TryProcess(buffer.Samples, buffer.Format, out var spectrum);
            AdvancedMeasurementFrame advancedMeasurements;
            lock (_measurementGate)
            {
                advancedMeasurements = new AdvancedMeasurementFrame(
                    _truePeakMeter.Process(buffer.Samples, buffer.Format.Channels),
                    _loudnessMeter.Process(buffer.Samples, buffer.Format));
            }
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

            frames.TryWrite(new AnalysisFrame(timestamp, buffer.Format, levels, spectrum, droppedBufferCount, advancedMeasurements));
        }
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
