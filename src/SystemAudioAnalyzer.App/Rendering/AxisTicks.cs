using System.Globalization;

namespace SystemAudioAnalyzer.App.Rendering;

/// <summary>A scale mark; <see cref="Ratio"/> is the fraction of the plot height measured from the bottom.</summary>
public readonly record struct AxisTick(double Value, double Ratio, string Label);

public readonly record struct FrequencyLabel(double Hertz, string Label);

public readonly record struct TimeTick(double SecondsAgo, string Label);

public static class AxisTicks
{
    private static readonly double[] RtaHertz = [20, 28, 40, 56, 80, 112, 160, 224, 315, 450, 630, 900, 1_300, 1_800, 2_500, 3_600, 5_000, 7_100, 10_000, 14_000, 20_000];
    private static readonly double[] WaterfallHertz = [20, 50, 100, 200, 500, 1_000, 2_000, 5_000, 10_000, 20_000];
    private static readonly int[] TimeSteps = [5, 10, 15, 30, 60, 120, 300, 600, 900, 1_800, 3_600, 7_200, 14_400];

    public static IReadOnlyList<AxisTick> PeakRailDb(double rangeDb, double stepDb = 5)
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

    public static IReadOnlyList<AxisTick> LufsLabels(LufsScaleRange scale)
    {
        var span = scale.TopDb - scale.BottomDb;
        if (!double.IsFinite(span) || span <= 0 || !double.IsFinite(scale.StepDb) || scale.StepDb <= 0) return [];
        var ticks = new List<AxisTick>();
        for (var index = 1; index < 1_000; index++)
        {
            var value = scale.TopDb - (index * scale.StepDb);
            if (value <= scale.BottomDb + 1e-9) break;
            ticks.Add(new AxisTick(value, (value - scale.BottomDb) / span, Math.Abs(value).ToString("0", CultureInfo.InvariantCulture)));
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
