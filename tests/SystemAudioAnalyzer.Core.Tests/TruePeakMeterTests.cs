namespace SystemAudioAnalyzer.Core.Tests;

public sealed class TruePeakMeterTests
{
    [Fact]
    public void FourTimesOversamplingFindsAnInterSamplePeak()
    {
        var meter = new TruePeakMeter();
        var samples = CreateSineWave(frequencyHz: 1_000, sampleRate: 44_100, frames: 441, phaseRadians: 0.2f);
        var samplePeak = samples.Max(MathF.Abs);

        var measurement = meter.Process(samples, channels: 1);

        Assert.True(measurement.Current[0] > samplePeak);
        Assert.InRange(measurement.Current[0], 0.999f, 1.001f);
    }

    [Fact]
    public void OverloadLatchesOnlyForTheAffectedChannelUntilReset()
    {
        var meter = new TruePeakMeter();

        var measurement = meter.Process([1.1f, 0.5f, 0.25f, 0.5f], channels: 2);
        meter.Reset(channel: 0);
        var afterReset = meter.Process([0.4f, 0.4f], channels: 2);

        Assert.True(measurement.Overload[0]);
        Assert.False(measurement.Overload[1]);
        Assert.InRange(afterReset.Maximum[0], 0.39f, 0.41f);
        Assert.True(afterReset.Maximum[1] >= 0.5f);
    }

    [Fact]
    public void ResetBeforeTheFirstBufferDoesNotThrow()
    {
        var meter = new TruePeakMeter();

        var exception = Record.Exception(() => meter.Reset(channel: 0));

        Assert.Null(exception);
    }

    [Fact]
    public void ResetMaximumPreservesTheOverloadLatch()
    {
        var meter = new TruePeakMeter();
        meter.Process([1.1f, 0.2f], channels: 1);

        meter.ResetMaximum(channel: 0);
        var afterReset = meter.Process([0.3f], channels: 1);

        Assert.InRange(afterReset.Maximum[0], 0.29f, 0.31f);
        Assert.True(afterReset.Overload[0]);
    }

    [Fact]
    public void ResetOverloadPreservesTheMaximum()
    {
        var meter = new TruePeakMeter();
        meter.Process([1.1f, 0.2f], channels: 1);

        meter.ResetOverload(channel: 0);
        var afterReset = meter.Process([0.3f], channels: 1);

        Assert.True(afterReset.Maximum[0] >= 1.1f);
        Assert.False(afterReset.Overload[0]);
    }

    private static float[] CreateSineWave(int frequencyHz, int sampleRate, int frames, float phaseRadians)
    {
        var samples = new float[frames];
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = MathF.Sin(phaseRadians + ((2f * MathF.PI * frequencyHz * index) / sampleRate));
        }

        return samples;
    }
}
