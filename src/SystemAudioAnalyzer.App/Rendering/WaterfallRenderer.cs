using System.Windows.Media;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed class WaterfallRenderer
{
    private const int MaximumRows = 150;
    private readonly Queue<(float[] Left, float[] Right)> _rows = new();

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

    public void Render(DrawingContext context, WaterfallLayout layout)
    {
        var rows = _rows.ToArray();
        if (rows.Length == 0)
        {
            return;
        }

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var height = layout.LeftBounds.Height / rows.Length;
            var y = layout.LeftBounds.Top + (rowIndex * height);
            RenderRow(context, rows[rowIndex].Left, layout.LeftBounds, y, height);
            RenderRow(context, rows[rowIndex].Right, layout.RightBounds, y, height);
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

    private static void RenderRow(DrawingContext context, IReadOnlyList<float> row, Rect bounds, double y, double height)
    {
        var width = bounds.Width / row.Count;
        for (var column = 0; column < row.Count; column++)
        {
            context.DrawRectangle(CreateBrush(row[column]), null, new Rect(bounds.Left + (column * width), y, width + 0.2, height + 0.2));
        }
    }

    private static Brush CreateBrush(float intensity)
    {
        var color = Color.FromRgb(
            (byte)(12 + (intensity * 220)),
            (byte)(20 + (intensity * 210)),
            (byte)(55 + (intensity * 120)));
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
