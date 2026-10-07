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
}
