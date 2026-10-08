namespace SystemAudioAnalyzer.App.Rendering;

using SystemAudioAnalyzer.App.Settings;

public static class FrequencyScale
{
    public const double MinimumHertz = 20d;
    public const double MaximumHertz = 20_000d;

    public static double ToNormalized(double hertz)
        => ToNormalized(hertz, AnalyzerFrequencyScale.Logarithmic);

    public static double ToNormalized(double hertz, AnalyzerFrequencyScale scale)
    {
        ValidateHertz(hertz);
        return scale switch
        {
            AnalyzerFrequencyScale.Linear => (hertz - MinimumHertz) / (MaximumHertz - MinimumHertz),
            AnalyzerFrequencyScale.Logarithmic => Math.Log(hertz / MinimumHertz) / Math.Log(MaximumHertz / MinimumHertz),
            _ => throw new ArgumentOutOfRangeException(nameof(scale)),
        };
    }

    public static double ToHertz(double normalized)
        => ToHertz(normalized, AnalyzerFrequencyScale.Logarithmic);

    public static double ToHertz(double normalized, AnalyzerFrequencyScale scale)
    {
        if (normalized is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(normalized));
        }

        return scale switch
        {
            AnalyzerFrequencyScale.Linear => MinimumHertz + normalized * (MaximumHertz - MinimumHertz),
            AnalyzerFrequencyScale.Logarithmic => MinimumHertz * Math.Pow(MaximumHertz / MinimumHertz, normalized),
            _ => throw new ArgumentOutOfRangeException(nameof(scale)),
        };
    }

    public static string Format(double hertz)
    {
        ValidateHertz(hertz);
        return hertz < 1_000d
            ? hertz.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " " + Localization.Localizer.T("UnitHz")
            : hertz < 10_000d
                ? (hertz / 1_000d).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " " + Localization.Localizer.T("UnitKhz")
                : (hertz / 1_000d).ToString("00.0", System.Globalization.CultureInfo.InvariantCulture) + " " + Localization.Localizer.T("UnitKhz");
    }

    private static void ValidateHertz(double hertz)
    {
        if (hertz is < MinimumHertz or > MaximumHertz)
        {
            throw new ArgumentOutOfRangeException(nameof(hertz));
        }
    }
}
