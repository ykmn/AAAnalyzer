namespace SystemAudioAnalyzer.Core.Tests;

public sealed class LevelMeterTests
{
    [Fact]
    public void ProcessCalculatesPeakAndRmsForEachChannel()
    {
        var meter = new LevelMeter();
        var samples = new[] { 0.5f, -0.25f, -0.5f, 0.25f };

        var levels = meter.Process(samples, channels: 2);

        Assert.Collection(
            levels,
            left =>
            {
                Assert.Equal(0.5f, left.Peak);
                Assert.Equal(0.5f, left.Rms, precision: 5);
            },
            right =>
            {
                Assert.Equal(0.25f, right.Peak);
                Assert.Equal(0.25f, right.Rms, precision: 5);
            });
    }

    [Fact]
    public void ProcessIgnoresAnIncompleteFinalAudioFrame()
    {
        var meter = new LevelMeter();
        var samples = new[] { 0.5f, 0.25f, 1.0f };

        var levels = meter.Process(samples, channels: 2);

        Assert.Equal(0.5f, levels[0].Peak);
        Assert.Equal(0.25f, levels[1].Peak);
    }
}
