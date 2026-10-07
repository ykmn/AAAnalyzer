using System.Runtime.InteropServices;

namespace SystemAudioAnalyzer.App.Sources;

/// <summary>
/// Reads the audio LibVLC hands to the play callback. The amem output only honours S16N, so the data is 16-bit
/// signed PCM (two bytes per sample); reading it as 32-bit floats yields garbage and runs past the native buffer.
/// </summary>
public static class LibVlcPcm
{
    public static float[] ReadInterleavedS16(IntPtr samples, int frames, int channels)
    {
        if (frames < 0) throw new ArgumentOutOfRangeException(nameof(frames));
        if (channels < 1) throw new ArgumentOutOfRangeException(nameof(channels));
        var raw = new short[checked(frames * channels)];
        if (raw.Length > 0)
        {
            if (samples == IntPtr.Zero) throw new ArgumentException("Sample buffer is null.", nameof(samples));
            Marshal.Copy(samples, raw, 0, raw.Length);
        }

        return ToFloat(raw);
    }

    public static float[] ToFloat(ReadOnlySpan<short> raw)
    {
        var converted = new float[raw.Length];
        for (var index = 0; index < raw.Length; index++)
        {
            converted[index] = raw[index] / 32768f;
        }

        return converted;
    }
}
