namespace SystemAudioAnalyzer.Core;

public sealed class LoudnessMeter
{
    public const int MaxIntegratedWindowSeconds = 3_600;

    private readonly Queue<double> _momentaryEnergies = new();
    private readonly Queue<double> _shortTermEnergies = new();
    private readonly Queue<double> _integratedBlocks = new();
    private long _integratedBlockCapacity;
    private Biquad[] _preFilters = [];
    private Biquad[] _highPassFilters = [];
    private AudioFormat? _format;
    private double _momentarySum;
    private double _shortTermSum;
    private double _blockSum;
    private int _blockFrames;

    public LoudnessMeter(int integratedWindowSeconds = 600)
    {
        if (integratedWindowSeconds is <= 0 or > MaxIntegratedWindowSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(integratedWindowSeconds), $"Integrated window must be between 1 and {MaxIntegratedWindowSeconds} seconds.");
        }

        IntegratedWindowSeconds = integratedWindowSeconds;
        _integratedBlockCapacity = integratedWindowSeconds * 10L;
    }

    public int IntegratedWindowSeconds { get; private set; }

    public int BufferedIntegratedBlockCount => _integratedBlocks.Count;

    public void SetIntegratedWindowSeconds(int seconds)
    {
        if (seconds is <= 0 or > MaxIntegratedWindowSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), $"Integrated window must be between 1 and {MaxIntegratedWindowSeconds} seconds.");
        }

        IntegratedWindowSeconds = seconds;
        _integratedBlockCapacity = seconds * 10L;
        while (_integratedBlocks.Count > _integratedBlockCapacity)
        {
            _integratedBlocks.Dequeue();
        }
    }

    public LoudnessMeasurement Process(ReadOnlySpan<float> interleavedSamples, AudioFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (_format != format)
        {
            ResetForFormat(format);
        }

        var frames = interleavedSamples.Length / format.Channels;
        var momentaryFrames = checked((int)Math.Round(format.SampleRate * 0.4));
        var shortTermFrames = checked(format.SampleRate * 3);
        var blockFrames = checked((int)Math.Round(format.SampleRate * 0.1));
        for (var frame = 0; frame < frames; frame++)
        {
            var energy = 0d;
            for (var channel = 0; channel < format.Channels; channel++)
            {
                var sample = interleavedSamples[(frame * format.Channels) + channel];
                var weighted = _highPassFilters[channel].Process(_preFilters[channel].Process(sample));
                energy += weighted * weighted;
            }

            // BS.1770 sums the channel energies; averaging them would read 3 dB low on identical stereo channels.
            Append(_momentaryEnergies, ref _momentarySum, energy, momentaryFrames);
            Append(_shortTermEnergies, ref _shortTermSum, energy, shortTermFrames);
            _blockSum += energy;
            _blockFrames++;
            if (_blockFrames == blockFrames)
            {
                _integratedBlocks.Enqueue(_blockSum / _blockFrames);
                if (_integratedBlocks.Count > _integratedBlockCapacity)
                {
                    _integratedBlocks.Dequeue();
                }
                _blockSum = 0d;
                _blockFrames = 0;
            }
        }

        return new LoudnessMeasurement(
            ToLufs(_momentaryEnergies.Count == 0 ? 0d : _momentarySum / _momentaryEnergies.Count, _momentaryEnergies.Count == momentaryFrames),
            ToLufs(_shortTermEnergies.Count == 0 ? 0d : _shortTermSum / _shortTermEnergies.Count, _shortTermEnergies.Count == shortTermFrames),
            CalculateIntegrated());
    }

    public void Reset()
    {
        _momentaryEnergies.Clear();
        _shortTermEnergies.Clear();
        _integratedBlocks.Clear();
        _momentarySum = 0d;
        _shortTermSum = 0d;
        _blockSum = 0d;
        _blockFrames = 0;
        foreach (var filter in _preFilters)
        {
            filter.Reset();
        }

        foreach (var filter in _highPassFilters)
        {
            filter.Reset();
        }
    }

    private void ResetForFormat(AudioFormat format)
    {
        _format = format;
        _preFilters = Enumerable.Range(0, format.Channels).Select(_ => Biquad.CreateKWeightingShelf(format.SampleRate)).ToArray();
        _highPassFilters = Enumerable.Range(0, format.Channels).Select(_ => Biquad.CreateRlbHighPass(format.SampleRate)).ToArray();
        Reset();
    }

    private float? CalculateIntegrated()
    {
        var absoluteGatedEnergy = 0d;
        var absoluteGatedCount = 0;
        foreach (var block in _integratedBlocks)
        {
            if (ToLufs(block, true) is >= -70f)
            {
                absoluteGatedEnergy += block;
                absoluteGatedCount++;
            }
        }

        if (absoluteGatedCount == 0) return null;

        var relativeGate = ToLufs(absoluteGatedEnergy / absoluteGatedCount, true)!.Value - 10f;
        var gatedEnergy = 0d;
        var gatedCount = 0;
        foreach (var block in _integratedBlocks)
        {
            var blockLufs = ToLufs(block, true);
            if (blockLufs >= Math.Max(-70f, relativeGate))
            {
                gatedEnergy += block;
                gatedCount++;
            }
        }

        return gatedCount == 0 ? null : ToLufs(gatedEnergy / gatedCount, true);
    }

    private static void Append(Queue<double> values, ref double sum, double value, int maximumCount)
    {
        values.Enqueue(value);
        sum += value;
        if (values.Count > maximumCount)
        {
            sum -= values.Dequeue();
        }
    }

    private static float? ToLufs(double energy, bool hasFullWindow)
    {
        if (!hasFullWindow || energy <= 0d)
        {
            return null;
        }

        return (float)(-0.691d + (10d * Math.Log10(energy)));
    }

    private sealed class Biquad(double b0, double b1, double b2, double a1, double a2)
    {
        private double _x1;
        private double _x2;
        private double _y1;
        private double _y2;

        public double Process(double input)
        {
            var output = (b0 * input) + (b1 * _x1) + (b2 * _x2) - (a1 * _y1) - (a2 * _y2);
            _x2 = _x1;
            _x1 = input;
            _y2 = _y1;
            _y1 = output;
            return output;
        }

        public void Reset() => (_x1, _x2, _y1, _y2) = (0d, 0d, 0d, 0d);

        public static Biquad CreateKWeightingShelf(int sampleRate) =>
            CreateHighShelf(sampleRate, frequency: 1_681.974450955533, gainDb: 3.999843853973347, q: 0.7071752369554196);

        public static Biquad CreateRlbHighPass(int sampleRate) =>
            CreateHighPass(sampleRate, frequency: 38.13547087602444, q: 0.5003270373238773);

        private static Biquad CreateHighPass(int sampleRate, double frequency, double q)
        {
            var omega = 2d * Math.PI * frequency / sampleRate;
            var cosine = Math.Cos(omega);
            var alpha = Math.Sin(omega) / (2d * q);
            return Normalize((1d + cosine) / 2d, -(1d + cosine), (1d + cosine) / 2d, 1d + alpha, -2d * cosine, 1d - alpha);
        }

        private static Biquad CreateHighShelf(int sampleRate, double frequency, double gainDb, double q)
        {
            var amplitude = Math.Pow(10d, gainDb / 40d);
            var omega = 2d * Math.PI * frequency / sampleRate;
            var cosine = Math.Cos(omega);
            var alpha = Math.Sin(omega) / (2d * q);
            var squareRootAmplitude = Math.Sqrt(amplitude);
            return Normalize(
                amplitude * ((amplitude + 1d) + ((amplitude - 1d) * cosine) + (2d * squareRootAmplitude * alpha)),
                -2d * amplitude * ((amplitude - 1d) + ((amplitude + 1d) * cosine)),
                amplitude * ((amplitude + 1d) + ((amplitude - 1d) * cosine) - (2d * squareRootAmplitude * alpha)),
                (amplitude + 1d) - ((amplitude - 1d) * cosine) + (2d * squareRootAmplitude * alpha),
                2d * ((amplitude - 1d) - ((amplitude + 1d) * cosine)),
                (amplitude + 1d) - ((amplitude - 1d) * cosine) - (2d * squareRootAmplitude * alpha));
        }

        private static Biquad Normalize(double b0, double b1, double b2, double a0, double a1, double a2) =>
            new(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
    }
}
