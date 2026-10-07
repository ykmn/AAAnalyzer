using SystemAudioAnalyzer.App.Rendering;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class MeterRailLayoutTests
{
    [Fact]
    public void MeterRailReservesMaximumCurrentAndOverloadRowsBeforeBars()
    {
        var layout = MeterRailLayout.Calculate(260, 320);

        Assert.True(layout.LeftMaximum.Bottom < layout.LeftOverload.Top);
        Assert.True(layout.LeftOverload.Bottom < layout.LeftMeter.Top);
        Assert.True(layout.RightMaximum.Bottom < layout.RightOverload.Top);
        Assert.True(layout.RightOverload.Bottom < layout.RightMeter.Top);
        Assert.Equal(layout.LeftMaximum.Width, layout.RightMaximum.Width);
    }

    [Fact]
    public void PeakRailAtWorkspaceWidthFitsScaleBarsLufsColumnAndLabels()
    {
        var layout = MeterRailLayout.Calculate(WorkspaceLayout.PeakRailWidth, 400);

        Assert.True(layout.DbScale.Right <= layout.LeftMeter.Left);
        Assert.True(layout.LeftMeter.Right <= layout.RightMeter.Left);
        Assert.True(layout.RightMeter.Right <= layout.LufsMeter.Left);
        Assert.True(layout.LufsMeter.Right <= layout.LufsScale.Left);
        Assert.True(layout.LufsScale.Right <= WorkspaceLayout.PeakRailWidth);
        Assert.True(layout.LeftMeter.Width >= 24);
    }

    [Fact]
    public void PeakRailStacksMetersChannelLabelsAndReadoutVertically()
    {
        var layout = MeterRailLayout.Calculate(WorkspaceLayout.PeakRailWidth, 400);

        Assert.True(layout.LeftMeter.Bottom <= layout.ChannelLabels.Top);
        Assert.True(layout.ChannelLabels.Bottom <= layout.LufsReadout.Top);
        Assert.True(layout.LufsReadout.Bottom <= layout.LufsCaption.Top);
        Assert.True(layout.LufsCaption.Bottom <= 400);
    }

    [Fact]
    public void PeakRailSurvivesVeryShortHeights()
    {
        var layout = MeterRailLayout.Calculate(WorkspaceLayout.PeakRailWidth, 20);

        Assert.True(layout.LeftMeter.Height >= 1);
    }
}
