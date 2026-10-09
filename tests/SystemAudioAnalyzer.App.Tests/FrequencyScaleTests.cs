using SystemAudioAnalyzer.App.Rendering;
using SystemAudioAnalyzer.App.Settings;

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
        var normalized = FrequencyScale.ToNormalized(1_000, AnalyzerFrequencyScale.Logarithmic, 20_000);

        Assert.InRange(FrequencyScale.ToHertz(normalized, AnalyzerFrequencyScale.Logarithmic, 20_000), 999.9, 1_000.1);
    }

    [Fact]
    public void LinearScaleMapsFrequencyEndpointsAndMidpointToSharedNormalizedCoordinates()
    {
        Assert.Equal(0, FrequencyScale.ToNormalized(20, AnalyzerFrequencyScale.Linear, 20_000), 6);
        Assert.Equal(0.5, FrequencyScale.ToNormalized(10_010, AnalyzerFrequencyScale.Linear, 20_000), 6);
        Assert.Equal(1, FrequencyScale.ToNormalized(20_000, AnalyzerFrequencyScale.Linear, 20_000), 6);
        Assert.Equal(10_010, FrequencyScale.ToHertz(0.5, AnalyzerFrequencyScale.Linear, 20_000), 6);
    }

    [Fact]
    public void LogarithmicScaleRetainsOctaveBasedMapping()
    {
        Assert.Equal(0.5, FrequencyScale.ToNormalized(632.455532, AnalyzerFrequencyScale.Logarithmic, 20_000), 5);
        Assert.Equal(632.455532, FrequencyScale.ToHertz(0.5, AnalyzerFrequencyScale.Logarithmic, 20_000), 5);
    }

    // Regression: typing into the Settings "max frequency" field fires on every keystroke, so a live render can
    // briefly see a maxHertz at, below, or even negative relative to MinimumHertz; none of these may throw.
    [Theory]
    [InlineData(0d)]
    [InlineData(-5d)]
    [InlineData(FrequencyScale.MinimumHertz)]
    [InlineData(2d)]
    public void OutOfRangeMaxHertzDuringEditingDoesNotThrow(double maxHertz)
    {
        Assert.InRange(FrequencyScale.ToNormalized(20_000, AnalyzerFrequencyScale.Logarithmic, maxHertz), 0d, 1d);
        Assert.InRange(FrequencyScale.ToNormalized(20_000, AnalyzerFrequencyScale.Linear, maxHertz), 0d, 1d);
        Assert.True(double.IsFinite(FrequencyScale.ToHertz(1d, AnalyzerFrequencyScale.Logarithmic, maxHertz)));
        Assert.True(double.IsFinite(FrequencyScale.ToHertz(1d, AnalyzerFrequencyScale.Linear, maxHertz)));
    }
}
