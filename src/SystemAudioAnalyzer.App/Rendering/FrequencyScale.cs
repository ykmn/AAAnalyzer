namespace SystemAudioAnalyzer.App.Rendering;

public static class FrequencyScale
{
    public const double MinimumHertz = 20d;
    public const double MaximumHertz = 20_000d;

    public static double ToNormalized(double hertz)
    {
        ValidateHertz(hertz);
        return Math.Log(hertz / MinimumHertz) / Math.Log(MaximumHertz / MinimumHertz);
    }

    public static double ToHertz(double normalized)
    {
        if (normalized is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(normalized));
        }

        return MinimumHertz * Math.Pow(MaximumHertz / MinimumHertz, normalized);
    }

    public static string Format(double hertz)
    {
        ValidateHertz(hertz);
        return hertz < 1_000d
            ? hertz.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " Hz"
            : hertz < 10_000d
                ? (hertz / 1_000d).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " kHz"
                : (hertz / 1_000d).ToString("00.0", System.Globalization.CultureInfo.InvariantCulture) + " kHz";
    }

    private static void ValidateHertz(double hertz)
    {
        if (hertz is < MinimumHertz or > MaximumHertz)
        {
            throw new ArgumentOutOfRangeException(nameof(hertz));
        }
    }
}
