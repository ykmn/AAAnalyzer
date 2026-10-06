using NAudio.Wave;

namespace SystemAudioAnalyzer.Core.Tests;

public sealed class PcmSampleConverterTests
{
    [Fact]
    public void ConvertNormalizesSigned16BitPcm()
    {
        var bytes = new byte[] { 0x00, 0x40, 0x00, 0xC0 };
        var format = new WaveFormat(48_000, bits: 16, channels: 1);

        var samples = PcmSampleConverter.Convert(bytes, format);

        Assert.Equal(new[] { 0.5f, -0.5f }, samples);
    }

    [Fact]
    public void ConvertPreserves32BitFloatSamples()
    {
        var bytes = BitConverter.GetBytes(-0.25f).Concat(BitConverter.GetBytes(0.75f)).ToArray();
        var format = WaveFormat.CreateIeeeFloatWaveFormat(48_000, channels: 2);

        var samples = PcmSampleConverter.Convert(bytes, format);

        Assert.Equal(new[] { -0.25f, 0.75f }, samples);
    }
}
