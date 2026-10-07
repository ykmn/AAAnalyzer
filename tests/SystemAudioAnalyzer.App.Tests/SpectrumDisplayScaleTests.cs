using SystemAudioAnalyzer.App.Rendering;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class SpectrumDisplayScaleTests
{
    [Theory]
    [InlineData(-120, -110, -110)]
    [InlineData(-80, -110, -80)]
    public void GlobalAnalyzerFloorAndInstrumentFloorBothLimitTheVisibleRange(double analyzerFloor, double instrumentFloor, double expectedFloor)
    {
        Assert.Equal(expectedFloor, SpectrumDisplayScale.EffectiveFloor(analyzerFloor, instrumentFloor));
    }

    [Fact]
    public void AnalyzerAmplitudeScaleAppliesGainInLinearAndLogarithmicModes()
    {
        Assert.Equal(1, SpectrumDisplayScale.ToNormalizedAmplitude(0.5, 2, AnalyzerAmplitudeScale.Linear, -60), 6);
        Assert.Equal(0.899657, SpectrumDisplayScale.ToNormalizedAmplitude(0.5, 1, AnalyzerAmplitudeScale.Logarithmic, -60), 6);
    }
}
