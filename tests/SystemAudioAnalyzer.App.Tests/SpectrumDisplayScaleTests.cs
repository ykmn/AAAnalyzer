using SystemAudioAnalyzer.App.Rendering;

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
}
