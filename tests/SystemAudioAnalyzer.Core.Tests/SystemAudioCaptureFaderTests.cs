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
    public void PostFaderLeavesSamplesUnchanged()
    {
        var samples = new[] { 0.5f, -0.25f, 1f };
        var reader = new FakeVolumeReader { Scalar = 0.1f, IsMuted = true };

        var result = SystemAudioCapture.ApplyFaderGain(samples, FaderMode.PostFader, reader);

        Assert.Equal(new[] { 0.5f, -0.25f, 1f }, result);
    }

    [Fact]
    public void PreFaderDividesOutTheCurrentGain()
    {
        var samples = new[] { 0.25f, -0.125f, 0.5f };
        var reader = new FakeVolumeReader { Scalar = 0.5f, IsMuted = false };

        var result = SystemAudioCapture.ApplyFaderGain(samples, FaderMode.PreFader, reader);

        Assert.Equal(new[] { 0.5f, -0.25f, 1f }, result);
    }

    [Fact]
    public void PreFaderLeavesSamplesUnchangedWhenMuted()
    {
        // Windows has already discarded the signal before the loopback tap; there is nothing to recover.
        var samples = new[] { 0f, 0f, 0f };
        var reader = new FakeVolumeReader { Scalar = 0.9f, IsMuted = true };

        var result = SystemAudioCapture.ApplyFaderGain(samples, FaderMode.PreFader, reader);

        Assert.Equal(new[] { 0f, 0f, 0f }, result);
    }

    [Fact]
    public void PreFaderLeavesSamplesUnchangedAtZeroGain()
    {
        var samples = new[] { 0.5f, -0.25f, 1f };
        var reader = new FakeVolumeReader { Scalar = 0f, IsMuted = false };

        var result = SystemAudioCapture.ApplyFaderGain(samples, FaderMode.PreFader, reader);

        Assert.Equal(new[] { 0.5f, -0.25f, 1f }, result);
    }
}
