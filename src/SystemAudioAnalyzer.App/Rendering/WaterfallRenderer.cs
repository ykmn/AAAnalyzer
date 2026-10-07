using System.Windows.Media;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed class WaterfallRenderer
{
    private const int MaximumRows = 150;
    private readonly Queue<(float[] Left, float[] Right)> _rows = new();
    private MeasurementSettings? _paletteSettings;
    private SolidColorBrush[] _palette = [];

    public static Color SampleColor(double levelDb, double displayFloorDb, double displayOffsetDb, IReadOnlyList<ColorStop> gradientStops)
    {
        if (!double.IsFinite(levelDb)) levelDb = displayFloorDb;
        if (!double.IsFinite(displayFloorDb)) throw new ArgumentOutOfRangeException(nameof(displayFloorDb));
        if (!double.IsFinite(displayOffsetDb)) throw new ArgumentOutOfRangeException(nameof(displayOffsetDb));
        return ColorGradient.Sample(gradientStops, Math.Max(displayFloorDb, levelDb + displayOffsetDb));
    }

    public static SolidColorBrush[] CreatePalette(double displayFloorDb, double displayOffsetDb, IReadOnlyList<ColorStop> gradientStops, int colorCount = 256)
    {
        if (colorCount < 2) throw new ArgumentOutOfRangeException(nameof(colorCount));
        var top = Math.Max(displayFloorDb, gradientStops[^1].LevelDb);
        var palette = new SolidColorBrush[colorCount];
        for (var index = 0; index < colorCount; index++)
        {
            var level = displayFloorDb + (top - displayFloorDb) * index / (colorCount - 1d);
            var brush = new SolidColorBrush(SampleColor(level - displayOffsetDb, displayFloorDb, displayOffsetDb, gradientStops));
            brush.Freeze();
            palette[index] = brush;
        }
        return palette;
    }

    public static int GetPaletteIndex(double levelDb, double displayFloorDb, double displayOffsetDb, IReadOnlyList<ColorStop> gradientStops, int colorCount)
    {
        if (colorCount < 2) throw new ArgumentOutOfRangeException(nameof(colorCount));
        var displayedDb = Math.Max(displayFloorDb, levelDb + displayOffsetDb);
        var top = Math.Max(displayFloorDb, gradientStops[^1].LevelDb);
        var normalized = Math.Clamp((displayedDb - displayFloorDb) / Math.Max(1e-9, top - displayFloorDb), 0, 1);
        return (int)Math.Round(normalized * (colorCount - 1), MidpointRounding.AwayFromZero);
    }

    public void Append(AnalysisFrame frame)
    {
        if (frame.Spectrum is null)
        {
            return;
        }

        var columns = 96;
        var leftLevel = frame.Levels.Count > 0 ? frame.Levels[0].Rms : 0f;
        var rightLevel = frame.Levels.Count > 1 ? frame.Levels[1].Rms : leftLevel;
        _rows.Enqueue((CreateRow(frame.Spectrum.Magnitudes, columns, leftLevel), CreateRow(frame.Spectrum.Magnitudes, columns, rightLevel)));
        while (_rows.Count > MaximumRows)
        {
            _rows.Dequeue();
        }
    }

    public void Render(DrawingContext context, WaterfallLayout layout, MeasurementSettings? settings = null)
    {
        settings ??= MeasurementSettings.Default;
        if (_palette.Length == 0 || !Equals(_paletteSettings, settings))
        {
            var floor = SpectrumDisplayScale.EffectiveFloor(settings.Analyzer.DisplayFloorDb, settings.Waterfall.DisplayFloorDb);
            _palette = CreatePalette(floor, settings.Waterfall.DisplayOffsetDb, settings.Waterfall.GradientStops);
            _paletteSettings = settings;
        }
        var rows = _rows.ToArray();
        if (rows.Length == 0)
        {
            return;
        }

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var height = layout.LeftBounds.Height / rows.Length;
            var y = layout.LeftBounds.Top + (rowIndex * height);
            RenderRow(context, rows[rowIndex].Left, layout.LeftBounds, y, height, settings);
            RenderRow(context, rows[rowIndex].Right, layout.RightBounds, y, height, settings);
        }
    }

    private static float[] CreateRow(IReadOnlyList<float> magnitudes, int columns, float level)
    {
        var row = new float[columns];
        for (var column = 0; column < columns; column++)
        {
            var start = 1 + (column * (magnitudes.Count - 1) / columns);
            var end = Math.Min(magnitudes.Count, 1 + ((column + 1) * (magnitudes.Count - 1) / columns));
            var peak = 0f;
            for (var index = start; index < end; index++)
            {
                peak = Math.Max(peak, magnitudes[index]);
            }

            row[column] = Math.Clamp((peak / 50f) * Math.Max(level, 0.15f), 0f, 1f);
        }

        return row;
    }

    private void RenderRow(DrawingContext context, IReadOnlyList<float> row, Rect bounds, double y, double height, MeasurementSettings settings)
    {
        var width = bounds.Width / row.Count;
        var floor = SpectrumDisplayScale.EffectiveFloor(settings.Analyzer.DisplayFloorDb, settings.Waterfall.DisplayFloorDb);
        var top = Math.Max(floor, settings.Waterfall.GradientStops[^1].LevelDb);
        for (var column = 0; column < row.Count; column++)
        {
            var levelDb = floor + Math.Clamp(row[column], 0, 1) * (top - floor);
            var paletteIndex = GetPaletteIndex(levelDb, floor, settings.Waterfall.DisplayOffsetDb, settings.Waterfall.GradientStops, _palette.Length);
            context.DrawRectangle(_palette[paletteIndex], null, new Rect(bounds.Left + (column * width), y, width + 0.2, height + 0.2));
        }
    }
}
