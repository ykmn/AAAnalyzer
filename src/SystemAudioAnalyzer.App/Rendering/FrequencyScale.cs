namespace SystemAudioAnalyzer.App.Rendering;

using SystemAudioAnalyzer.App.Settings;

public static class FrequencyScale
{
    public const double MinimumHertz = 20d;

    /// <summary>Maps a frequency to [0,1]. Out-of-range inputs (including a maxHertz the user is still mid-typing
    /// in Settings, transiently below MinimumHertz) are clamped rather than thrown, since this runs on every
    /// render while a live analysis keeps redrawing.</summary>
    public static double ToNormalized(double hertz, AnalyzerFrequencyScale scale, double maxHertz)
    {
        maxHertz = EffectiveMaxHertz(maxHertz);
        hertz = Math.Clamp(hertz, MinimumHertz, maxHertz);
        return scale switch
        {
            AnalyzerFrequencyScale.Linear => (hertz - MinimumHertz) / (maxHertz - MinimumHertz),
            AnalyzerFrequencyScale.Logarithmic => Math.Log(hertz / MinimumHertz) / Math.Log(maxHertz / MinimumHertz),
            _ => throw new ArgumentOutOfRangeException(nameof(scale)),
        };
    }

    public static double ToHertz(double normalized, AnalyzerFrequencyScale scale, double maxHertz)
    {
        maxHertz = EffectiveMaxHertz(maxHertz);
        normalized = Math.Clamp(normalized, 0d, 1d);
        return scale switch
        {
            AnalyzerFrequencyScale.Linear => MinimumHertz + normalized * (maxHertz - MinimumHertz),
            AnalyzerFrequencyScale.Logarithmic => MinimumHertz * Math.Pow(maxHertz / MinimumHertz, normalized),
            _ => throw new ArgumentOutOfRangeException(nameof(scale)),
        };
    }

    public static string Format(double hertz)
    {
        hertz = Math.Max(hertz, MinimumHertz);
        return hertz < 1_000d
            ? hertz.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " " + Localization.Localizer.T("UnitHz")
            : hertz < 10_000d
                ? (hertz / 1_000d).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " " + Localization.Localizer.T("UnitKhz")
                : (hertz / 1_000d).ToString("00.0", System.Globalization.CultureInfo.InvariantCulture) + " " + Localization.Localizer.T("UnitKhz");
    }

    /// <summary>Guards against a maxHertz at or below MinimumHertz (division by zero / log of <=1) while a
    /// Settings field is mid-edit.</summary>
    private static double EffectiveMaxHertz(double maxHertz) => Math.Max(maxHertz, MinimumHertz + 1d);
}
