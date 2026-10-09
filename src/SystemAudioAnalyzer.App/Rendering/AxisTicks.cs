using System.Globalization;

namespace SystemAudioAnalyzer.App.Rendering;

/// <summary>A scale mark; <see cref="Ratio"/> is the fraction of the plot height measured from the bottom.</summary>
public readonly record struct AxisTick(double Value, double Ratio, string Label);

public readonly record struct FrequencyLabel(double Hertz, string Label);

public readonly record struct TimeTick(double SecondsAgo, string Label);

public static class AxisTicks
{
    private static readonly double[] RtaHertz = [20, 28, 40, 56, 80, 112, 160, 224, 315, 450, 630, 900, 1_300, 1_800, 2_500, 3_600, 5_000, 7_100, 10_000, 14_000, 20_000];
    private static readonly double[] WaterfallHertz =
    [
        20, 30, 40, 50, 60, 70, 80, 90, 100, 150, 200, 300, 400, 500, 600, 700, 800, 900,
        1_000, 1_500, 2_000, 3_000, 4_000, 5_000, 6_000, 7_000, 8_000, 9_000,
        10_000, 12_000, 14_000, 16_000, 18_000, 20_000, 22_000, 24_000,
    ];
    private static readonly int[] TimeSteps = [5, 10, 15, 30, 60, 120, 300, 600, 900, 1_800, 3_600, 7_200, 14_400];

    public static IReadOnlyList<AxisTick> PeakRailDb(double rangeDb, double stepDb = 3)
    {
        var range = Math.Max(1d, Math.Abs(rangeDb));
        var step = Math.Max(1d, stepDb);
        var ticks = new List<AxisTick>();
        for (var value = -step; value > -range; value -= step)
        {
            ticks.Add(new AxisTick(value, (value + range) / range, value.ToString("0", CultureInfo.InvariantCulture)));
        }
        return ticks;
    }

    /// <summary>Marks at every multiple of <paramref name="step"/> inside the scale, signed.</summary>
    public static IReadOnlyList<AxisTick> LufsLabels(LufsScaleRange scale, double step = 3)
    {
        var span = scale.TopDb - scale.BottomDb;
        if (!double.IsFinite(span) || span <= 0 || step <= 0) return [];
        var ticks = new List<AxisTick>();
        for (var value = Math.Ceiling(scale.BottomDb / step) * step; value <= scale.TopDb + 1e-9 && ticks.Count < 1_000; value += step)
        {
            ticks.Add(new AxisTick(value, (value - scale.BottomDb) / span, value.ToString("0", CultureInfo.InvariantCulture)));
        }
        return ticks;
    }

    public static IReadOnlyList<AxisTick> RtaDb(double topDb, double rangeDb, double stepDb)
    {
        var range = Math.Max(1d, rangeDb);
        var step = Math.Max(1d, stepDb);
        var ticks = new List<AxisTick>();
        for (var offset = 0d; offset <= range + 1e-9; offset += step)
        {
            var value = topDb - offset;
            ticks.Add(new AxisTick(value, (value - (topDb - range)) / range, value.ToString("0", CultureInfo.InvariantCulture)));
        }
        return ticks;
    }

    public static string FormatHertz(double hertz) => hertz switch
    {
        >= 10_000 => (hertz / 1_000d).ToString("0", CultureInfo.InvariantCulture) + "k",
        >= 1_000 => (hertz / 1_000d).ToString("0.0", CultureInfo.InvariantCulture) + "k",
        _ => hertz.ToString("0", CultureInfo.InvariantCulture),
    };

    public static IReadOnlyList<FrequencyLabel> RtaFrequencyLabels() => RtaHertz.Select(hertz => new FrequencyLabel(hertz, FormatHertz(hertz))).ToArray();

    public static IReadOnlyList<FrequencyLabel> WaterfallFrequencyLabels() => WaterfallHertz.Select(hertz => new FrequencyLabel(hertz, FormatHertz(hertz))).ToArray();

    public static double LoudnessYStep(double spanLufs) => spanLufs switch
    {
        < 12 => 1,
        <= 24 => 2,
        <= 60 => 5,
        _ => 10,
    };

    public static int LoudnessTimeStep(double visibleSeconds)
    {
        foreach (var step in TimeSteps)
        {
            if (visibleSeconds / step <= 8d) return step;
        }
        return TimeSteps[^1];
    }

    public static IReadOnlyList<TimeTick> TimeTicks(DateTimeOffset now, TimeSpan window, int stepSeconds)
    {
        if (stepSeconds < 1 || window <= TimeSpan.Zero) return [];
        var offsetSeconds = now.Offset.TotalSeconds;
        var nowLocal = (now.ToUnixTimeMilliseconds() / 1_000d) + offsetSeconds;
        var ticks = new List<TimeTick>();
        for (var t = Math.Floor(nowLocal / stepSeconds) * stepSeconds; nowLocal - t <= window.TotalSeconds; t -= stepSeconds)
        {
            var label = DateTimeOffset.FromUnixTimeSeconds((long)(t - offsetSeconds)).ToOffset(now.Offset).ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            ticks.Add(new TimeTick(nowLocal - t, label));
        }
        return ticks;
    }
}
