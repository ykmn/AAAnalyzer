using SystemAudioAnalyzer.App.ViewModels;
using SystemAudioAnalyzer.Core;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed record RtaBand(double CenterHz, float Magnitude)
{
    public double DisplayDb { get; init; } = 20 * Math.Log10(Math.Max(Magnitude, 0.000001f));

    public double PeakDb { get; init; } = 20 * Math.Log10(Math.Max(Magnitude, 0.000001f));
}

/// <summary>Maintains bounded RTA averaging and display ballistics outside the paint path.</summary>
public sealed class RtaBandAggregator
{
    private const int MaximumAveragingCount = 1_000;
    private readonly Queue<float[]> _spectra = new();
    private float[] _sumSpectrum = [];
    private double[] _displayDb = [];
    private double[] _peakDb = [];
    private DateTimeOffset[] _peakExpires = [];
    private DateTimeOffset? _lastUpdate;
    private int _sampleRate;
    private int _fftSize;
    private RtaResolution? _resolution;
    private RtaChannelMode? _source;

    public static IReadOnlyList<RtaBand> Aggregate(Spectrum spectrum, RtaResolution resolution, double maxHertz = 20_000)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        return Aggregate(spectrum.SampleRate, spectrum.FftSize, spectrum.Magnitudes, resolution, maxHertz);
    }

    private static IReadOnlyList<RtaBand> Aggregate(int sampleRate, int fftSize, IReadOnlyList<float> magnitudes, RtaResolution resolution, double maxHertz)
    {
        var bandsPerOctave = GetBandsPerOctave(resolution);
        var firstIndex = (int)Math.Ceiling(bandsPerOctave * Math.Log2(FrequencyScale.MinimumHertz / 1_000d));
        var lastIndex = (int)Math.Floor(bandsPerOctave * Math.Log2(maxHertz / 1_000d));
        var bands = new List<RtaBand>(lastIndex - firstIndex + 1);

        for (var index = firstIndex; index <= lastIndex; index++)
        {
            var center = 1_000d * Math.Pow(2, (double)index / bandsPerOctave);
            var lower = center / Math.Pow(2, 0.5d / bandsPerOctave);
            var upper = center * Math.Pow(2, 0.5d / bandsPerOctave);
            var magnitude = FindLevel(sampleRate, fftSize, magnitudes, lower, center, upper);
            bands.Add(new RtaBand(center, magnitude));
        }

        return bands;
    }

    public IReadOnlyList<RtaBand> Update(
        Spectrum spectrum,
        RtaResolution resolution,
        RtaChannelMode source,
        int averagingCount,
        double releaseDbPerSecond,
        double peakHoldMilliseconds,
        bool showPeakCaps,
        DateTimeOffset timestamp,
        double maxHertz = 20_000)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        if (averagingCount is < 1 or > MaximumAveragingCount)
            throw new ArgumentOutOfRangeException(nameof(averagingCount));
        if (!double.IsFinite(releaseDbPerSecond) || releaseDbPerSecond < 0)
            throw new ArgumentOutOfRangeException(nameof(releaseDbPerSecond));
        if (!double.IsFinite(peakHoldMilliseconds) || peakHoldMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(peakHoldMilliseconds));
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));

        if (_resolution != resolution || _source != source || _sampleRate != spectrum.SampleRate || _fftSize != spectrum.FftSize)
        {
            Reset();
            _resolution = resolution;
            _source = source;
            _sampleRate = spectrum.SampleRate;
            _fftSize = spectrum.FftSize;
        }

        if (_sumSpectrum.Length != spectrum.Magnitudes.Count)
        {
            _spectra.Clear();
            _sumSpectrum = new float[spectrum.Magnitudes.Count];
        }

        var latest = spectrum.Magnitudes.ToArray();
        _spectra.Enqueue(latest);
        for (var bin = 0; bin < latest.Length; bin++) _sumSpectrum[bin] += latest[bin];
        while (_spectra.Count > averagingCount)
        {
            var removed = _spectra.Dequeue();
            for (var bin = 0; bin < removed.Length; bin++) _sumSpectrum[bin] -= removed[bin];
        }
        var averaged = new float[_sumSpectrum.Length];
        for (var bin = 0; bin < averaged.Length; bin++) averaged[bin] = _sumSpectrum[bin] / _spectra.Count;

        var rawBands = Aggregate(spectrum.SampleRate, spectrum.FftSize, averaged, resolution, maxHertz);
        if (_displayDb.Length != rawBands.Count)
        {
            _displayDb = Enumerable.Repeat(double.NegativeInfinity, rawBands.Count).ToArray();
            _peakDb = Enumerable.Repeat(double.NegativeInfinity, rawBands.Count).ToArray();
            _peakExpires = new DateTimeOffset[rawBands.Count];
        }

        var elapsedSeconds = _lastUpdate.HasValue
            ? Math.Max(0, (timestamp - _lastUpdate.Value).TotalSeconds)
            : 0;
        var result = new RtaBand[rawBands.Count];
        for (var index = 0; index < rawBands.Count; index++)
        {
            var candidate = rawBands[index].DisplayDb;
            var displayed = double.IsNegativeInfinity(_displayDb[index]) || candidate >= _displayDb[index]
                ? candidate
                : Math.Max(candidate, _displayDb[index] - releaseDbPerSecond * elapsedSeconds);
            _displayDb[index] = displayed;

            if (!showPeakCaps || double.IsNegativeInfinity(_peakDb[index]) || displayed >= _peakDb[index] || timestamp >= _peakExpires[index])
            {
                _peakDb[index] = displayed;
                _peakExpires[index] = timestamp.AddMilliseconds(peakHoldMilliseconds);
            }

            var magnitude = (float)Math.Pow(10, displayed / 20d);
            result[index] = new RtaBand(rawBands[index].CenterHz, magnitude)
            {
                DisplayDb = displayed,
                PeakDb = _peakDb[index],
            };
        }

        _lastUpdate = timestamp;
        return result;
    }

    public static double ApplyTilt(double levelDb, double frequencyHz, double tiltDbPerOctave)
    {
        if (!double.IsFinite(levelDb)) throw new ArgumentOutOfRangeException(nameof(levelDb));
        if (!double.IsFinite(frequencyHz) || frequencyHz <= 0) throw new ArgumentOutOfRangeException(nameof(frequencyHz));
        if (!double.IsFinite(tiltDbPerOctave)) throw new ArgumentOutOfRangeException(nameof(tiltDbPerOctave));
        return levelDb + tiltDbPerOctave * Math.Log2(frequencyHz / 1_000d);
    }

    public void Reset()
    {
        _spectra.Clear();
        _sumSpectrum = [];
        _displayDb = [];
        _peakDb = [];
        _peakExpires = [];
        _lastUpdate = null;
    }

    private static int GetBandsPerOctave(RtaResolution resolution) => resolution switch
    {
        RtaResolution.One => 1,
        RtaResolution.OneThird => 3,
        RtaResolution.OneSixth => 6,
        RtaResolution.OneTwelfth => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(resolution)),
    };

    /// <summary>Peak of the bins inside the band; bands narrower than one FFT bin interpolate between the bins around their centre.</summary>
    private static float FindLevel(int sampleRate, int fftSize, IReadOnlyList<float> magnitudes, double lowerHz, double centerHz, double upperHz)
    {
        var binWidthHz = (float)sampleRate / fftSize;
        var first = Math.Max(0, (int)Math.Ceiling(lowerHz / binWidthHz));
        var last = Math.Min(magnitudes.Count - 1, (int)Math.Floor(upperHz / binWidthHz));
        if (first <= last)
        {
            var peak = 0f;
            for (var bin = first; bin <= last; bin++) peak = Math.Max(peak, magnitudes[bin]);
            return peak;
        }

        var position = Math.Clamp(centerHz / binWidthHz, 0d, magnitudes.Count - 1d);
        var lowerBin = (int)Math.Floor(position);
        var upperBin = Math.Min(magnitudes.Count - 1, lowerBin + 1);
        var fraction = (float)(position - lowerBin);
        return magnitudes[lowerBin] + ((magnitudes[upperBin] - magnitudes[lowerBin]) * fraction);
    }
}
