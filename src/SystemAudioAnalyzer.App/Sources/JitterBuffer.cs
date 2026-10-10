using NAudio.Wave;

namespace SystemAudioAnalyzer.App.Sources;

/// <summary>
/// Holds decoded stream audio ahead of the sound card. Playback starts once the configured delay has been collected and
/// pauses (silence) to collect it again after an underrun, so bursty delivery (HLS segments) does not stutter. It also
/// counts the frames the card has really consumed, which is the clock the analysis follows.
/// </summary>
internal sealed class JitterBuffer : IWaveProvider
{
    private readonly object _gate = new();
    private readonly Queue<Block> _queue = new();
    private readonly Queue<Block> _analysis = new();
    private readonly int _channels;
    private readonly long _basePrefillFrames;
    private readonly long _ceilingFrames;
    private readonly int _sampleRate;
    private long _prefillFrames;
    private long _queuedFrames;
    private long _consumedFrames;
    private long _enqueuedFrames;
    private int _offset;
    private bool _playing;
    private float[] _scratch = [];
    private long _underruns;
    private long _droppedFrames;

    public JitterBuffer(int sampleRate, int channels, int delayMilliseconds)
    {
        _channels = channels;
        _sampleRate = sampleRate;
        _basePrefillFrames = _prefillFrames = (long)sampleRate * Math.Max(0, delayMilliseconds) / 1000;
        _ceilingFrames = Math.Max(_basePrefillFrames, (long)sampleRate * 4);
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delayMilliseconds));
    }

    public WaveFormat WaveFormat { get; }

    /// <summary>Frames already handed to the card but not yet audible; analysis waits for them so display and sound line up.</summary>
    public long OutputLatencyFrames { get; set; }

    /// <summary>Raised when the card ran out of audio; the argument is the delay (ms) now collected before resuming.</summary>
    public event Action<int>? Underrun;

    private TimeSpan Delay { get; }

    public void Add(float[] pcm)
    {
        var frames = pcm.Length / _channels;
        if (frames == 0) return;
        lock (_gate)
        {
            _enqueuedFrames += frames;
            var block = new Block(pcm, _enqueuedFrames, DateTime.UtcNow.Ticks);
            _queue.Enqueue(block);
            _analysis.Enqueue(block);
            _queuedFrames += frames;
            // If the output runs slower than the stream, drop the oldest audio instead of growing without bound.
            while (_queuedFrames > _prefillFrames + ((long)_sampleRate * 3) && _queue.Count > 1)
            {
                var dropped = _queue.Dequeue();
                _queuedFrames -= (dropped.Pcm.Length - _offset) / _channels;
                _offset = 0;
                _consumedFrames = dropped.EndFrame;
                _droppedFrames += (dropped.Pcm.Length - _offset) / _channels;
            }
        }
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        var floats = count / sizeof(float);
        if (_scratch.Length < floats) _scratch = new float[floats];
        var output = _scratch;
        Array.Clear(output, 0, floats);
        var written = 0;
        int? underrunDelay = null;
        lock (_gate)
        {
            if (!_playing && _queue.Count > 0 && _queuedFrames >= _prefillFrames) _playing = true;
            while (_playing && written < floats && _queue.Count > 0)
            {
                var block = _queue.Peek();
                var take = Math.Min(floats - written, block.Pcm.Length - _offset);
                Array.Copy(block.Pcm, _offset, output, written, take);
                written += take;
                _offset += take;
                if (_offset >= block.Pcm.Length)
                {
                    _queue.Dequeue();
                    _offset = 0;
                }
            }

            var frames = written / _channels;
            _queuedFrames -= frames;
            _consumedFrames += frames;
            // After an underrun, collect the delay again rather than stuttering on every new block. Each underrun
            // also lengthens that delay (up to a ceiling), so a stream that delivers in bursts settles into smooth playback.
            if (_playing && written < floats)
            {
                _playing = false;
                _underruns++;
                _prefillFrames = Math.Min(_ceilingFrames, Math.Max(_basePrefillFrames, _prefillFrames) + (_sampleRate / 4));
                underrunDelay = (int)(_prefillFrames * 1000 / _sampleRate);
            }
        }

        if (underrunDelay is { } delay) Underrun?.Invoke(delay);

        Buffer.BlockCopy(output, 0, buffer, offset, count);
        return count;
    }

    public string Describe()
    {
        lock (_gate)
        {
            return $"queued={_queuedFrames * 1000 / _sampleRate} ms, prefill={_prefillFrames * 1000 / _sampleRate} ms, playing={_playing}, underruns={_underruns}, dropped={_droppedFrames * 1000 / _sampleRate} ms";
        }
    }

    /// <summary>The next block for analysis once it is due: consumed by the card, or old enough when no card is playing.</summary>
    public bool TryTakeAnalysisBlock(bool useClock, out float[] pcm)
    {
        lock (_gate)
        {
            if (_analysis.Count > 0)
            {
                var block = _analysis.Peek();
                var due = useClock
                    ? DateTime.UtcNow.Ticks - block.ArrivedTicks >= Delay.Ticks
                    : block.EndFrame <= _consumedFrames - OutputLatencyFrames;
                if (due)
                {
                    _analysis.Dequeue();
                    pcm = block.Pcm;
                    return true;
                }
            }
        }

        pcm = [];
        return false;
    }

    private sealed record Block(float[] Pcm, long EndFrame, long ArrivedTicks);
}
