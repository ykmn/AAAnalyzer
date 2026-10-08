using System.Globalization;

namespace SystemAudioAnalyzer.App.Settings;

/// <summary>Pure settings transforms behind the Loudness and RTA toolbar buttons.</summary>
public static class ToolbarSettingsActions
{
    private static readonly int[] RollingWindows = [60, 300, 600, 1_800, 3_600];

    public static MeasurementSettings WithLoudnessMetric(MeasurementSettings settings, LoudnessMetric metric) =>
        settings with { Loudness = settings.Loudness with { Metric = metric } };

    public static MeasurementSettings WithLoudnessWindow(MeasurementSettings settings, int seconds) =>
        settings with { Loudness = settings.Loudness with { HistorySeconds = Math.Clamp(seconds, 15, 43_200) } };

    public static MeasurementSettings ZoomLoudness(MeasurementSettings settings, double factor) =>
        settings with { Loudness = settings.Loudness with { AutoScale = false, SpanLufs = Math.Clamp(settings.Loudness.SpanLufs * factor, 2, 60) } };

    public static MeasurementSettings ShiftLoudness(MeasurementSettings settings, double deltaLufs) =>
        settings with { Loudness = settings.Loudness with { AutoScale = false, CentreLufs = settings.Loudness.CentreLufs + deltaLufs } };

    public static MeasurementSettings WithLoudnessTarget(MeasurementSettings settings, double deltaLufs) =>
        settings with { Loudness = settings.Loudness with { TargetLufs = Math.Clamp(settings.Loudness.TargetLufs + deltaLufs, -70, 0) } };

    public static MeasurementSettings CycleRollingWindow(MeasurementSettings settings)
    {
        var current = settings.Meters.IntegratedWindowSeconds;
        var next = RollingWindows.FirstOrDefault(window => window > current);
        if (next == 0) next = RollingWindows[0];
        return settings with
        {
            Meters = settings.Meters with { IntegratedWindowSeconds = next },
            Loudness = settings.Loudness with { Metric = LoudnessMetric.Integrated },
        };
    }

    public static string LoudnessScaleText(MeasurementSettings settings)
    {
        if (settings.Loudness.AutoScale) return "auto";
        var half = settings.Loudness.SpanLufs / 2d;
        return string.Create(CultureInfo.InvariantCulture, $"{settings.Loudness.CentreLufs - half:0.#}..{settings.Loudness.CentreLufs + half:0.#}");
    }

    /// <summary>Steps the averaging by <paramref name="delta"/>; above 1 the count stays a multiple of 10 (1, 10, 20, ...).</summary>
    public static MeasurementSettings WithRtaAveraging(MeasurementSettings settings, int delta)
    {
        var count = Math.Clamp(settings.Rta.AveragingCount + delta, 1, 1_000);
        if (count > 1) count = Math.Max(10, count / 10 * 10);
        return settings with { Rta = settings.Rta with { AveragingCount = count } };
    }

    public static MeasurementSettings WithRtaTarget(MeasurementSettings settings, double deltaDb)
    {
        var rta = settings.Rta;
        var target = Math.Clamp(rta.TargetLineDb + deltaDb, rta.ScaleTopDb - rta.ScaleRangeDb, rta.ScaleTopDb);
        return settings with { Rta = rta with { TargetLineDb = target } };
    }

    /// <summary>Copies the values the Loudness/RTA toolbars can change from <paramref name="source"/> into <paramref name="target"/>.</summary>
    public static MeasurementSettings CopyToolbarFields(MeasurementSettings target, MeasurementSettings source) => target with
    {
        Rta = target.Rta with
        {
            Source = source.Rta.Source,
            Resolution = source.Rta.Resolution,
            AveragingCount = source.Rta.AveragingCount,
            TargetLineDb = source.Rta.TargetLineDb,
        },
        Loudness = target.Loudness with
        {
            Metric = source.Loudness.Metric,
            HistorySeconds = source.Loudness.HistorySeconds,
            AutoScale = source.Loudness.AutoScale,
            SpanLufs = source.Loudness.SpanLufs,
            CentreLufs = source.Loudness.CentreLufs,
            TargetLufs = source.Loudness.TargetLufs,
        },
        Meters = target.Meters with { IntegratedWindowSeconds = source.Meters.IntegratedWindowSeconds },
    };
}
