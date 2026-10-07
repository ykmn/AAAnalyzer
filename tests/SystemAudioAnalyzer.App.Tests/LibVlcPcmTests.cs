using System.Runtime.InteropServices;
using SystemAudioAnalyzer.App.Sources;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class LibVlcPcmTests
{
    [Fact]
    public void SixteenBitSamplesScaleToUnitFloats()
    {
        var converted = LibVlcPcm.ToFloat([short.MinValue, -16384, 0, 16384, short.MaxValue]);

        Assert.Equal(-1f, converted[0]);
        Assert.Equal(-0.5f, converted[1]);
        Assert.Equal(0f, converted[2]);
        Assert.Equal(0.5f, converted[3]);
        Assert.InRange(converted[4], 0.9999f, 1f);
    }

    [Fact]
    public void NativeBufferIsReadAsInterleavedSixteenBitFramesWithoutOverrunning()
    {
        short[] source = [100, -100, 200, -200, 300, -300];
        var buffer = Marshal.AllocHGlobal(source.Length * sizeof(short));
        try
        {
            Marshal.Copy(source, 0, buffer, source.Length);

            var pcm = LibVlcPcm.ReadInterleavedS16(buffer, frames: 3, channels: 2);

            Assert.Equal(6, pcm.Length);
            Assert.Equal(100 / 32768f, pcm[0]);
            Assert.Equal(-300 / 32768f, pcm[5]);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Fact]
    public void EmptyAndInvalidRequestsAreHandled()
    {
        Assert.Empty(LibVlcPcm.ReadInterleavedS16(IntPtr.Zero, 0, 2));
        Assert.Throws<ArgumentException>(() => LibVlcPcm.ReadInterleavedS16(IntPtr.Zero, 4, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => LibVlcPcm.ReadInterleavedS16(IntPtr.Zero, -1, 2));
    }
}
