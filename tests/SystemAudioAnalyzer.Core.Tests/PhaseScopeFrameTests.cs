namespace SystemAudioAnalyzer.Core.Tests;

public sealed class PhaseScopeFrameTests
{
    [Fact]
    public void PhaseFrameIsBoundedAndPreservesStereoPairs()
    {
        var frame = PhaseScopeFrame.FromInterleaved([0.1f, -0.1f, 0.2f, -0.2f, 0.3f, -0.3f], channels: 2, maximumPoints: 2);

        Assert.Equal(2, frame.Points.Count);
        Assert.Equal((0.2f, -0.2f), frame.Points[0]);
        Assert.Equal((0.3f, -0.3f), frame.Points[1]);
    }
}
