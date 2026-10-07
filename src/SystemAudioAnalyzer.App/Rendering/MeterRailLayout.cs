using SystemAudioAnalyzer.App.Settings;
using SystemAudioAnalyzer.Core;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed record LufsScaleRange(double TopDb, double BottomDb, double StepDb);

public sealed record MeterRailLayout(Rect LeftMaximum, Rect RightMaximum, Rect LeftOverload, Rect RightOverload, Rect LeftMeter, Rect RightMeter)
{
    public static double CalculateFillRatio(float linearLevel, double displayRangeDb)
    {
        if (!float.IsFinite(linearLevel) || linearLevel <= 0f || !double.IsFinite(displayRangeDb))
        {
            return 0d;
        }

        var rangeDb = Math.Max(1d, Math.Abs(displayRangeDb));
        var levelDb = 20d * Math.Log10(linearLevel);
        return Math.Clamp((levelDb + rangeDb) / rangeDb, 0d, 1d);
    }

    public static float? SelectLoudness(LoudnessMeasurement? measurement, LoudnessMetric metric)
    {
        if (measurement is null) return null;
        var value = metric switch
        {
            LoudnessMetric.Momentary => measurement.MomentaryLufs,
            LoudnessMetric.ShortTerm => measurement.ShortTermLufs,
            _ => measurement.IntegratedLufs,
        };
        return value.HasValue && float.IsFinite(value.Value) ? value : null;
    }

    public static LufsScaleRange ResolveLufsScale(MeterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.LufsScale switch
        {
            LufsScalePreset.Broadcast => new LufsScaleRange(0d, -36d, 3d),
            // A wider monitoring scale for quieter material; Custom retains user-entered values.
            LufsScalePreset.FullScale => new LufsScaleRange(0d, -60d, 6d),
            _ => new LufsScaleRange(settings.LufsScaleTop, settings.LufsScaleBottom, settings.LufsScaleStep),
        };
    }

    public static double CalculateLufsRatio(double value, LufsScaleRange scale) =>
        double.IsFinite(value) && double.IsFinite(scale.TopDb) && double.IsFinite(scale.BottomDb) && scale.TopDb > scale.BottomDb
            ? Math.Clamp((value - scale.BottomDb) / (scale.TopDb - scale.BottomDb), 0d, 1d)
            : 0d;

    public static IReadOnlyList<double> CreateLufsScaleTicks(MeterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var scale = ResolveLufsScale(settings);
        var range = scale.TopDb - scale.BottomDb;
        if (!double.IsFinite(range) || range <= 0d || !double.IsFinite(scale.StepDb) || scale.StepDb <= 0d)
        {
            return [];
        }

        var count = (int)Math.Min(1_000d, Math.Floor(range / scale.StepDb));
        var ticks = new double[count + 1];
        for (var index = 0; index <= count; index++)
        {
            ticks[index] = scale.BottomDb + (index * scale.StepDb);
        }
        return ticks;
    }

    public static MeterRailLayout Calculate(double width, double height)
    {
        var half = width / 2d;
        var leftX = 8d;
        var rightX = half + 8d;
        var barWidth = Math.Max(1d, half - 16d);
        var meterTop = Math.Min(56d, height);
        return new MeterRailLayout(
            new Rect(leftX, 2, barWidth, 14), new Rect(rightX, 2, barWidth, 14),
            new Rect(leftX, 30, barWidth, 13), new Rect(rightX, 30, barWidth, 13),
            new Rect(leftX, meterTop, barWidth, Math.Max(1d, height - meterTop - 6)),
            new Rect(rightX, meterTop, barWidth, Math.Max(1d, height - meterTop - 6)));
    }
}
