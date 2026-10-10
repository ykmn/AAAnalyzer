using LibVLCSharp.Shared;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Diagnostics;

namespace SystemAudioAnalyzer.App.Sources;

public sealed class LibVlcPlayer : ILibVlcPlayer
{
    private const uint Channels = 2;
    // A roomy card buffer rides out CPU stalls (start-up JIT, UI work) that would otherwise glitch the sound.
    private const int OutputLatencyMilliseconds = 200;
    private uint _sampleRate = 48_000;
    private Media? _media;
    private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer;
    private bool _disposed;
    private readonly string? _playbackDeviceId;
    private readonly int _playbackBufferMilliseconds;
    private WasapiOut? _output;
    private JitterBuffer? _jitter;
    private CancellationTokenSource? _releaseCancel;
    private Thread? _releaseThread;

    private readonly Action<string>? _diagnostic;
    private readonly Stopwatch _streamClock = new();
    private long _lastArrivalMs;
    private long _expectedPresentationUs = long.MinValue;
    private long _arrivals;

    public LibVlcPlayer(string? playbackDeviceId = null, int playbackBufferMilliseconds = 0, Action<string>? diagnostic = null)
    {
        _diagnostic = diagnostic;
        _playbackDeviceId = playbackDeviceId;
        _playbackBufferMilliseconds = Math.Clamp(playbackBufferMilliseconds, 0, 5000);
    }

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
        _streamClock.Reset();
        _arrivals = 0;
        _expectedPresentationUs = long.MinValue;
        StartPlayback((int)_sampleRate);
        StartRelease((int)_sampleRate);
        return 0;
    }

    private void OnAudioCleanup(IntPtr opaque)
    {
        StopRelease();
        StopPlayback();
    }

    // The amem callbacks silence VLC's own output, so the decoded PCM is also played here. A playback failure must not
    // stop the analysis, hence it is swallowed.
    private void StartPlayback(int sampleRate)
    {
        StopPlayback();
        var jitter = new JitterBuffer(sampleRate, (int)Channels, _playbackBufferMilliseconds);
        _jitter = jitter;
        jitter.Underrun += delay => _diagnostic?.Invoke($"Stream playback underrun; rebuffering {delay} ms");
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = string.IsNullOrEmpty(_playbackDeviceId)
                ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                : enumerator.GetDevice(_playbackDeviceId);
            var output = new WasapiOut(device, AudioClientShareMode.Shared, true, OutputLatencyMilliseconds);
            output.Init(jitter);
            output.Play();
            jitter.OutputLatencyFrames = (long)sampleRate * OutputLatencyMilliseconds / 1000;
            _output = output;
        }
        catch (Exception)
        {
            StopPlayback(keepJitter: true);
        }
    }

    private void StopPlayback(bool keepJitter = false)
    {
        var output = _output;
        _output = null;
        if (!keepJitter) _jitter = null;
        try
        {
            output?.Dispose();
        }
        catch (Exception)
        {
        }
    }

    // Analysis gets each block when the sound card has consumed it (or, with no working output, once the buffer delay
    // has passed), so the display stays in step with what is heard.
    private void StartRelease(int sampleRate)
    {
        StopRelease();
        var jitter = _jitter ?? new JitterBuffer(sampleRate, (int)Channels, _playbackBufferMilliseconds);
        _jitter = jitter;
        var format = new AudioFormat(sampleRate, (int)Channels);
        var cancel = new CancellationTokenSource();
        _releaseCancel = cancel;
        _releaseThread = new Thread(() => ReleaseLoop(jitter, format, cancel.Token)) { IsBackground = true, Name = "LibVlcRelease", Priority = ThreadPriority.AboveNormal };
        _releaseThread.Start();
    }

    private void StopRelease()
    {
        var cancel = _releaseCancel;
        var thread = _releaseThread;
        _releaseCancel = null;
        _releaseThread = null;
        cancel?.Cancel();
        if (thread is not null && thread != Thread.CurrentThread) thread.Join(1000);
        cancel?.Dispose();
    }

    private void ReleaseLoop(JitterBuffer jitter, AudioFormat format, CancellationToken token)
    {
        var nextReport = DateTime.UtcNow.AddSeconds(5);
        var started = Stopwatch.StartNew();
        var lastRelease = TimeSpan.Zero;
        var released = 0;
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_diagnostic is not null && DateTime.UtcNow >= nextReport)
                {
                    nextReport = DateTime.UtcNow.AddSeconds(5);
                    _diagnostic($"Stream playback: {jitter.Describe()}");
                }

                var useClock = _output is null;
                while (jitter.TryTakeAnalysisBlock(useClock, out var pcm))
                {
                    var now = started.Elapsed;
                    // Start-up diagnostics: a long pause between blocks handed to the analysis shows as a gap on the plots.
                    if (_diagnostic is not null && now - lastRelease > TimeSpan.FromMilliseconds(150))
                    {
                        _diagnostic($"Analysis feed gap {(now - lastRelease).TotalMilliseconds:0} ms at +{now.TotalSeconds:0.00} s after stream start, block #{released} ({jitter.Describe()})");
                    }

                    lastRelease = now;
                    released++;
                    PcmReceived?.Invoke(this, new LibVlcPcmEventArgs(pcm, format));
                }

                token.WaitHandle.WaitOne(5);
            }
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }
    }

    private void OnAudioPlay(IntPtr opaque, IntPtr samples, uint count, long presentationTime)
    {
        try
        {
            var pcm = LibVlcPcm.ReadInterleavedS16(samples, checked((int)count), (int)Channels);
            TraceArrival((int)count, presentationTime);
            _jitter?.Add(pcm);
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }
    }

    // Start-up trace: LibVLC delivering late, or skipping stream time (a presentation-time jump), would both be
    // audible as a dropout even though the playback buffer itself never ran dry.
    private void TraceArrival(int frames, long presentationUs)
    {
        if (_diagnostic is null) return;
        if (!_streamClock.IsRunning) _streamClock.Restart();
        var nowMs = _streamClock.ElapsedMilliseconds;
        var lateMs = nowMs - _lastArrivalMs;
        var jumpMs = _expectedPresentationUs == long.MinValue ? 0 : (presentationUs - _expectedPresentationUs) / 1000;
        if (_arrivals > 0 && nowMs < 30_000 && (lateMs > 150 || Math.Abs(jumpMs) > 30))
        {
            _diagnostic($"LibVLC audio at +{nowMs} ms: {lateMs} ms since previous block, presentation time jump {jumpMs} ms, block #{_arrivals} of {frames} frames");
        }

        _arrivals++;
        _lastArrivalMs = nowMs;
        _expectedPresentationUs = presentationUs + (frames * 1_000_000L / Math.Max(1, (int)_sampleRate));
    }

    private void OnBuffering(object? sender, MediaPlayerBufferingEventArgs eventArgs)
    {
        _diagnostic?.Invoke($"LibVLC buffering {eventArgs.Cache:0}%");
        BufferingChanged?.Invoke(this, eventArgs.Cache);
    }

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

        StopRelease();
        StopPlayback();

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
