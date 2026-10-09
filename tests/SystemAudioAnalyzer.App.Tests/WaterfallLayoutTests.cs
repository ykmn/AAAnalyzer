using SystemAudioAnalyzer.App.Rendering;
using SystemAudioAnalyzer.App.Settings;
using System.Windows.Media;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class WaterfallLayoutTests
{
    [Theory]
    [InlineData(1000, 400)]
    [InlineData(420, 300)]
    public void WaterfallLayoutStacksLeftAboveRightWithAxisBelow(double width, double height)
    {
        var layout = WaterfallLayout.Calculate(width, height);

        Assert.Equal(width - WaterfallLayout.TimeAxisWidth, layout.LeftBounds.Width);
        Assert.Equal(width - WaterfallLayout.TimeAxisWidth, layout.RightBounds.Width);
        Assert.Equal(0, layout.LeftBounds.X);
        Assert.Equal(0, layout.RightBounds.X);
        Assert.Equal(layout.LeftBounds.Height, layout.RightBounds.Height);
        Assert.True(layout.LeftBounds.Bottom < layout.RightBounds.Top);
        Assert.Equal(layout.RightBounds.Bottom, layout.AxisBounds.Top, 6);
        Assert.Equal(height, layout.AxisBounds.Bottom, 6);
    }

    [Fact]
    public void WaterfallLayoutRejectsNonPositiveViewportDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WaterfallLayout.Calculate(0, 200));
        Assert.Throws<ArgumentOutOfRangeException>(() => WaterfallLayout.Calculate(200, -1));
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.8)]
    public void CursorPositionIsTheSameOnBothChannels(double normalized)
    {
        var layout = WaterfallLayout.Calculate(1000, 400);

        Assert.Equal(normalized, layout.GetNormalizedX(layout.LeftBounds.Width * normalized), 6);
        Assert.Equal(0, layout.GetNormalizedX(-50));
        Assert.Equal(1, layout.GetNormalizedX(5000));
    }

    [Fact]
    public void WaterfallGradientSamplingAppliesOffsetAndClampsAtConfiguredFloor()
    {
        var stops = new[] { new ColorStop(-110, "#000000"), new ColorStop(-80, "#0080C0"), new ColorStop(-55, "#00FF39") };

        var floor = WaterfallRenderer.SampleColor(-120, -100, 0, stops);
        var midpoint = WaterfallRenderer.SampleColor(-90, -100, 0, stops);
        var offset = WaterfallRenderer.SampleColor(-95, -100, 5, stops);

        Assert.Equal(Color.FromRgb(0, 43, 64), floor);
        Assert.Equal(Color.FromRgb(0, 85, 128), midpoint);
        Assert.Equal(midpoint, offset);
    }
}
