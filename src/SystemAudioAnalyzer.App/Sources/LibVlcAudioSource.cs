namespace SystemAudioAnalyzer.App.Sources;

public sealed class LibVlcAudioSource : IAudioSource
{
    private const int IcecastNetworkCachingMilliseconds = 100;
    private const int HlsNetworkCachingMilliseconds = 500;
    private readonly Uri _streamUri;
    private readonly ILibVlcPlayer _player;
    private bool _subscribed;
    private bool _disposed;
    private AudioSourceState _state = AudioSourceState.Stopped;

    public LibVlcAudioSource(Uri streamUri, ILibVlcPlayer player)
    {
        _streamUri = streamUri ?? throw new ArgumentNullException(nameof(streamUri));
        _player = player ?? throw new ArgumentNullException(nameof(player));
    }

    public event EventHandler<AudioSamplesAvailableEventArgs>? SamplesAvailable;

    public event EventHandler<AudioSourceStateChangedEventArgs>? StateChanged;

    public event EventHandler<CaptureFaultedEventArgs>? Faulted;

    public AudioSourceState State => _state;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_state is AudioSourceState.Connecting or AudioSourceState.Buffering or AudioSourceState.Running)
        {
            return;
        }

        Subscribe();
        SetState(AudioSourceState.Connecting);
        try
        {
            await _player.PlayAsync(_streamUri, GetNetworkCachingMilliseconds(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || _state == AudioSourceState.Stopped)
        {
            return;
        }

        Unsubscribe();
        await _player.StopAsync(cancellationToken).ConfigureAwait(false);
        SetState(AudioSourceState.Stopped);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        Unsubscribe();
        _disposed = true;
        await _player.DisposeAsync().ConfigureAwait(false);
    }

    private int GetNetworkCachingMilliseconds() =>
        _streamUri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
            ? HlsNetworkCachingMilliseconds
            : IcecastNetworkCachingMilliseconds;

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _player.PcmReceived += OnPcmReceived;
        _player.BufferingChanged += OnBufferingChanged;
        _player.Failed += OnFailed;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        _player.PcmReceived -= OnPcmReceived;
        _player.BufferingChanged -= OnBufferingChanged;
        _player.Failed -= OnFailed;
        _subscribed = false;
    }

    private void OnPcmReceived(object? sender, LibVlcPcmEventArgs eventArgs) =>
        SamplesAvailable?.Invoke(this, new AudioSamplesAvailableEventArgs(eventArgs.Samples, eventArgs.Format));

    private void OnBufferingChanged(object? sender, float progress) =>
        SetState(progress < 100 ? AudioSourceState.Buffering : AudioSourceState.Running);

    private void OnFailed(object? sender, Exception exception) => HandleFailure(exception);

    private void HandleFailure(Exception exception)
    {
        SetState(AudioSourceState.Faulted);
        Faulted?.Invoke(this, new CaptureFaultedEventArgs(exception));
    }

    private void SetState(AudioSourceState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        StateChanged?.Invoke(this, new AudioSourceStateChangedEventArgs(state));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LibVlcAudioSource));
        }
    }
}
