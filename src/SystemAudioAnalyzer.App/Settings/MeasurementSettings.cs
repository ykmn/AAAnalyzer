using SystemAudioAnalyzer.App.ViewModels;
using System.Text.Json.Serialization;

namespace SystemAudioAnalyzer.App.Settings;

public sealed record MeasurementSettings(
    [property: JsonRequired] AnalyzerSettings Analyzer,
    [property: JsonRequired] WaterfallSettings Waterfall,
    [property: JsonRequired] MeterSettings Meters,
    [property: JsonRequired] LoudnessDisplaySettings Loudness,
    [property: JsonRequired] RtaDisplaySettings Rta,
    [property: JsonRequired] PhaseDisplaySettings Phase)
{
    public static MeasurementSettings Default { get; } = new(
        new AnalyzerSettings(-120, "#FFFFFF", "#D1D5DB"),
        new WaterfallSettings(-110, 0, "#06B6D4"),
        new MeterSettings(-60, "#22C55E", "#EF4444"),
        new LoudnessDisplaySettings(60, LoudnessMetric.Integrated, true, 6, -18),
        new RtaDisplaySettings(RtaChannelMode.Mono, RtaResolution.OneThird, 0, 60, -36),
        new PhaseDisplaySettings(1));
}

public sealed record AnalyzerSettings([property: JsonRequired] double DisplayFloorDb, [property: JsonRequired] string CursorColor, [property: JsonRequired] string TextColor);

public sealed record WaterfallSettings([property: JsonRequired] double DisplayFloorDb, [property: JsonRequired] double DisplayOffsetDb, [property: JsonRequired] string PaletteColor);

public sealed record MeterSettings([property: JsonRequired] double DisplayRangeDb, [property: JsonRequired] string MeterColor, [property: JsonRequired] string OverloadColor);

public sealed record LoudnessDisplaySettings([property: JsonRequired] int HistorySeconds, [property: JsonRequired] LoudnessMetric Metric, [property: JsonRequired] bool AutoScale, [property: JsonRequired] double SpanLufs, [property: JsonRequired] double CentreLufs);

public sealed record RtaDisplaySettings([property: JsonRequired] RtaChannelMode Source, [property: JsonRequired] RtaResolution Resolution, [property: JsonRequired] double ScaleTopDb, [property: JsonRequired] double ScaleRangeDb, [property: JsonRequired] double TargetLineDb);

public sealed record PhaseDisplaySettings([property: JsonRequired] double Gain);

public enum LoudnessMetric
{
    Momentary,
    ShortTerm,
    Integrated,
}
