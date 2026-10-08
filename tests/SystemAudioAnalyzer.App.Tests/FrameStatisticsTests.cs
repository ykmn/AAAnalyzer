using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class FrameStatisticsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FramesPerSecondCountsArrivalsInTheLastSecond()
    {
        var statistics = new FrameStatistics();

        for (var index = 0; index < 10; index++)
        {
            var now = Start.AddMilliseconds(index * 100);
            statistics.Record(now, now, 0, TimeSpan.Zero);
        }

        Assert.Equal(10, statistics.FramesPerSecond);

        var later = Start.AddSeconds(5);
        statistics.Record(later, later, 0, TimeSpan.Zero);
        Assert.Equal(1, statistics.FramesPerSecond);
    }

    [Fact]
    public void DelayStartsAtTheFirstSampleAndThenSmooths()
    {
        var statistics = new FrameStatistics();

        statistics.Record(Start, Start.AddMilliseconds(100), 0, TimeSpan.Zero);
        Assert.Equal(100, statistics.DelayMilliseconds, 6);

        statistics.Record(Start, Start.AddMilliseconds(200), 0, TimeSpan.Zero);
        Assert.InRange(statistics.DelayMilliseconds, 100, 200);
        Assert.NotEqual(200, statistics.DelayMilliseconds);
    }

    [Fact]
    public void DroppedBuffersAccumulateAndResetClearsEverything()
    {
        var statistics = new FrameStatistics();

        statistics.Record(Start, Start, 3, TimeSpan.FromMilliseconds(2));
        statistics.Record(Start, Start, 2, TimeSpan.FromMilliseconds(2));

        Assert.Equal(5, statistics.DroppedBuffers);
        Assert.Contains("dropped buffers 5", statistics.Text, StringComparison.Ordinal);

        statistics.Reset();

        Assert.Equal(0, statistics.DroppedBuffers);
        Assert.Equal(0, statistics.FramesPerSecond);
    }

    [Fact]
    public void FutureFrameTimestampsNeverProduceNegativeDelay()
    {
        var statistics = new FrameStatistics();

        statistics.Record(Start.AddSeconds(1), Start, 0, TimeSpan.Zero);

        Assert.Equal(0, statistics.DelayMilliseconds);
    }
}
