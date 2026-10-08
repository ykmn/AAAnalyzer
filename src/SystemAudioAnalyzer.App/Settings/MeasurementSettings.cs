using SystemAudioAnalyzer.App.ViewModels;
using System.Text.Json.Serialization;
using System.Collections.Immutable;

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
        new AnalyzerSettings(-130, "#FFFFFF", "#FFFFFF"),
        new WaterfallSettings(-110, 0, "#06B6D4"),
        new MeterSettings(-60, "#22C55E", "#EF4444"),
        new LoudnessDisplaySettings(60, LoudnessMetric.Integrated, true, 6, -11),
        new RtaDisplaySettings(RtaChannelMode.Mono, RtaResolution.OneTwelfth, 0, 60, -36),
        new PhaseDisplaySettings(1));
}

public sealed record AnalyzerSettings([property: JsonRequired] double DisplayFloorDb, [property: JsonRequired] string CursorColor, [property: JsonRequired] string TextColor)
{
    // 8192 resolves the low 1/12-octave bands; 2048 smears energy from 100-200 Hz into everything below.
    public int FftSize { get; init; } = 8192;
    public AnalyzerWindowFunction WindowFunction { get; init; } = AnalyzerWindowFunction.Blackman;
    public AnalyzerFrequencyScale FrequencyScale { get; init; } = AnalyzerFrequencyScale.Linear;
    public AnalyzerAmplitudeScale AmplitudeScale { get; init; } = AnalyzerAmplitudeScale.Logarithmic;
    public double Gain { get; init; } = 1;
}

public sealed record WaterfallSettings([property: JsonRequired] double DisplayFloorDb, [property: JsonRequired] double DisplayOffsetDb, [property: JsonRequired] string PaletteColor)
{
    public ImmutableArray<ColorStop> GradientStops { get; init; } = [new(-110, "#000000"), new(-80, "#0080C0"), new(-55, "#00FF39"), new(-45, "#E8E800")];

    public bool Equals(WaterfallSettings? other) => other is not null
        && DisplayFloorDb.Equals(other.DisplayFloorDb) && DisplayOffsetDb.Equals(other.DisplayOffsetDb)
        && PaletteColor == other.PaletteColor && ColorStopEquality.Equals(GradientStops, other.GradientStops);

    public override int GetHashCode() => HashCode.Combine(DisplayFloorDb, DisplayOffsetDb, PaletteColor, ColorStopEquality.GetHashCode(GradientStops));
}

public sealed record MeterSettings([property: JsonRequired] double DisplayRangeDb, [property: JsonRequired] string MeterColor, [property: JsonRequired] string OverloadColor)
{
    public double AttackMs { get; init; } = 52;
    public double ReleaseMs { get; init; } = 494;
    public double PeakHoldMs { get; init; } = 561;
    public bool ShowClipIndicator { get; init; } = true;
    public bool ShowPeakReadout { get; init; } = true;
    public bool ShowRmsBars { get; init; }
    public bool ShowDbScale { get; init; } = true;
    public bool ShowLufsIndicator { get; init; } = true;
    public bool ShowLkfsReadout { get; init; } = true;
    public LoudnessMetric LufsMetric { get; init; } = LoudnessMetric.Integrated;
    public int IntegratedWindowSeconds { get; init; } = 600;
    public MeterFontSize FontSize { get; init; } = MeterFontSize.Large;
    public LufsScalePreset LufsScale { get; init; } = LufsScalePreset.Broadcast;
    public double LufsScaleTop { get; init; }
    public double LufsScaleBottom { get; init; } = -36;
    public double LufsScaleStep { get; init; } = 3;
    public string PeakColor { get; init; } = "#00FF99";
    public string RmsColor { get; init; } = "#00BF69";
    public string LufsColor { get; init; } = "#00B4DC";
    public string ClipColor { get; init; } = "#FF0000";
}

public sealed record LoudnessDisplaySettings([property: JsonRequired] int HistorySeconds, [property: JsonRequired] LoudnessMetric Metric, [property: JsonRequired] bool AutoScale, [property: JsonRequired] double SpanLufs, [property: JsonRequired] double CentreLufs)
{
    /// <summary>Reference loudness drawn as a line on the plot (EBU R128 programme loudness by default).</summary>
    public double TargetLufs { get; init; } = -23;
    /// <summary>Half-width of the band drawn around the target line.</summary>
    public double TargetRangeLu { get; init; } = 3;
    public ImmutableArray<ColorStop> GradientStops { get; init; } = [new(-15, "#2F6FD6"), new(-12, "#2FA84F"), new(-8, "#E0C93A"), new(-6, "#D6392F")];

    public bool Equals(LoudnessDisplaySettings? other) => other is not null
        && HistorySeconds == other.HistorySeconds && Metric == other.Metric && AutoScale == other.AutoScale
        && SpanLufs.Equals(other.SpanLufs) && CentreLufs.Equals(other.CentreLufs) && TargetLufs.Equals(other.TargetLufs) && TargetRangeLu.Equals(other.TargetRangeLu)
        && ColorStopEquality.Equals(GradientStops, other.GradientStops);

    public override int GetHashCode() => HashCode.Combine(HistorySeconds, Metric, AutoScale, SpanLufs, CentreLufs, TargetLufs, TargetRangeLu, ColorStopEquality.GetHashCode(GradientStops));
}

public sealed record RtaDisplaySettings([property: JsonRequired] RtaChannelMode Source, [property: JsonRequired] RtaResolution Resolution, [property: JsonRequired] double ScaleTopDb, [property: JsonRequired] double ScaleRangeDb, [property: JsonRequired] double TargetLineDb)
{
    public int AveragingCount { get; init; } = 50;
    public double TiltDbPerOctave { get; init; }
    public double ReleaseDbPerSecond { get; init; } = 500;
    public double TargetRangeDb { get; init; } = 3;
    public bool ShowPeakHoldCaps { get; init; } = true;
    public double PeakHoldMs { get; init; } = 1000;
    public string BarColor { get; init; } = "#C9A528";
    public string PeakCapColor { get; init; } = "#FFF6D6";
    public string TargetBandColor { get; init; } = "#5C1212";
}

public sealed record PhaseDisplaySettings([property: JsonRequired] double Gain);

public sealed record ColorStop([property: JsonRequired] double LevelDb, [property: JsonRequired] string Color);

public enum AnalyzerWindowFunction { Rectangular, Hann, Hamming, Blackman }
public enum AnalyzerFrequencyScale { Linear, Logarithmic }
public enum AnalyzerAmplitudeScale { Linear, Logarithmic }
public enum MeterFontSize { Small, Medium, Large }
public enum LufsScalePreset { Broadcast, FullScale, Custom }

internal static class ColorStopEquality
{
    public static bool Equals(ImmutableArray<ColorStop> first, ImmutableArray<ColorStop> second) =>
        first.IsDefault || second.IsDefault ? first.IsDefault == second.IsDefault : first.SequenceEqual(second);

    public static int GetHashCode(ImmutableArray<ColorStop> stops)
    {
        var hash = new HashCode();
        hash.Add(stops.IsDefault);
        if (!stops.IsDefault)
        {
            foreach (var stop in stops) hash.Add(stop);
        }
        return hash.ToHashCode();
    }
}

public enum LoudnessMetric
{
    Momentary,
    ShortTerm,
    Integrated,
}
