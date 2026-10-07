using LibVLCSharp.Shared;

namespace SystemAudioAnalyzer.App.Sources;

public sealed class LibVlcPlayer : ILibVlcPlayer
{
    private const uint SampleRate = 48_000;
    private const uint Channels = 2;
    private LibVLC? _libVlc;
    private Media? _media;
    private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer;
    private bool _disposed;

    public event EventHandler<LibVlcPcmEventArgs>? PcmReceived;

    public event EventHandler<float>? BufferingChanged;

    public event EventHandler<Exception>? Failed;

    public Task PlayAsync(Uri streamUri, int networkCachingMilliseconds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(streamUri);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        StopAndDisposePlayer();

        LibVLCSharp.Shared.Core.Initialize();
        _libVlc = new LibVLC($"--network-caching={networkCachingMilliseconds}");
        _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVlc);
        _mediaPlayer.Buffering += OnBuffering;
        _mediaPlayer.EncounteredError += OnEncounteredError;
        _mediaPlayer.SetAudioFormat("S16N", SampleRate, Channels);
        _mediaPlayer.SetAudioCallbacks(OnAudioPlay, null, null, null, null);
        _media = new Media(_libVlc, streamUri);

        if (!_mediaPlayer.Play(_media))
        {
            HandleFailure(new InvalidOperationException("LibVLC could not start the stream."));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StopAndDisposePlayer();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            StopAndDisposePlayer();
        }

        return ValueTask.CompletedTask;
    }

    private void OnAudioPlay(IntPtr opaque, IntPtr samples, uint count, long presentationTime)
    {
        try
        {
            var pcm = LibVlcPcm.ReadInterleavedS16(samples, checked((int)count), (int)Channels);
            PcmReceived?.Invoke(this, new LibVlcPcmEventArgs(pcm, new AudioFormat((int)SampleRate, (int)Channels)));
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }
    }

    private void OnBuffering(object? sender, MediaPlayerBufferingEventArgs eventArgs) =>
        BufferingChanged?.Invoke(this, eventArgs.Cache);

    private void OnEncounteredError(object? sender, EventArgs eventArgs) =>
        HandleFailure(new InvalidOperationException("LibVLC reported a stream playback error."));

    private void HandleFailure(Exception exception) => Failed?.Invoke(this, exception);

    private void StopAndDisposePlayer()
    {
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.Buffering -= OnBuffering;
            _mediaPlayer.EncounteredError -= OnEncounteredError;
            _mediaPlayer.Stop();
            _mediaPlayer.Dispose();
            _mediaPlayer = null;
        }

        _media?.Dispose();
        _media = null;
        _libVlc?.Dispose();
        _libVlc = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LibVlcPlayer));
        }
    }
}
