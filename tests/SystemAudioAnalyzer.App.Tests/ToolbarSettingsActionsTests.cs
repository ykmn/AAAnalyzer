using SystemAudioAnalyzer.App.ViewModels;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class ToolbarSettingsActionsTests
{
    private static readonly MeasurementSettings Defaults = MeasurementSettings.Default;

    [Fact]
    public void ScaleTextShowsManualRangeFromCentreAndSpan()
    {
        var manual = Defaults with { Loudness = Defaults.Loudness with { AutoScale = false } };

        Assert.Equal("-14..-8", ToolbarSettingsActions.LoudnessScaleText(manual));
        Assert.Equal("auto", ToolbarSettingsActions.LoudnessScaleText(Defaults));
    }

    [Fact]
    public void ZoomAndShiftSwitchToManualScale()
    {
        var zoomed = ToolbarSettingsActions.ZoomLoudness(Defaults, 2);
        var shifted = ToolbarSettingsActions.ShiftLoudness(Defaults, -2);

        Assert.False(zoomed.Loudness.AutoScale);
        Assert.Equal(12, zoomed.Loudness.SpanLufs);
        Assert.False(shifted.Loudness.AutoScale);
        Assert.Equal(-13, shifted.Loudness.CentreLufs);
    }

    [Fact]
    public void ZoomIsClampedToSupportedSpan()
    {
        Assert.Equal(60, ToolbarSettingsActions.ZoomLoudness(Defaults, 1_000).Loudness.SpanLufs);
        Assert.Equal(2, ToolbarSettingsActions.ZoomLoudness(Defaults, 0.0001).Loudness.SpanLufs);
    }

    [Fact]
    public void WindowAndMetricUpdateOnlyLoudnessSettings()
    {
        var updated = ToolbarSettingsActions.WithLoudnessMetric(ToolbarSettingsActions.WithLoudnessWindow(Defaults, 3_600), LoudnessMetric.Momentary);

        Assert.Equal(3_600, updated.Loudness.HistorySeconds);
        Assert.Equal(LoudnessMetric.Momentary, updated.Loudness.Metric);
        Assert.Equal(Defaults.Rta, updated.Rta);
    }

    [Theory]
    [InlineData(600, 1_800)]
    [InlineData(3_600, 60)]
    [InlineData(45, 60)]
    public void RollingCyclesIntegratedWindowAndSelectsIntegrated(int current, int expected)
    {
        var start = Defaults with { Meters = Defaults.Meters with { IntegratedWindowSeconds = current } };
        start = ToolbarSettingsActions.WithLoudnessMetric(start, LoudnessMetric.Momentary);

        var next = ToolbarSettingsActions.CycleRollingWindow(start);

        Assert.Equal(expected, next.Meters.IntegratedWindowSeconds);
        Assert.Equal(LoudnessMetric.Integrated, next.Loudness.Metric);
    }

    [Fact]
    public void RtaAveragingIsClampedToValidatorRange()
    {
        Assert.Equal(60, ToolbarSettingsActions.WithRtaAveraging(Defaults, 10).Rta.AveragingCount);
        Assert.Equal(1, ToolbarSettingsActions.WithRtaAveraging(Defaults, -500).Rta.AveragingCount);
        Assert.Equal(1_000, ToolbarSettingsActions.WithRtaAveraging(Defaults, 5_000).Rta.AveragingCount);
    }

    [Fact]
    public void RtaTargetStaysInsideTheScale()
    {
        Assert.Equal(-35, ToolbarSettingsActions.WithRtaTarget(Defaults, 1).Rta.TargetLineDb);
        Assert.Equal(0, ToolbarSettingsActions.WithRtaTarget(Defaults, 500).Rta.TargetLineDb);
        Assert.Equal(-60, ToolbarSettingsActions.WithRtaTarget(Defaults, -500).Rta.TargetLineDb);
    }

    [Fact]
    public void CopyToolbarFieldsTransfersOnlyTheFieldsOwnedByTheToolbars()
    {
        var runtime = Defaults with
        {
            Rta = Defaults.Rta with { Source = RtaChannelMode.Left, Resolution = RtaResolution.OneSixth, AveragingCount = 80, TargetLineDb = -30, TiltDbPerOctave = 3 },
            Loudness = Defaults.Loudness with { Metric = LoudnessMetric.Momentary, HistorySeconds = 600, AutoScale = false, SpanLufs = 12, CentreLufs = -20 },
            Meters = Defaults.Meters with { IntegratedWindowSeconds = 1_800, AttackMs = 99 },
            Analyzer = Defaults.Analyzer with { Gain = 2 },
        };

        var profile = ToolbarSettingsActions.CopyToolbarFields(Defaults, runtime);

        Assert.Equal(RtaChannelMode.Left, profile.Rta.Source);
        Assert.Equal(RtaResolution.OneSixth, profile.Rta.Resolution);
        Assert.Equal(80, profile.Rta.AveragingCount);
        Assert.Equal(-30, profile.Rta.TargetLineDb);
        Assert.Equal(LoudnessMetric.Momentary, profile.Loudness.Metric);
        Assert.Equal(600, profile.Loudness.HistorySeconds);
        Assert.False(profile.Loudness.AutoScale);
        Assert.Equal(12, profile.Loudness.SpanLufs);
        Assert.Equal(-20, profile.Loudness.CentreLufs);
        Assert.Equal(1_800, profile.Meters.IntegratedWindowSeconds);
        Assert.Equal(Defaults.Rta.TiltDbPerOctave, profile.Rta.TiltDbPerOctave);
        Assert.Equal(Defaults.Meters.AttackMs, profile.Meters.AttackMs);
        Assert.Equal(Defaults.Analyzer, profile.Analyzer);
    }
}
