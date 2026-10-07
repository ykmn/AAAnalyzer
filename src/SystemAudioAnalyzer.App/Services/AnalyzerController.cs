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
    private bool _disposed;

    public AnalyzerController()
        : this(
            new AudioAnalysisEngine(new NaudioAudioOutputDeviceProvider(), new NaudioAudioCaptureFactory()),
            CreateSource)
    {
    }

    public AnalyzerController(AudioAnalysisEngine engine, Func<SourceSelection, IAudioSource> sourceFactory)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
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
            var source = _sourceFactory(selection);
            source.StateChanged += OnSourceStateChanged;
            _source = source;
            await _engine.StartAsync(source, cancellationToken).ConfigureAwait(false);
            _frameCancellation = new CancellationTokenSource();
            _frameTask = ReadFramesAsync(_frameCancellation.Token);
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

    public void ResetLoudness() => _engine.ResetLoudness();

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

    private static IAudioSource CreateSource(SourceSelection selection) => selection.Mode switch
    {
        SourceMode.Device when selection.Device is not null =>
            new AudioCaptureSource(new NaudioAudioCaptureFactory().Create(selection.Device)),
        SourceMode.Stream when selection.StreamUri is not null =>
            new LibVlcAudioSource(selection.StreamUri, new LibVlcPlayer()),
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
