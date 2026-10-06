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
    private readonly EngineStateMachine _stateMachine = new();
    private Channel<AudioSamplesAvailableEventArgs>? _samples;
    private Channel<AnalysisFrame>? _frames;
    private CancellationTokenSource? _cancellation;
    private IAudioCapture? _capture;
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

    public Task StartAsync(OutputDeviceInfo? device = null)
    {
        EngineDiagnostic? diagnostic = null;
        lock (_sync)
        {
            _stateMachine.BeginStarting();
            try
            {
                var selectedDevice = device ?? _deviceProvider.GetDefaultDevice()
                    ?? throw new InvalidOperationException("No active audio output device is available.");
                _samples = Channel.CreateBounded<AudioSamplesAvailableEventArgs>(new BoundedChannelOptions(8)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = false,
                });
                _frames = Channel.CreateBounded<AnalysisFrame>(new BoundedChannelOptions(4)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = false,
                    SingleWriter = true,
                });
                _cancellation = new CancellationTokenSource();
                _lastFrameTimestamp = DateTimeOffset.MinValue;
                Interlocked.Exchange(ref _droppedBufferCount, 0);
                _capture = _captureFactory.Create(selectedDevice);
                _capture.SamplesAvailable += OnSamplesAvailable;
                _capture.Faulted += OnCaptureFaulted;
                _processingTask = ProcessSamplesAsync(_samples.Reader, _frames.Writer, _cancellation.Token);
                _capture.Start();
                _stateMachine.MarkRunning();
                _currentDevice = selectedDevice;
                diagnostic = CreateDiagnostic(EngineDiagnosticKind.Started, "Audio capture started.", selectedDevice);
            }
            catch
            {
                _stateMachine.MarkFaulted();
                throw;
            }
        }

        PublishDiagnostic(diagnostic!);

        return Task.CompletedTask;
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
        IAudioCapture? capture;
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
            capture = _capture;
            processingTask = _processingTask;
            cancellation = _cancellation;
            samples = _samples;
            frames = _frames;
            stoppedDevice = _currentDevice;
            _capture = null;
            _processingTask = null;
            _cancellation = null;
            _currentDevice = null;
        }

        if (capture is not null)
        {
            capture.SamplesAvailable -= OnSamplesAvailable;
            capture.Faulted -= OnCaptureFaulted;
            capture.Stop();
            capture.Dispose();
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

            frames.TryWrite(new AnalysisFrame(timestamp, buffer.Format, levels, spectrum, droppedBufferCount));
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
