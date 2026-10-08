using SystemAudioAnalyzer.App.Settings;
using SystemAudioAnalyzer.Core;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed record LufsScaleRange(double TopDb, double BottomDb, double StepDb);

public sealed record MeterRailLayout(Rect LeftMaximum, Rect RightMaximum, Rect LeftOverload, Rect RightOverload, Rect LeftMeter, Rect RightMeter,
    Rect DbScale, Rect LufsMeter, Rect LufsScale, Rect ChannelLabels, Rect LufsReadout, Rect LufsCaption,
    Rect LeftCurrent, Rect RightCurrent)
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

    public static string LufsCaptionText(LoudnessMetric metric) => metric switch
    {
        LoudnessMetric.Momentary => "M LUFS",
        LoudnessMetric.ShortTerm => "S LUFS",
        _ => "I LUFS",
    };

    /// <summary>The LU bar follows the Loudness plot: a fixed plot range is read from settings, an automatic one from the plot itself.</summary>
    public static LufsScaleRange ResolveLufsRange(MeasurementSettings settings, (double Minimum, double Maximum)? plotRange)
    {
        var range = settings.Loudness.AutoScale ? plotRange : LoudnessDisplayScale.ResolveRange(settings.Loudness, []);
        return range is { } r ? new LufsScaleRange(r.Maximum, r.Minimum, 3d) : ResolveLufsScale(settings.Meters);
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
        const double edge = 2d, scaleWidth = 26d, gap = 2d, lufsGap = 4d, lufsWidth = 14d, lufsScaleWidth = 22d;
        const double rowHeight = 14d, peakRowHeight = 12d, overloadHeight = 8d, readoutHeight = 22d;
        const double bottomReserve = WorkspaceLayout.PlotBottomReserve;
        var fixedWidth = (edge * 2) + scaleWidth + (gap * 2) + lufsGap + lufsWidth + lufsScaleWidth;
        var barWidth = Math.Max(1d, (width - fixedWidth) / 2d);
        var scaleX = edge;
        var leftX = scaleX + scaleWidth + gap;
        var rightX = leftX + barWidth + gap;
        var lufsX = rightX + barWidth + lufsGap;
        var meterTop = Math.Min(WorkspaceLayout.PlotTop, Math.Max(0d, height));
        var meterHeight = Math.Max(1d, height - meterTop - bottomReserve);
        // Readouts stack directly above the bars: MAX, NOW, then the overload lamp.
        var overloadY = meterTop - overloadHeight - 2d;
        var currentY = overloadY - peakRowHeight - 1d;
        var maximumY = currentY - peakRowHeight - 1d;
        var labels = new Rect(edge, meterTop + meterHeight + 2d, Math.Max(1d, width - (edge * 2)), rowHeight);
        var readout = new Rect(0, labels.Bottom, Math.Max(1d, width), readoutHeight);
        return new MeterRailLayout(
            new Rect(leftX, maximumY, barWidth, peakRowHeight), new Rect(rightX, maximumY, barWidth, peakRowHeight),
            new Rect(leftX, overloadY, barWidth, overloadHeight), new Rect(rightX, overloadY, barWidth, overloadHeight),
            new Rect(leftX, meterTop, barWidth, meterHeight), new Rect(rightX, meterTop, barWidth, meterHeight),
            new Rect(scaleX, meterTop, scaleWidth, meterHeight),
            new Rect(lufsX, meterTop, lufsWidth, meterHeight),
            new Rect(lufsX + lufsWidth, meterTop, lufsScaleWidth, meterHeight),
            labels, readout, new Rect(0, readout.Bottom, Math.Max(1d, width), rowHeight),
            new Rect(leftX, currentY, barWidth, peakRowHeight), new Rect(rightX, currentY, barWidth, peakRowHeight));
    }

    /// <summary>Clicks on either channel's readouts or overload lamp reset that value for both channels.</summary>
    public MeterRailResetTarget? HitTest(Point point)
    {
        if (LeftMaximum.Contains(point) || RightMaximum.Contains(point) || LeftCurrent.Contains(point) || RightCurrent.Contains(point))
            return MeterRailResetTarget.Maximum;
        var left = LeftOverload;
        var right = RightOverload;
        left.Inflate(0, 3);
        right.Inflate(0, 3);
        return left.Contains(point) || right.Contains(point) ? MeterRailResetTarget.Overload : null;
    }
}
