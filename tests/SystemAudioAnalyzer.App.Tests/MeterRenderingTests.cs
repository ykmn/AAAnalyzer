using SystemAudioAnalyzer.App.Rendering;
using SystemAudioAnalyzer.App.Settings;
using SystemAudioAnalyzer.Core;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class MeterRenderingTests
{
    private static readonly DateTimeOffset Origin = DateTimeOffset.Parse("2026-10-07T12:00:00Z");

    [Fact]
    public void TruePeakProducesNonZeroFillAndSilenceProducesZeroFill()
    {
        var settings = MeasurementSettings.Default.Meters;
        var history = new MeteringHistory();
        var level = history.Update(new ChannelLevel(0.5f, 0.25f), Origin, settings);

        Assert.True(MeterRailLayout.CalculateFillRatio(level.Peak, settings.DisplayRangeDb) > 0);
        var silence = history.Update(new ChannelLevel(0, 0), Origin.AddSeconds(1), settings with { ReleaseMs = 0 });
        Assert.Equal(0, MeterRailLayout.CalculateFillRatio(silence.Peak, settings.DisplayRangeDb));
    }

    [Fact]
    public void MeteringHistoryTracksPeakAndRmsIndependentlyAndExpiresPeakHold()
    {
        var settings = MeasurementSettings.Default.Meters with { AttackMs = 0, ReleaseMs = 0, PeakHoldMs = 100 };
        var history = new MeteringHistory();

        var loud = history.Update(new ChannelLevel(0.8f, 0.3f), Origin, settings);
        var quiet = history.Update(new ChannelLevel(0.1f, 0.05f), Origin.AddMilliseconds(50), settings);
        var expired = history.Update(new ChannelLevel(0.1f, 0.05f), Origin.AddMilliseconds(101), settings);

        Assert.Equal(0.8f, loud.Peak);
        Assert.Equal(0.3f, loud.Rms);
        Assert.Equal(0.8f, quiet.PeakHold);
        Assert.Equal(0.1f, expired.PeakHold);
    }

    [Fact]
    public void SeparateChannelHistoriesKeepIndependentPeakHolds()
    {
        var settings = MeasurementSettings.Default.Meters with { AttackMs = 0, ReleaseMs = 0, PeakHoldMs = 1_000 };
        var left = new MeteringHistory();
        var right = new MeteringHistory();
        left.Update(new ChannelLevel(0.8f, 0.2f), Origin, settings);
        right.Update(new ChannelLevel(0.3f, 0.1f), Origin, settings);

        var leftState = left.Update(new ChannelLevel(0.1f, 0.05f), Origin.AddMilliseconds(100), settings);
        var rightState = right.Update(new ChannelLevel(0.2f, 0.08f), Origin.AddMilliseconds(100), settings);

        Assert.Equal(0.8f, leftState.PeakHold);
        Assert.Equal(0.3f, rightState.PeakHold);
        Assert.Equal(0.1f, leftState.Peak);
        Assert.Equal(0.2f, rightState.Peak);
    }

    [Fact]
    public void MeteringHistoryAppliesAttackAndReleaseAndKeepsFiniteStateForInvalidValues()
    {
        var settings = MeasurementSettings.Default.Meters with { AttackMs = 100, ReleaseMs = 100 };
        var history = new MeteringHistory();
        history.Update(new ChannelLevel(0.1f, 0.1f), Origin, settings);

        var attack = history.Update(new ChannelLevel(0.9f, 0.9f), Origin.AddMilliseconds(10), settings);
        var release = history.Update(new ChannelLevel(0.1f, 0.1f), Origin.AddMilliseconds(20), settings);
        var invalid = history.Update(new ChannelLevel(float.NaN, float.PositiveInfinity), Origin.AddMilliseconds(5), settings);

        Assert.InRange(attack.Peak, 0.1f, 0.9f);
        Assert.True(attack.Peak > 0.1f && attack.Peak < 0.9f);
        Assert.True(release.Peak < attack.Peak && release.Peak > 0.1f);
        Assert.True(float.IsFinite(invalid.Peak));
        Assert.True(float.IsFinite(invalid.Rms));
    }

    [Fact]
    public void MeterRailLayoutUsesConfiguredDbRangeForFill()
    {
        Assert.Equal(0, MeterRailLayout.CalculateFillRatio(0, -60));
        Assert.Equal(1, MeterRailLayout.CalculateFillRatio(1, -60));
        Assert.InRange(MeterRailLayout.CalculateFillRatio(0.5f, -60), 0.89, 0.91);
        Assert.Equal(0, MeterRailLayout.CalculateFillRatio(float.NaN, -60));
    }

    [Theory]
    [InlineData(LoudnessMetric.Momentary, -18)]
    [InlineData(LoudnessMetric.ShortTerm, -20)]
    [InlineData(LoudnessMetric.Integrated, -22)]
    public void MeterUsesTheSelectedLufsMetric(LoudnessMetric metric, float expected)
    {
        var measurement = new LoudnessMeasurement(-18, -20, -22);

        Assert.Equal(expected, MeterRailLayout.SelectLoudness(measurement, metric));
    }

    [Fact]
    public void LufsScaleUsesConfiguredRangeAndStep()
    {
        var meters = MeasurementSettings.Default.Meters;

        Assert.Equal(Enumerable.Range(0, 13).Select(index => -36d + (index * 3d)), MeterRailLayout.CreateLufsScaleTicks(meters));
        Assert.Equal(0.5d, MeterRailLayout.CalculateLufsRatio(-18, MeterRailLayout.ResolveLufsScale(meters)));
        Assert.Equal(0d, MeterRailLayout.CalculateLufsRatio(double.NaN, MeterRailLayout.ResolveLufsScale(meters)));
    }

    [Fact]
    public void LufsScalePresetChangesTheRuntimeRangeAndCustomUsesSavedValues()
    {
        var meters = MeasurementSettings.Default.Meters;

        Assert.Equal(new LufsScaleRange(0, -36, 3), MeterRailLayout.ResolveLufsScale(meters));
        Assert.Equal(new LufsScaleRange(0, -60, 6), MeterRailLayout.ResolveLufsScale(meters with { LufsScale = LufsScalePreset.FullScale }));
        Assert.Equal(new LufsScaleRange(-6, -48, 6), MeterRailLayout.ResolveLufsScale(meters with { LufsScale = LufsScalePreset.Custom, LufsScaleTop = -6, LufsScaleBottom = -48, LufsScaleStep = 6 }));
    }
}
