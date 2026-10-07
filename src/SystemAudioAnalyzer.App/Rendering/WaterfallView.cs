using System.Windows.Input;
using System.Windows.Media;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed class WaterfallView : FrameworkElement
{
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame), typeof(AnalysisFrame), typeof(WaterfallView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnFrameChanged));

    private readonly WaterfallHistory _history = new(TimeSpan.FromSeconds(10));
    private double _cursor = 0.5;

    public WaterfallView()
    {
        MouseMove += OnMouseMove;
    }

    public AnalysisFrame? Frame
    {
        get => (AnalysisFrame?)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    public void Reset()
    {
        _history.Clear();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(11, 18, 32)), null, new Rect(new Point(), RenderSize));
        if (ActualWidth <= 1 || ActualHeight <= 1) return;
        var layout = WaterfallLayout.Calculate(ActualWidth, Math.Max(1, ActualHeight - 30));
        var rows = _history.GetVisibleRows(DateTimeOffset.UtcNow);
        DrawRows(context, rows, layout.LeftBounds, true);
        DrawRows(context, rows, layout.RightBounds, false);
        context.DrawLine(new Pen(Brushes.DimGray, 1), new Point(layout.RightBounds.Left, layout.LeftBounds.Top), new Point(layout.RightBounds.Left, layout.LeftBounds.Bottom));
        DrawCursor(context, layout.LeftBounds);
        DrawCursor(context, layout.RightBounds);
        DrawText(context, FrequencyScale.Format(FrequencyScale.ToHertz(_cursor)), 6, 4, 12, Brushes.White);
        foreach (var hertz in new[] { 20d, 100d, 1_000d, 10_000d, 20_000d })
        {
            var x = FrequencyScale.ToNormalized(hertz) * layout.LeftBounds.Width;
            context.DrawLine(new Pen(Brushes.DimGray, 1), new Point(x, layout.LeftBounds.Bottom), new Point(x, layout.LeftBounds.Bottom + 4));
            DrawText(context, FrequencyScale.Format(hertz), x + 2, layout.LeftBounds.Bottom + 5, 9, Brushes.LightGray);
        }
    }

    private static void OnFrameChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is AnalysisFrame frame) ((WaterfallView)target)._history.Append(frame);
    }

    private void OnMouseMove(object sender, MouseEventArgs args)
    {
        var paneWidth = Math.Max(1, ActualWidth / 2d);
        _cursor = Math.Clamp((args.GetPosition(this).X % paneWidth) / paneWidth, 0, 1);
        InvalidateVisual();
    }

    private void DrawRows(DrawingContext context, IReadOnlyList<WaterfallRow> rows, Rect bounds, bool left)
    {
        if (rows.Count == 0) return;
        var height = bounds.Height / rows.Count;
        for (var row = 0; row < rows.Count; row++)
        {
            var values = left ? rows[row].Left : rows[row].Right;
            for (var x = 0; x < 96; x++)
            {
                var index = Math.Min(values.Count - 1, (int)((long)x * values.Count / 96));
                var intensity = values.Count == 0 ? 0 : Math.Clamp(values[index] * 15, 0, 1);
                var brush = new SolidColorBrush(Color.FromRgb((byte)(12 + intensity * 220), (byte)(20 + intensity * 160), (byte)(45 + intensity * 170)));
                brush.Freeze();
                context.DrawRectangle(brush, null, new Rect(bounds.Left + x * bounds.Width / 96, bounds.Top + row * height, bounds.Width / 96 + 1, height + 1));
            }
        }
    }

    private void DrawCursor(DrawingContext context, Rect bounds)
    {
        var x = bounds.Left + bounds.Width * _cursor;
        context.DrawLine(new Pen(Brushes.White, 1), new Point(x, bounds.Top), new Point(x, bounds.Bottom));
    }

    private void DrawText(DrawingContext context, string text, double x, double y, double size, Brush brush) =>
        context.DrawText(new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));
}
