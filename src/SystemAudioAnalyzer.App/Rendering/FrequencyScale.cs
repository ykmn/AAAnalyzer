namespace SystemAudioAnalyzer.App.Rendering;

using SystemAudioAnalyzer.App.Settings;

public static class FrequencyScale
{
    public const double MinimumHertz = 20d;

    public static double ToNormalized(double hertz, AnalyzerFrequencyScale scale, double maxHertz)
    {
        ValidateHertz(hertz, maxHertz);
        return scale switch
        {
            AnalyzerFrequencyScale.Linear => (hertz - MinimumHertz) / (maxHertz - MinimumHertz),
            AnalyzerFrequencyScale.Logarithmic => Math.Log(hertz / MinimumHertz) / Math.Log(maxHertz / MinimumHertz),
            _ => throw new ArgumentOutOfRangeException(nameof(scale)),
        };
    }

    public static double ToHertz(double normalized, AnalyzerFrequencyScale scale, double maxHertz)
    {
        if (normalized is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(normalized));
        }

        return scale switch
        {
            AnalyzerFrequencyScale.Linear => MinimumHertz + normalized * (maxHertz - MinimumHertz),
            AnalyzerFrequencyScale.Logarithmic => MinimumHertz * Math.Pow(maxHertz / MinimumHertz, normalized),
            _ => throw new ArgumentOutOfRangeException(nameof(scale)),
        };
    }

    public static string Format(double hertz)
    {
        if (hertz < MinimumHertz)
        {
            throw new ArgumentOutOfRangeException(nameof(hertz));
        }

        return hertz < 1_000d
            ? hertz.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " " + Localization.Localizer.T("UnitHz")
            : hertz < 10_000d
                ? (hertz / 1_000d).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " " + Localization.Localizer.T("UnitKhz")
                : (hertz / 1_000d).ToString("00.0", System.Globalization.CultureInfo.InvariantCulture) + " " + Localization.Localizer.T("UnitKhz");
    }

    private static void ValidateHertz(double hertz, double maxHertz)
    {
        if (hertz < MinimumHertz || hertz > maxHertz)
        {
            throw new ArgumentOutOfRangeException(nameof(hertz));
        }
    }
}
