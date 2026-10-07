using SystemAudioAnalyzer.App.Rendering;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class FrequencyScaleTests
{
    [Theory]
    [InlineData(999, "999 Hz")]
    [InlineData(1_000, "1.000 kHz")]
    [InlineData(10_000, "10.0 kHz")]
    public void FrequencyScaleFormatsCursorLabels(double hertz, string expected)
    {
        Assert.Equal(expected, FrequencyScale.Format(hertz));
    }

    [Fact]
    public void FrequencyScaleRoundTripsLogarithmicPosition()
    {
        var normalized = FrequencyScale.ToNormalized(1_000);

        Assert.InRange(FrequencyScale.ToHertz(normalized), 999.9, 1_000.1);
    }
}
