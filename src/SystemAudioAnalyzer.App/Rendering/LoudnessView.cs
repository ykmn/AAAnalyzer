using System.Windows.Media;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed class LoudnessView : FrameworkElement
{
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(nameof(Frame), typeof(AnalysisFrame), typeof(LoudnessView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnFrameChanged));
    private readonly LoudnessHistory _history = new(TimeSpan.FromMinutes(1));

    public AnalysisFrame? Frame { get => (AnalysisFrame?)GetValue(FrameProperty); set => SetValue(FrameProperty, value); }

    public void Reset()
    {
        _history.Clear();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(17, 24, 39)), null, new Rect(new Point(), RenderSize));
        var points = _history.GetVisiblePoints(DateTimeOffset.UtcNow);
        var finite = points.SelectMany(point => new[] { point.MomentaryLufs, point.ShortTermLufs, point.IntegratedLufs }).Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        if (finite.Length == 0 || ActualWidth <= 1 || ActualHeight <= 1) return;
        var minimum = Math.Floor(finite.Min()) - 1;
        var maximum = Math.Ceiling(finite.Max()) + 1;
        for (var second = 0; second <= 60; second += 10)
        {
            var x = ActualWidth * second / 60d;
            context.DrawLine(new Pen(Brushes.DimGray, 0.5), new Point(x, 0), new Point(x, ActualHeight));
        }
        for (var lufs = minimum; lufs <= maximum; lufs++)
        {
            var y = Map(lufs, minimum, maximum);
            context.DrawLine(new Pen(Brushes.DimGray, 1), new Point(0, y), new Point(ActualWidth, y));
            DrawLabel(context, $"{lufs:0} LUFS", 3, y - 12);
        }
        DrawSeries(context, points, point => point.MomentaryLufs, Brushes.Gold, minimum, maximum);
        DrawSeries(context, points, point => point.ShortTermLufs, Brushes.DeepSkyBlue, minimum, maximum);
        DrawSeries(context, points, point => point.IntegratedLufs, Brushes.MediumPurple, minimum, maximum);
    }

    private static void OnFrameChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is AnalysisFrame frame && frame.AdvancedMeasurements is { } measurements)
            ((LoudnessView)target)._history.Append(frame.Timestamp, measurements.Loudness);
    }

    private void DrawSeries(DrawingContext context, IReadOnlyList<LoudnessHistoryPoint> points, Func<LoudnessHistoryPoint, float?> select, Brush brush, double min, double max)
    {
        Point? previous = null;
        for (var index = 0; index < points.Count; index++)
        {
            var value = select(points[index]);
            if (!value.HasValue) { previous = null; continue; }
            var current = new Point(index * ActualWidth / Math.Max(1, points.Count - 1), Map(value.Value, min, max));
            if (previous is not null) context.DrawLine(new Pen(brush, 1.5), previous.Value, current);
            previous = current;
        }
    }

    private double Map(double value, double min, double max) => ActualHeight - ((value - min) / (max - min) * ActualHeight);

    private void DrawLabel(DrawingContext context, string text, double x, double y) =>
        context.DrawText(new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 9, Brushes.LightGray, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));
}
