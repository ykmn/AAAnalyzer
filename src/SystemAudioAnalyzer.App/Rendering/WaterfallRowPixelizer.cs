using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed record WaterfallPixelSettings(uint[] Palette, double FloorDb, double OffsetDb, IReadOnlyList<ColorStop> Stops, double Gain, AnalyzerFrequencyScale Scale)
{
    public static WaterfallPixelSettings From(MeasurementSettings settings)
    {
        var floor = SpectrumDisplayScale.EffectiveFloor(settings.Analyzer.DisplayFloorDb, settings.Waterfall.DisplayFloorDb);
        var palette = WaterfallRenderer.CreateArgbPalette(floor, settings.Waterfall.DisplayOffsetDb, settings.Waterfall.GradientStops);
        return new WaterfallPixelSettings(palette, floor, settings.Waterfall.DisplayOffsetDb, settings.Waterfall.GradientStops, settings.Analyzer.Gain, settings.Analyzer.FrequencyScale);
    }
}

/// <summary>Maps one spectrum frame onto a row of palette colours, one pixel per horizontal position.</summary>
public static class WaterfallRowPixelizer
{
    public static uint[] CreateRow(IReadOnlyList<float> magnitudes, int sampleRate, int fftSize, int width, WaterfallPixelSettings settings)
    {
        ArgumentNullException.ThrowIfNull(magnitudes);
        ArgumentNullException.ThrowIfNull(settings);
        if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
        var row = new uint[width];
        for (var x = 0; x < width; x++)
        {
            var lowerHertz = FrequencyScale.ToHertz((double)x / width, settings.Scale);
            var upperHertz = FrequencyScale.ToHertz((double)(x + 1) / width, settings.Scale);
            var firstBin = Math.Max(0, (int)Math.Floor(lowerHertz * fftSize / sampleRate));
            var lastBin = Math.Min(magnitudes.Count - 1, (int)Math.Ceiling(upperHertz * fftSize / sampleRate));
            var magnitude = 0f;
            for (var bin = firstBin; bin <= lastBin; bin++) magnitude = Math.Max(magnitude, magnitudes[bin]);
            var db = magnitudes.Count == 0 ? settings.FloorDb : 20 * Math.Log10(Math.Max(magnitude * settings.Gain, 0.000001d));
            var index = WaterfallRenderer.GetPaletteIndex(db, settings.FloorDb, settings.OffsetDb, settings.Stops, settings.Palette.Length);
            row[x] = settings.Palette[index];
        }
        return row;
    }
}
