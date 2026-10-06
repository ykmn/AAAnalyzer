using System.Buffers.Binary;
using NAudio.Wave;

namespace SystemAudioAnalyzer.Core;

public static class PcmSampleConverter
{
    public static float[] Convert(ReadOnlySpan<byte> bytes, WaveFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        var bytesPerSample = format.BitsPerSample / 8;
        if (bytesPerSample is not 2 and not 3 and not 4)
        {
            throw new NotSupportedException($"{format.BitsPerSample}-bit audio is not supported.");
        }

        var sampleCount = bytes.Length / bytesPerSample;
        var samples = new float[sampleCount];
        for (var index = 0; index < sampleCount; index++)
        {
            var offset = index * bytesPerSample;
            samples[index] = format.Encoding switch
            {
                WaveFormatEncoding.IeeeFloat when bytesPerSample == 4 => BitConverter.ToSingle(bytes.Slice(offset, 4)),
                WaveFormatEncoding.Pcm when bytesPerSample == 2 => BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(offset, 2)) / 32768f,
                WaveFormatEncoding.Pcm when bytesPerSample == 3 => ReadInt24LittleEndian(bytes.Slice(offset, 3)) / 8_388_608f,
                WaveFormatEncoding.Pcm when bytesPerSample == 4 => BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)) / 2_147_483_648f,
                _ => throw new NotSupportedException($"Audio format {format.Encoding} with {format.BitsPerSample} bits is not supported."),
            };
        }

        return samples;
    }

    private static int ReadInt24LittleEndian(ReadOnlySpan<byte> value)
    {
        var result = value[0] | (value[1] << 8) | (value[2] << 16);
        return (result & 0x80_0000) != 0 ? result | unchecked((int)0xFF00_0000) : result;
    }
}
