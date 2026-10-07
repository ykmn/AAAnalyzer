using SystemAudioAnalyzer.App.Rendering;
using SystemAudioAnalyzer.App.Settings;
using System.Windows.Media;
using System.Collections.Immutable;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class MeasurementSettingsValidatorTests
{
    [Fact]
    public void ReferenceDefaultsAreValid()
    {
        var settings = MeasurementSettings.Default;
        Assert.Equal(2048, settings.Analyzer.FftSize);
        Assert.Equal(AnalyzerWindowFunction.Blackman, settings.Analyzer.WindowFunction);
        Assert.Equal(AnalyzerFrequencyScale.Linear, settings.Analyzer.FrequencyScale);
        Assert.Equal(AnalyzerAmplitudeScale.Logarithmic, settings.Analyzer.AmplitudeScale);
        Assert.Equal(-130, settings.Analyzer.DisplayFloorDb);
        Assert.Equal(1, settings.Analyzer.Gain);
        Assert.Equal(new double[] { -110, -80, -55, -45 }, settings.Waterfall.GradientStops.Select(stop => stop.LevelDb));
        Assert.Empty(MeasurementSettingsValidator.Validate(settings));
        Assert.True(MeasurementSettingsValidator.IsValid(settings));
    }

    [Fact]
    public void WaterfallDefaultColorsMatchReferenceSwatches()
    {
        Assert.Equal(new[] { "#000000", "#0080C0", "#00FF39", "#E8E800" },
            MeasurementSettings.Default.Waterfall.GradientStops.Select(stop => stop.Color));
    }

    [Theory]
    [InlineData(256)]
    [InlineData(32768)]
    [InlineData(1000)]
    public void InvalidFftSizesAreRejected(int size)
    {
        AssertInvalid(MeasurementSettings.Default with { Analyzer = MeasurementSettings.Default.Analyzer with { FftSize = size } });
    }

    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(4096)]
    [InlineData(8192)]
    [InlineData(16384)]
    public void SupportedFftSizesAreAccepted(int size)
    {
        Assert.True(MeasurementSettingsValidator.IsValid(MeasurementSettings.Default with { Analyzer = MeasurementSettings.Default.Analyzer with { FftSize = size } }));
    }

    [Fact]
    public void NaNInvalidEnumAndInvalidColorAreRejected()
    {
        AssertInvalid(MeasurementSettings.Default with { Analyzer = MeasurementSettings.Default.Analyzer with { Gain = double.NaN } });
        AssertInvalid(MeasurementSettings.Default with { Analyzer = MeasurementSettings.Default.Analyzer with { WindowFunction = (AnalyzerWindowFunction)999 } });
        AssertInvalid(MeasurementSettings.Default with { Analyzer = MeasurementSettings.Default.Analyzer with { FrequencyScale = (AnalyzerFrequencyScale)999 } });
        AssertInvalid(MeasurementSettings.Default with { Analyzer = MeasurementSettings.Default.Analyzer with { CursorColor = "invalid-color" } });
    }

    [Theory]
    [InlineData(-80, -110)]
    [InlineData(-80, -80)]
    [InlineData(double.NaN, -80)]
    [InlineData(-110, double.PositiveInfinity)]
    public void UnorderedDuplicateOrNonFiniteGradientStopsAreRejected(double first, double second)
    {
        var stops = new[] { new ColorStop(first, "#0000FF"), new ColorStop(second, "#00FFFF") }.ToImmutableArray();
        AssertInvalid(MeasurementSettings.Default with { Waterfall = MeasurementSettings.Default.Waterfall with { GradientStops = stops } });
        AssertInvalid(MeasurementSettings.Default with { Loudness = MeasurementSettings.Default.Loudness with { GradientStops = stops } });
    }

    [Fact]
    public void GradientsRequireTwoValidColorStops()
    {
        AssertInvalid(MeasurementSettings.Default with { Waterfall = MeasurementSettings.Default.Waterfall with { GradientStops = [new(-110, "#0000FF")] } });
        AssertInvalid(MeasurementSettings.Default with { Waterfall = MeasurementSettings.Default.Waterfall with { GradientStops = [new(-110, "bad"), new(-80, "#00FFFF")] } });
    }

    [Fact]
    public void GradientInterpolatesBlueCyanMidpointAndClampsEndpoints()
    {
        ColorStop[] stops = [new(-110, "#0000FF"), new(-80, "#00FFFF")];
        Assert.Equal(Color.FromRgb(0, 128, 255), ColorGradient.Sample(stops, -95));
        Assert.Equal(Colors.Blue, ColorGradient.Sample(stops, -130));
        Assert.Equal(Colors.Cyan, ColorGradient.Sample(stops, -40));
    }

    private static void AssertInvalid(MeasurementSettings settings)
    {
        Assert.NotEmpty(MeasurementSettingsValidator.Validate(settings));
        Assert.False(MeasurementSettingsValidator.IsValid(settings));
    }

    [Fact]
    public void MeterLoudnessAndRtaReferenceDefaultsArePreserved()
    {
        var settings = MeasurementSettings.Default;
        Assert.Equal(52, settings.Meters.AttackMs);
        Assert.Equal(494, settings.Meters.ReleaseMs);
        Assert.Equal(561, settings.Meters.PeakHoldMs);
        Assert.True(settings.Meters.ShowClipIndicator);
        Assert.True(settings.Meters.ShowPeakReadout);
        Assert.False(settings.Meters.ShowRmsBars);
        Assert.True(settings.Meters.ShowDbScale);
        Assert.True(settings.Meters.ShowLufsIndicator);
        Assert.True(settings.Meters.ShowLkfsReadout);
        Assert.Equal(LoudnessMetric.Integrated, settings.Meters.LufsMetric);
        Assert.Equal(600, settings.Meters.IntegratedWindowSeconds);
        Assert.Equal(MeterFontSize.Large, settings.Meters.FontSize);
        Assert.Equal(LufsScalePreset.Broadcast, settings.Meters.LufsScale);
        Assert.Equal(0, settings.Meters.LufsScaleTop);
        Assert.Equal(-36, settings.Meters.LufsScaleBottom);
        Assert.Equal(3, settings.Meters.LufsScaleStep);
        Assert.Equal("#00FF99", settings.Meters.PeakColor);
        Assert.Equal("#00BF69", settings.Meters.RmsColor);
        Assert.Equal("#00B4DC", settings.Meters.LufsColor);
        Assert.Equal("#FF0000", settings.Meters.ClipColor);
        Assert.Equal(60, settings.Loudness.HistorySeconds);
        Assert.Equal(-11, settings.Loudness.CentreLufs);
        Assert.Equal(new double[] { -15, -12, -8, -6 }, settings.Loudness.GradientStops.Select(stop => stop.LevelDb));
        Assert.Equal(SystemAudioAnalyzer.App.ViewModels.RtaResolution.OneTwelfth, settings.Rta.Resolution);
        Assert.Equal(50, settings.Rta.AveragingCount);
        Assert.Equal(0, settings.Rta.TiltDbPerOctave);
        Assert.Equal(500, settings.Rta.ReleaseDbPerSecond);
        Assert.Equal(3, settings.Rta.TargetRangeDb);
        Assert.True(settings.Rta.ShowPeakHoldCaps);
        Assert.Equal(1000, settings.Rta.PeakHoldMs);
        Assert.Equal("#C9A528", settings.Rta.BarColor);
        Assert.Equal("#FFF6D6", settings.Rta.PeakCapColor);
        Assert.Equal("#5C1212", settings.Rta.TargetBandColor);
    }

    [Fact]
    public void InvalidDynamicsRangesAndAdditionalEnumsAreRejected()
    {
        var settings = MeasurementSettings.Default;
        AssertInvalid(settings with { Meters = settings.Meters with { AttackMs = -1 } });
        AssertInvalid(settings with { Meters = settings.Meters with { ReleaseMs = double.PositiveInfinity } });
        AssertInvalid(settings with { Meters = settings.Meters with { PeakHoldMs = double.NaN } });
        AssertInvalid(settings with { Meters = settings.Meters with { IntegratedWindowSeconds = 0 } });
        AssertInvalid(settings with { Meters = settings.Meters with { FontSize = (MeterFontSize)999 } });
        AssertInvalid(settings with { Meters = settings.Meters with { LufsScale = (LufsScalePreset)999 } });
        AssertInvalid(settings with { Meters = settings.Meters with { LufsMetric = (LoudnessMetric)999 } });
        AssertInvalid(settings with { Meters = settings.Meters with { LufsScaleBottom = 0 } });
        AssertInvalid(settings with { Rta = settings.Rta with { AveragingCount = 0 } });
        AssertInvalid(settings with { Rta = settings.Rta with { AveragingCount = 1001 } });
        AssertInvalid(settings with { Rta = settings.Rta with { ReleaseDbPerSecond = -1 } });
        AssertInvalid(settings with { Rta = settings.Rta with { TargetRangeDb = -1 } });
        AssertInvalid(settings with { Rta = settings.Rta with { TiltDbPerOctave = double.NaN } });
        AssertInvalid(settings with { Analyzer = settings.Analyzer with { AmplitudeScale = (AnalyzerAmplitudeScale)999 } });
    }

    [Theory]
    [InlineData(15, true)]
    [InlineData(43200, true)]
    [InlineData(14, false)]
    [InlineData(43201, false)]
    public void LoudnessHistoryUsesReferenceBounds(int seconds, bool valid)
    {
        Assert.Equal(valid, MeasurementSettingsValidator.IsValid(MeasurementSettings.Default with { Loudness = MeasurementSettings.Default.Loudness with { HistorySeconds = seconds } }));
    }

    [Fact]
    public void CustomLufsScaleRequiresOrderedFiniteRangeAndPositiveStep()
    {
        var settings = MeasurementSettings.Default;
        var custom = settings with { Meters = settings.Meters with { LufsScale = LufsScalePreset.Custom, LufsScaleTop = -6, LufsScaleBottom = -48, LufsScaleStep = 6 } };
        Assert.True(MeasurementSettingsValidator.IsValid(custom));
        AssertInvalid(custom with { Meters = custom.Meters with { LufsScaleBottom = -5 } });
        AssertInvalid(custom with { Meters = custom.Meters with { LufsScaleStep = 0 } });
        AssertInvalid(custom with { Meters = custom.Meters with { LufsScaleTop = double.NaN } });
    }
}
