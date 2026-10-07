using SystemAudioAnalyzer.App.Rendering;
using SystemAudioAnalyzer.App.ViewModels;
using SystemAudioAnalyzer.Core;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class RtaRenderingTests
{
    [Theory]
    [InlineData(RtaResolution.OneThird, 3)]
    [InlineData(RtaResolution.OneTwelfth, 12)]
    public void RtaAggregatorCreatesRequestedBandsPerOctave(RtaResolution resolution, int bandsPerOctave)
    {
        var spectrum = new Spectrum(48_000, 4_096, Enumerable.Repeat(0.5f, 2_049));

        var bands = RtaBandAggregator.Aggregate(spectrum, resolution);

        Assert.Equal(bandsPerOctave, bands.Count(band => band.CenterHz >= 1_000 && band.CenterHz < 2_000));
    }

    [Fact]
    public void PhaseRotationMapsMonoToVerticalAndAntiphaseToHorizontal()
    {
        var mono = PhaseScopeTransform.Transform(0.5f, 0.5f, 1);
        var antiPhase = PhaseScopeTransform.Transform(0.5f, -0.5f, 1);

        Assert.InRange(Math.Abs(mono.X), 0, 0.0001);
        Assert.InRange(Math.Abs(antiPhase.Y), 0, 0.0001);
        Assert.True(Math.Abs(mono.Y) > 0.1);
        Assert.True(Math.Abs(antiPhase.X) > 0.1);
    }

    [Fact]
    public void CursorPositionUsesEqualRelativeCoordinatesInBothStereoPanels()
    {
        var layout = WaterfallLayout.Calculate(800, 300);
        var normalized = FrequencyScale.ToNormalized(1_000);

        var leftX = layout.LeftBounds.Left + (layout.LeftBounds.Width * normalized);
        var rightX = layout.RightBounds.Left + (layout.RightBounds.Width * normalized);

        Assert.Equal(normalized, (leftX - layout.LeftBounds.Left) / layout.LeftBounds.Width, 6);
        Assert.Equal(normalized, (rightX - layout.RightBounds.Left) / layout.RightBounds.Width, 6);
    }
}
