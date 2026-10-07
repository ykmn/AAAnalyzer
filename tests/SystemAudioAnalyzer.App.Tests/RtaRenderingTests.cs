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

    [Theory]
    [InlineData(RtaResolution.One)]
    [InlineData(RtaResolution.OneThird)]
    [InlineData(RtaResolution.OneTwelfth)]
    public void EveryBandShowsALevelEvenWhenNarrowerThanOneFftBin(RtaResolution resolution)
    {
        // 48 kHz / 2048 gives ~23 Hz bins, wider than the low-frequency 1/12-octave bands.
        var spectrum = new Spectrum(48_000, 2_048, Enumerable.Repeat(0.5f, 1_025));

        var bands = RtaBandAggregator.Aggregate(spectrum, resolution);

        Assert.All(bands, band => Assert.Equal(0.5f, band.Magnitude, 4));
    }

    [Fact]
    public void BandsWithoutABinInterpolateBetweenNeighbouringBins()
    {
        var magnitudes = Enumerable.Range(0, 1_025).Select(bin => bin * 0.001f).ToArray();
        var spectrum = new Spectrum(48_000, 2_048, magnitudes);

        var band = RtaBandAggregator.Aggregate(spectrum, RtaResolution.OneTwelfth)
            .First(candidate => Math.Abs(candidate.CenterHz - 40) < 1.5);

        var binPosition = band.CenterHz / (48_000d / 2_048);
        Assert.Equal((float)(binPosition * 0.001), band.Magnitude, 5);
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

    [Fact]
    public void PhaseScopeViewportRemainsSquareInsideWidePanel()
    {
        var viewport = PhaseScopeTransform.CalculateViewport(900, 400);

        Assert.Equal(viewport.Width, viewport.Height);
        Assert.Equal(250, viewport.X);
        Assert.Equal(0, viewport.Y);
    }

    [Fact]
    public void PhaseGainKeepsTransformedCoordinatesInsideSafeViewport()
    {
        var gained = PhaseScopeTransform.Transform(1f, 1f, 4);

        Assert.InRange(gained.X, -1, 1);
        Assert.InRange(gained.Y, -1, 1);
    }

    [Fact]
    public void RtaRollingAverageUsesOnlyConfiguredNumberOfRecentSpectra()
    {
        var aggregator = new RtaBandAggregator();
        var now = DateTimeOffset.Parse("2026-10-07T12:00:00Z");

        var first = aggregator.Update(CreateSpectrum(1), RtaResolution.One, RtaChannelMode.Mono, 2, 100, 1_000, true, now);
        var second = aggregator.Update(CreateSpectrum(3), RtaResolution.One, RtaChannelMode.Mono, 2, 100, 1_000, true, now.AddSeconds(1));
        var third = aggregator.Update(CreateSpectrum(5), RtaResolution.One, RtaChannelMode.Mono, 2, 100, 1_000, true, now.AddSeconds(2));

        Assert.Equal(1, first.Single(band => band.CenterHz == 1_000).Magnitude);
        Assert.Equal(2, second.Single(band => band.CenterHz == 1_000).Magnitude, 5);
        Assert.Equal(4, third.Single(band => band.CenterHz == 1_000).Magnitude, 5);
    }

    [Theory]
    [InlineData(1_000, 0)]
    [InlineData(2_000, 3)]
    [InlineData(500, -3)]
    public void RtaTiltIsZeroAtOneKilohertzAndChangesByOctaves(double frequency, double expectedDb)
    {
        Assert.Equal(expectedDb, RtaBandAggregator.ApplyTilt(0, frequency, 3), 6);
    }

    [Fact]
    public void RtaReleaseDecaysInDbPerSecondAndPeakCapExpires()
    {
        var aggregator = new RtaBandAggregator();
        var now = DateTimeOffset.Parse("2026-10-07T12:00:00Z");
        aggregator.Update(CreateSpectrum(1), RtaResolution.One, RtaChannelMode.Mono, 1, 3, 500, true, now);

        var next = aggregator.Update(CreateSpectrum(0.5f), RtaResolution.One, RtaChannelMode.Mono, 1, 3, 500, true, now.AddSeconds(1));
        var band = next.Single(value => value.CenterHz == 1_000);

        Assert.Equal(-3, band.DisplayDb, 5);
        Assert.Equal(-3, band.PeakDb, 5);
    }

    [Fact]
    public void RtaSourceChangeClearsIncompatibleRollingSpectrumHistory()
    {
        var aggregator = new RtaBandAggregator();
        var now = DateTimeOffset.Parse("2026-10-07T12:00:00Z");
        aggregator.Update(CreateSpectrum(1), RtaResolution.One, RtaChannelMode.Left, 2, 100, 1_000, true, now);

        var changed = aggregator.Update(CreateSpectrum(0.5f), RtaResolution.One, RtaChannelMode.Right, 2, 100, 1_000, true, now.AddMilliseconds(20));

        Assert.Equal(0.5, changed.Single(band => band.CenterHz == 1_000).Magnitude, 5);
    }

    private static Spectrum CreateSpectrum(float magnitude)
    {
        var values = new float[24_001];
        values[1_000] = magnitude;
        return new Spectrum(48_000, 48_000, values);
    }
}
