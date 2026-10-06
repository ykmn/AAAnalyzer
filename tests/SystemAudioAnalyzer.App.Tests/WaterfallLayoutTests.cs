using SystemAudioAnalyzer.App.Rendering;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class WaterfallLayoutTests
{
    [Theory]
    [InlineData(1000, 400)]
    [InlineData(420, 300)]
    public void WaterfallLayoutCreatesEqualWidthStereoRegions(double width, double height)
    {
        var layout = WaterfallLayout.Calculate(width, height);

        Assert.Equal(width / 2, layout.LeftBounds.Width);
        Assert.Equal(layout.LeftBounds.Width, layout.RightBounds.Width);
        Assert.Equal(0, layout.LeftBounds.X);
        Assert.Equal(layout.LeftBounds.Right, layout.RightBounds.X);
        Assert.Equal(width, layout.RightBounds.Right);
        Assert.Equal(height, layout.LeftBounds.Height);
    }

    [Fact]
    public void WaterfallLayoutRejectsNonPositiveViewportDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WaterfallLayout.Calculate(0, 200));
        Assert.Throws<ArgumentOutOfRangeException>(() => WaterfallLayout.Calculate(200, -1));
    }
}
