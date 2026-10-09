using SystemAudioAnalyzer.App.Rendering;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class AxisTicksTests
{
    [Fact]
    public void PeakRailDbTicksAreMultiplesOfThreeForSixtyDbRange()
    {
        var ticks = AxisTicks.PeakRailDb(60);

        Assert.Equal(19, ticks.Count);
        Assert.Equal("-3", ticks[0].Label);
        Assert.Equal("-57", ticks[^1].Label);
        Assert.Equal(57d / 60d, ticks[0].Ratio, 6);
    }

    [Fact]
    public void LufsLabelsMarkEveryMultipleOfThreeIncludingTheEdges()
    {
        var ticks = AxisTicks.LufsLabels(new LufsScaleRange(0, -36, 3));

        Assert.Equal(13, ticks.Count);
        Assert.Equal("-36", ticks[0].Label);
        Assert.Equal("0", ticks[^1].Label);
        Assert.Equal(0d, ticks[0].Ratio, 6);
        Assert.Equal(1d, ticks[^1].Ratio, 6);
    }

    [Fact]
    public void LufsLabelsSnapToMultiplesOfThreeInsideAnOddRange()
    {
        var ticks = AxisTicks.LufsLabels(new LufsScaleRange(-5, -17, 3));

        Assert.Equal(["-15", "-12", "-9", "-6"], ticks.Select(tick => tick.Label));
    }

    [Fact]
    public void RtaDbTicksCoverTopToFloorEveryTenDb()
    {
        var ticks = AxisTicks.RtaDb(0, 60, 10);

        Assert.Equal(["0", "-10", "-20", "-30", "-40", "-50", "-60"], ticks.Select(tick => tick.Label));
        Assert.Equal(1d, ticks[0].Ratio, 6);
        Assert.Equal(0d, ticks[^1].Ratio, 6);
    }

    [Theory]
    [InlineData(20, "20")]
    [InlineData(900, "900")]
    [InlineData(1300, "1.3k")]
    [InlineData(5000, "5.0k")]
    [InlineData(14000, "14k")]
    [InlineData(20000, "20k")]
    public void CompactFrequencyLabelsUseKiloSuffix(double hertz, string expected)
    {
        Assert.Equal(expected, AxisTicks.FormatHertz(hertz));
    }

    [Fact]
    public void WaterfallFrequencyLabelsAreDenserThanOnePerOctave()
    {
        var labels = AxisTicks.WaterfallFrequencyLabels();

        Assert.True(labels.Count >= 20);
        Assert.Equal(20, labels[0].Hertz);
        Assert.Equal(24_000, labels[^1].Hertz);
    }

    [Fact]
    public void RtaFrequencyLabelsStartAtTwentyAndEndAtTwentyKilohertz()
    {
        var labels = AxisTicks.RtaFrequencyLabels();

        Assert.Equal(20, labels[0].Hertz);
        Assert.Equal(20_000, labels[^1].Hertz);
    }

    [Theory]
    [InlineData(6, 1)]
    [InlineData(12, 2)]
    [InlineData(40, 5)]
    [InlineData(100, 10)]
    public void LoudnessYStepGrowsWithSpan(double span, double expected)
    {
        Assert.Equal(expected, AxisTicks.LoudnessYStep(span));
    }

    [Theory]
    [InlineData(60, 10)]
    [InlineData(3_600, 600)]
    [InlineData(43_200, 7_200)]
    public void LoudnessTimeStepKeepsAtMostEightIntervals(int visibleSeconds, int expected)
    {
        Assert.Equal(expected, AxisTicks.LoudnessTimeStep(visibleSeconds));
    }

    [Fact]
    public void TimeTicksAlignToWallClockInTheOffsetOfNow()
    {
        var now = new DateTimeOffset(2026, 10, 7, 16, 16, 32, TimeSpan.FromHours(5));

        var ticks = AxisTicks.TimeTicks(now, TimeSpan.FromSeconds(60), 10);

        Assert.Equal(6, ticks.Count);
        Assert.Equal("16:16:30", ticks[0].Label);
        Assert.Equal(2d, ticks[0].SecondsAgo, 3);
        Assert.Equal("16:15:40", ticks[^1].Label);
    }

    [Theory]
    [InlineData(0, 4, 0)]
    [InlineData(3.9, 4, 0)]
    [InlineData(4, 4, 1)]
    [InlineData(17, 4, 4)]
    public void RtaSegmentsCountWholeSegmentsOnly(double height, double pitch, int expected)
    {
        Assert.Equal(expected, RtaSegments.Count(height, pitch));
    }
}
