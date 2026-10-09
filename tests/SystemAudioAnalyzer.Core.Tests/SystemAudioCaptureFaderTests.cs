using SystemAudioAnalyzer.Core;
using Xunit;

namespace SystemAudioAnalyzer.Core.Tests;

public sealed class SystemAudioCaptureFaderTests
{
    private sealed class FakeVolumeReader : IEndpointVolumeReader
    {
        public float Scalar { get; set; } = 1f;
        public bool IsMuted { get; set; }
    }

    [Fact]
    public void PreFaderLeavesSamplesUnchanged()
    {
        var samples = new[] { 0.5f, -0.25f, 1f };
        var reader = new FakeVolumeReader { Scalar = 0.1f, IsMuted = true };

        var result = SystemAudioCapture.ApplyFaderGain(samples, FaderMode.PreFader, reader);

        Assert.Equal(new[] { 0.5f, -0.25f, 1f }, result);
    }

    [Fact]
    public void PostFaderMultipliesByVolumeScalar()
    {
        var samples = new[] { 0.5f, -0.25f, 1f };
        var reader = new FakeVolumeReader { Scalar = 0.5f, IsMuted = false };

        var result = SystemAudioCapture.ApplyFaderGain(samples, FaderMode.PostFader, reader);

        Assert.Equal(new[] { 0.25f, -0.125f, 0.5f }, result);
    }

    [Fact]
    public void PostFaderZeroesSamplesWhenMuted()
    {
        var samples = new[] { 0.5f, -0.25f, 1f };
        var reader = new FakeVolumeReader { Scalar = 0.9f, IsMuted = true };

        var result = SystemAudioCapture.ApplyFaderGain(samples, FaderMode.PostFader, reader);

        Assert.Equal(new[] { 0f, 0f, 0f }, result);
    }
}
