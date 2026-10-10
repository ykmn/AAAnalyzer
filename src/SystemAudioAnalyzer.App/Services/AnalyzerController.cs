using SystemAudioAnalyzer.App.Sources;
using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App.Services;

public sealed class AnalyzerController : IAnalyzerController, IAsyncDisposable
{
    private readonly AudioAnalysisEngine _engine;
    private readonly Func<SourceSelection, IAudioSource> _sourceFactory;
    private readonly SemaphoreSlim _transitionLock = new(1, 1);
    private CancellationTokenSource? _frameCancellation;
    private IAudioSource? _source;
    private Task? _frameTask;
    private readonly Action<string>? _diagnostic;
    private bool _disposed;

    public AnalyzerController(Action<string>? diagnostic = null)
        : this(
            new AudioAnalysisEngine(new NaudioAudioOutputDeviceProvider(), new NaudioAudioCaptureFactory()),
            selection => CreateSource(selection, diagnostic),
            diagnostic)
    {
    }

    public AnalyzerController(AudioAnalysisEngine engine, Func<SourceSelection, IAudioSource> sourceFactory, Action<string>? diagnostic = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
        _diagnostic = diagnostic;
        if (diagnostic is not null)
        {
            _engine.Faulted += (_, eventArgs) => diagnostic($"Engine faulted: {eventArgs.Exception}");
            var lastDropLog = DateTime.MinValue;
            _engine.DiagnosticPublished += (_, entry) =>
            {
                // Dropped buffers are reported on every frame while overloaded: log them at most once a second.
                if (entry.Kind == EngineDiagnosticKind.BufferDropped)
                {
                    if (DateTime.UtcNow - lastDropLog < TimeSpan.FromSeconds(1)) return;
                    lastDropLog = DateTime.UtcNow;
                }

                diagnostic($"Engine {entry.Kind}: {entry.Message} device={entry.Device?.Name} format={entry.Format}");
            };
        }
    }

    public event EventHandler<AnalysisFrame>? FrameAvailable;

    public event EventHandler<AudioSourceStateChangedEventArgs>? SourceStateChanged;

    public async Task StartAsync(SourceSelection selection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ThrowIfDisposed();
        await _transitionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopInternalAsync().ConfigureAwait(false);
            _diagnostic?.Invoke($"Start requested: {selection.Mode} {(selection.StreamUri?.ToString() ?? selection.Device?.Name)} id={selection.Device?.Id}");
            var source = _sourceFactory(selection);
            source.StateChanged += OnSourceStateChanged;
            _source = source;
            await _engine.StartAsync(source, cancellationToken).ConfigureAwait(false);
            _frameCancellation = new CancellationTokenSource();
            _frameTask = ReadFramesAsync(_frameCancellation.Token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _diagnostic?.Invoke($"Start failed: {exception}");
            throw;
        }
        finally
        {
            _transitionLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _transitionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _transitionLock.Release();
        }
    }

    public void ResetTruePeak(int channel) => _engine.ResetTruePeak(channel);

    public void ResetTruePeakMaximum(int channel) => _engine.ResetTruePeakMaximum(channel);

    public void ResetTruePeakOverload(int channel) => _engine.ResetTruePeakOverload(channel);

    public void ResetLoudness() => _engine.ResetLoudness();

    public void SetAnalysisConfiguration(AnalysisConfiguration configuration) => _engine.SetAnalysisConfiguration(configuration);

    public void SetLoudnessIntegratedWindow(int seconds) => _engine.SetLoudnessIntegratedWindow(seconds);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        _transitionLock.Dispose();
        await _engine.DisposeAsync().ConfigureAwait(false);
    }

    private async Task StopInternalAsync()
    {
        _frameCancellation?.Cancel();
        if (_frameTask is not null)
        {
            try
            {
                await _frameTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _frameCancellation?.Dispose();
        _frameCancellation = null;
        _frameTask = null;
        if (_source is not null)
        {
            _source.StateChanged -= OnSourceStateChanged;
            _source = null;
        }

        await _engine.StopAsync().ConfigureAwait(false);
    }

    private async Task ReadFramesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in _engine.ReadFrames(cancellationToken).ConfigureAwait(false))
            {
                FrameAvailable?.Invoke(this, frame);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnSourceStateChanged(object? sender, AudioSourceStateChangedEventArgs eventArgs) =>
        SourceStateChanged?.Invoke(this, eventArgs);

    private static IAudioSource CreateSource(SourceSelection selection, Action<string>? diagnostic) => selection.Mode switch
    {
        SourceMode.Device when selection.Device is not null =>
            new AudioCaptureSource(new NaudioAudioCaptureFactory().Create(selection.Device, selection.FaderMode)),
        SourceMode.Stream when selection.StreamUri is not null =>
            new LibVlcAudioSource(selection.StreamUri, new LibVlcPlayer(selection.PlaybackDeviceId, selection.PlaybackBufferMs, diagnostic)),
        _ => throw new InvalidOperationException("The selected audio source is incomplete."),
    };

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AnalyzerController));
        }
    }
}
