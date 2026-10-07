using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App.Settings;

public sealed record MeasurementSettings(
    AnalyzerSettings Analyzer,
    WaterfallSettings Waterfall,
    MeterSettings Meters,
    LoudnessDisplaySettings Loudness,
    RtaDisplaySettings Rta,
    PhaseDisplaySettings Phase)
{
    public static MeasurementSettings Default { get; } = new(
        new AnalyzerSettings(-120, "#FFFFFF", "#D1D5DB"),
        new WaterfallSettings(-110, 0, "#06B6D4"),
        new MeterSettings(-60, "#22C55E", "#EF4444"),
        new LoudnessDisplaySettings(60, LoudnessMetric.Integrated, true, 6, -18),
        new RtaDisplaySettings(RtaChannelMode.Mono, RtaResolution.OneThird, 0, 60, -36),
        new PhaseDisplaySettings(1));
}

public sealed record AnalyzerSettings(double DisplayFloorDb, string CursorColor, string TextColor);

public sealed record WaterfallSettings(double DisplayFloorDb, double DisplayOffsetDb, string PaletteColor);

public sealed record MeterSettings(double DisplayRangeDb, string MeterColor, string OverloadColor);

public sealed record LoudnessDisplaySettings(int HistorySeconds, LoudnessMetric Metric, bool AutoScale, double SpanLufs, double CentreLufs);

public sealed record RtaDisplaySettings(RtaChannelMode Source, RtaResolution Resolution, double ScaleTopDb, double ScaleRangeDb, double TargetLineDb);

public sealed record PhaseDisplaySettings(double Gain);

public enum LoudnessMetric
{
    Momentary,
    ShortTerm,
    Integrated,
}
