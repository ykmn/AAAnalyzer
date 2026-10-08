using LibVLCSharp.Shared;

namespace SystemAudioAnalyzer.App.Sources;

public sealed class LibVlcPlayer : ILibVlcPlayer
{
    private const uint Channels = 2;
    private uint _sampleRate = 48_000;
    private Media? _media;
    private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer;
    private bool _disposed;

    public event EventHandler<LibVlcPcmEventArgs>? PcmReceived;

    public event EventHandler<float>? BufferingChanged;

    public event EventHandler<Exception>? Failed;

    // Loading LibVLC and its plug-ins (and the first connection) can take many seconds, so it must not run on the
    // caller's thread: a blocked UI thread cannot repaint the status text.
    public Task PlayAsync(Uri streamUri, int networkCachingMilliseconds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(streamUri);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return Task.Run(() => Play(streamUri, networkCachingMilliseconds), cancellationToken);
    }

    private void Play(Uri streamUri, int networkCachingMilliseconds)
    {
        StopAndDisposePlayer();

        var libVlc = LibVlcRuntime.GetAsync().GetAwaiter().GetResult();
        _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(libVlc);
        _mediaPlayer.Buffering += OnBuffering;
        _mediaPlayer.EncounteredError += OnEncounteredError;
        _mediaPlayer.SetAudioCallbacks(OnAudioPlay, null, null, null, null);
        // Asking for a fixed rate makes VLC resample (44.1 -> 48 kHz adds a noise floor near -73 dB, which hides
        // the true spectrum above the codec's low-pass), so the format callback keeps the stream's own rate.
        _mediaPlayer.SetAudioFormatCallback(OnAudioSetup, OnAudioCleanup);
        _media = new Media(libVlc, streamUri);
        _media.AddOption($":network-caching={networkCachingMilliseconds}");

        if (!_mediaPlayer.Play(_media))
        {
            HandleFailure(new InvalidOperationException("LibVLC could not start the stream."));
        }
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

    private int OnAudioSetup(ref IntPtr opaque, ref IntPtr format, ref uint rate, ref uint channels)
    {
        // The amem output only honours S16N; the rate stays as decoded, the channels are mixed down to stereo.
        // The by-ref IntPtr is the native 4-byte format buffer itself: write exactly those four bytes ("S16N").
        System.Runtime.CompilerServices.Unsafe.As<IntPtr, int>(ref format) = 0x4E363153;
        _sampleRate = Math.Clamp(rate, 8_000u, 192_000u);
        rate = _sampleRate;
        channels = Channels;
        return 0;
    }

    private static void OnAudioCleanup(IntPtr opaque)
    {
    }

    private void OnAudioPlay(IntPtr opaque, IntPtr samples, uint count, long presentationTime)
    {
        try
        {
            var pcm = LibVlcPcm.ReadInterleavedS16(samples, checked((int)count), (int)Channels);
            PcmReceived?.Invoke(this, new LibVlcPcmEventArgs(pcm, new AudioFormat((int)_sampleRate, (int)Channels)));
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
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LibVlcPlayer));
        }
    }
}
