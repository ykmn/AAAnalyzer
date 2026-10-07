namespace SystemAudioAnalyzer.Core.Tests;

public sealed class LoudnessMeterTests
{
    [Fact]
    public void SilenceProducesNoFiniteLoudnessValues()
    {
        var meter = new LoudnessMeter();

        var measurement = meter.Process(new float[48_000], new AudioFormat(48_000, 1));

        Assert.Null(measurement.MomentaryLufs);
        Assert.Null(measurement.ShortTermLufs);
        Assert.Null(measurement.IntegratedLufs);
    }

    [Fact]
    public void ShortTermValueAppearsOnlyAfterThreeSeconds()
    {
        var meter = new LoudnessMeter();
        var format = new AudioFormat(48_000, 1);
        var signal = Enumerable.Repeat(0.1f, 48_000).ToArray();

        meter.Process(signal, format);
        meter.Process(signal, format);
        var beforeThreeSeconds = meter.Process(signal.AsSpan(0, 47_999), format);
        var atThreeSeconds = meter.Process(signal.AsSpan(47_999), format);

        Assert.Null(beforeThreeSeconds.ShortTermLufs);
        Assert.NotNull(atThreeSeconds.ShortTermLufs);
    }

    [Fact]
    public void MomentaryLoudnessUsesMeanWindowEnergy()
    {
        var meter = new LoudnessMeter();
        var format = new AudioFormat(48_000, 1);
        var signal = CreateSineWave(1_000, format.SampleRate, 19_200, amplitude: 0.1f);

        var measurement = meter.Process(signal, format);

        Assert.InRange(Assert.IsType<float>(measurement.MomentaryLufs), -26f, -20f);
    }

    [Fact]
    public void IntegratedLoudnessUsesConfiguredRollingWindow()
    {
        var meter = new LoudnessMeter(integratedWindowSeconds: 2);
        var format = new AudioFormat(48_000, 1);
        var loud = CreateSineWave(1_000, format.SampleRate, format.SampleRate, amplitude: 0.5f);
        var quiet = CreateSineWave(1_000, format.SampleRate, format.SampleRate, amplitude: 0.1f);

        meter.Process(loud, format);
        var mixed = meter.Process(quiet, format);
        var afterOldBlockExpires = meter.Process(quiet, format);

        Assert.NotNull(mixed.IntegratedLufs);
        Assert.NotNull(afterOldBlockExpires.IntegratedLufs);
        Assert.True(afterOldBlockExpires.IntegratedLufs < mixed.IntegratedLufs);
        Assert.InRange(meter.BufferedIntegratedBlockCount, 1, 20);
    }

    [Fact]
    public void ChangingIntegratedWindowTrimsAndResizesTheRollingBuffer()
    {
        var meter = new LoudnessMeter(integratedWindowSeconds: 1);
        var format = new AudioFormat(1_000, 1);
        var signal = Enumerable.Repeat(0.1f, 2_000).ToArray();

        meter.Process(signal, format);
        Assert.Equal(10, meter.BufferedIntegratedBlockCount);

        meter.SetIntegratedWindowSeconds(2);
        meter.Process(signal.AsSpan(0, 1_000), format);
        Assert.Equal(20, meter.BufferedIntegratedBlockCount);

        meter.SetIntegratedWindowSeconds(1);
        Assert.Equal(10, meter.BufferedIntegratedBlockCount);
    }

    [Fact]
    public void IntegratedWindowApiAcceptsMaximumAndRejectsValuesAboveIt()
    {
        var meter = new LoudnessMeter(integratedWindowSeconds: 3_600);

        Assert.Equal(3_600, meter.IntegratedWindowSeconds);
        meter.SetIntegratedWindowSeconds(3_600);
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoudnessMeter(integratedWindowSeconds: 3_601));
        Assert.Throws<ArgumentOutOfRangeException>(() => meter.SetIntegratedWindowSeconds(3_601));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoudnessMeter(integratedWindowSeconds: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => meter.SetIntegratedWindowSeconds(0));
        Assert.Equal(3_600, meter.IntegratedWindowSeconds);
    }

    private static float[] CreateSineWave(int frequencyHz, int sampleRate, int frames, float amplitude)
    {
        var samples = new float[frames];
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = amplitude * MathF.Sin((2f * MathF.PI * frequencyHz * index) / sampleRate);
        }

        return samples;
    }
}
