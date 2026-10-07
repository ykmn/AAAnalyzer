using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed record RtaBand(double CenterHz, float Magnitude);

public static class RtaBandAggregator
{
    public static IReadOnlyList<RtaBand> Aggregate(Spectrum spectrum, RtaResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(spectrum);

        var bandsPerOctave = GetBandsPerOctave(resolution);
        var firstIndex = (int)Math.Ceiling(bandsPerOctave * Math.Log2(FrequencyScale.MinimumHertz / 1_000d));
        var lastIndex = (int)Math.Floor(bandsPerOctave * Math.Log2(FrequencyScale.MaximumHertz / 1_000d));
        var bands = new List<RtaBand>(lastIndex - firstIndex + 1);

        for (var index = firstIndex; index <= lastIndex; index++)
        {
            var center = 1_000d * Math.Pow(2, (double)index / bandsPerOctave);
            var lower = center / Math.Pow(2, 0.5d / bandsPerOctave);
            var upper = center * Math.Pow(2, 0.5d / bandsPerOctave);
            bands.Add(new RtaBand(center, FindPeak(spectrum, lower, upper)));
        }

        return bands;
    }

    private static int GetBandsPerOctave(RtaResolution resolution) => resolution switch
    {
        RtaResolution.One => 1,
        RtaResolution.OneThird => 3,
        RtaResolution.OneSixth => 6,
        RtaResolution.OneTwelfth => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(resolution)),
    };

    private static float FindPeak(Spectrum spectrum, double lowerHz, double upperHz)
    {
        var first = Math.Max(0, (int)Math.Ceiling(lowerHz / spectrum.BinWidthHz));
        var last = Math.Min(spectrum.Magnitudes.Count - 1, (int)Math.Floor(upperHz / spectrum.BinWidthHz));
        var peak = 0f;
        for (var bin = first; bin <= last; bin++)
        {
            peak = Math.Max(peak, spectrum.Magnitudes[bin]);
        }

        return peak;
    }
}
