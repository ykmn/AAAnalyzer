using SystemAudioAnalyzer.App.Rendering;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class WaterfallRowPixelizerTests
{
    private static WaterfallPixelSettings Settings()
    {
        ColorStop[] stops = [new(-100, "#000000"), new(0, "#FFFFFF")];
        return new WaterfallPixelSettings(WaterfallRenderer.CreateArgbPalette(-100, 0, stops), -100, 0, stops, 1, AnalyzerFrequencyScale.Logarithmic);
    }

    [Fact]
    public void SpikeAtOneKilohertzLightsOnlyNearbyPixels()
    {
        var magnitudes = new float[2_049];
        magnitudes[85] = 1f;

        var row = WaterfallRowPixelizer.CreateRow(magnitudes, 48_000, 4_096, 100, Settings());

        Assert.Equal(100, row.Length);
        Assert.Equal(0xFFFFFFFFu, row[56]);
        Assert.Equal(0xFF000000u, row[10]);
        Assert.Equal(0xFF000000u, row[90]);
    }

    [Fact]
    public void EmptySpectrumProducesFloorColour()
    {
        var row = WaterfallRowPixelizer.CreateRow([], 48_000, 4_096, 8, Settings());

        Assert.All(row, pixel => Assert.Equal(0xFF000000u, pixel));
    }

    [Fact]
    public void ArgbPaletteEndsAreFullyOpaque()
    {
        var palette = WaterfallRenderer.CreateArgbPalette(-100, 0, [new ColorStop(-100, "#000000"), new ColorStop(0, "#FF8000")]);

        Assert.Equal(256, palette.Length);
        Assert.Equal(0xFF000000u, palette[0]);
        Assert.Equal(0xFFFF8000u, palette[^1]);
    }
}
