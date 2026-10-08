using System.Collections.Immutable;
using System.Windows.Media;
using SystemAudioAnalyzer.Core;

namespace SystemAudioAnalyzer.App.Settings;

public static class MeasurementSettingsValidator
{
    public static bool IsValid(MeasurementSettings settings) => Validate(settings).Count == 0;

    public static IReadOnlyList<string> Validate(MeasurementSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<string>();
        void Check(bool valid, string field)
        {
            if (!valid) errors.Add($"{field} contains an invalid value.");
        }
        void Finite(double value, string field) => Check(double.IsFinite(value), field);
        void NonNegative(double value, string field) => Check(double.IsFinite(value) && value >= 0, field);
        void Positive(double value, string field) => Check(double.IsFinite(value) && value > 0, field);
        void Color(string? value, string field) => Check(IsColor(value), field);
        void Gradient(ImmutableArray<ColorStop> stops, string field)
        {
            if (stops.IsDefaultOrEmpty || stops.Length < 2)
            {
                Check(false, field);
                return;
            }
            var previous = double.NegativeInfinity;
            foreach (var stop in stops)
            {
                if (stop is null)
                {
                    Check(false, field);
                    continue;
                }
                Check(double.IsFinite(stop.LevelDb) && stop.LevelDb > previous, field + ".LevelDb");
                Color(stop.Color, field + ".Color");
                previous = stop.LevelDb;
            }
        }

        if (settings.Analyzer is not { } analyzer) Check(false, nameof(settings.Analyzer));
        else
        {
            Check(analyzer.FftSize is >= 512 and <= 16384 && (analyzer.FftSize & (analyzer.FftSize - 1)) == 0, "Analyzer.FftSize");
            Check(Enum.IsDefined(analyzer.WindowFunction), "Analyzer.WindowFunction");
            Check(Enum.IsDefined(analyzer.FrequencyScale), "Analyzer.FrequencyScale");
            Check(Enum.IsDefined(analyzer.AmplitudeScale), "Analyzer.AmplitudeScale");
            Finite(analyzer.DisplayFloorDb, "Analyzer.DisplayFloorDb");
            Positive(analyzer.Gain, "Analyzer.Gain");
            Color(analyzer.CursorColor, "Analyzer.CursorColor");
            Color(analyzer.TextColor, "Analyzer.TextColor");
        }
        if (settings.Waterfall is not { } waterfall) Check(false, nameof(settings.Waterfall));
        else
        {
            Finite(waterfall.DisplayFloorDb, "Waterfall.DisplayFloorDb");
            Finite(waterfall.DisplayOffsetDb, "Waterfall.DisplayOffsetDb");
            Color(waterfall.PaletteColor, "Waterfall.PaletteColor");
            Gradient(waterfall.GradientStops, "Waterfall.GradientStops");
        }
        if (settings.Meters is not { } meters) Check(false, nameof(settings.Meters));
        else
        {
            Check(double.IsFinite(meters.DisplayRangeDb) && meters.DisplayRangeDb < 0, "Meters.DisplayRangeDb");
            Color(meters.MeterColor, "Meters.MeterColor");
            Color(meters.OverloadColor, "Meters.OverloadColor");
            NonNegative(meters.AttackMs, "Meters.AttackMs");
            NonNegative(meters.ReleaseMs, "Meters.ReleaseMs");
            NonNegative(meters.PeakHoldMs, "Meters.PeakHoldMs");
            Check(meters.IntegratedWindowSeconds is >= 1 and <= LoudnessMeter.MaxIntegratedWindowSeconds, "Meters.IntegratedWindowSeconds");
            Check(Enum.IsDefined(meters.LufsMetric), "Meters.LufsMetric");
            Check(Enum.IsDefined(meters.FontSize), "Meters.FontSize");
            Check(Enum.IsDefined(meters.LufsScale), "Meters.LufsScale");
            Finite(meters.LufsScaleTop, "Meters.LufsScaleTop");
            Check(double.IsFinite(meters.LufsScaleBottom) && meters.LufsScaleBottom < meters.LufsScaleTop, "Meters.LufsScaleBottom");
            Positive(meters.LufsScaleStep, "Meters.LufsScaleStep");
            Color(meters.PeakColor, "Meters.PeakColor");
            Color(meters.RmsColor, "Meters.RmsColor");
            Color(meters.LufsColor, "Meters.LufsColor");
            Color(meters.ClipColor, "Meters.ClipColor");
        }
        if (settings.Loudness is not { } loudness) Check(false, nameof(settings.Loudness));
        else
        {
            Check(loudness.HistorySeconds is >= 15 and <= 43200, "Loudness.HistorySeconds");
            Check(Enum.IsDefined(loudness.Metric), "Loudness.Metric");
            Positive(loudness.SpanLufs, "Loudness.SpanLufs");
            Finite(loudness.CentreLufs, "Loudness.CentreLufs");
            Finite(loudness.TargetLufs, "Loudness.TargetLufs");
            Gradient(loudness.GradientStops, "Loudness.GradientStops");
        }
        if (settings.Rta is not { } rta) Check(false, nameof(settings.Rta));
        else
        {
            Check(Enum.IsDefined(rta.Source), "Rta.Source");
            Check(Enum.IsDefined(rta.Resolution), "Rta.Resolution");
            Finite(rta.ScaleTopDb, "Rta.ScaleTopDb");
            Positive(rta.ScaleRangeDb, "Rta.ScaleRangeDb");
            Finite(rta.TargetLineDb, "Rta.TargetLineDb");
            Check(rta.AveragingCount is >= 1 and <= 1000, "Rta.AveragingCount");
            Finite(rta.TiltDbPerOctave, "Rta.TiltDbPerOctave");
            NonNegative(rta.ReleaseDbPerSecond, "Rta.ReleaseDbPerSecond");
            NonNegative(rta.TargetRangeDb, "Rta.TargetRangeDb");
            NonNegative(rta.PeakHoldMs, "Rta.PeakHoldMs");
            Color(rta.BarColor, "Rta.BarColor");
            Color(rta.PeakCapColor, "Rta.PeakCapColor");
            Color(rta.TargetBandColor, "Rta.TargetBandColor");
        }
        if (settings.Phase is not { } phase) Check(false, nameof(settings.Phase));
        else Check(double.IsFinite(phase.Gain) && phase.Gain is >= 0.25 and <= 4, "Phase.Gain");
        return errors;
    }

    private static bool IsColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { return ColorConverter.ConvertFromString(value) is Color; }
        catch (FormatException) { return false; }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
    }
}
