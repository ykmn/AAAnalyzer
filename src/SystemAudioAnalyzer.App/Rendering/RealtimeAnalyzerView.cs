using System.Windows.Media;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed class RealtimeAnalyzerView : FrameworkElement
{
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame),
        typeof(AnalysisFrame),
        typeof(RealtimeAnalyzerView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnFrameChanged));

    private readonly WaterfallRenderer _waterfall = new();

    public AnalysisFrame? Frame
    {
        get => (AnalysisFrame?)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb(11, 18, 32)), null, new Rect(new Point(), RenderSize));
        var frame = Frame;
        if (frame is null || ActualWidth <= 1 || ActualHeight <= 1)
        {
            return;
        }

        var rtaHeight = ActualHeight * 0.35;
        RtaRenderer.Render(drawingContext, frame, new Rect(0, 0, ActualWidth, rtaHeight));
        var waterfallHeight = ActualHeight - rtaHeight - 24;
        if (waterfallHeight <= 0)
        {
            return;
        }

        var layout = WaterfallLayout.Calculate(ActualWidth, waterfallHeight);
        layout = layout with
        {
            LeftBounds = new Rect(layout.LeftBounds.X, rtaHeight + 24, layout.LeftBounds.Width, layout.LeftBounds.Height),
            RightBounds = new Rect(layout.RightBounds.X, rtaHeight + 24, layout.RightBounds.Width, layout.RightBounds.Height),
        };
        drawingContext.DrawText(CreateText("L", layout.LeftBounds.Left + 8, rtaHeight + 4), new Point(layout.LeftBounds.Left + 8, rtaHeight + 3));
        drawingContext.DrawText(CreateText("R", layout.RightBounds.Left + 8, rtaHeight + 4), new Point(layout.RightBounds.Left + 8, rtaHeight + 3));
        _waterfall.Render(drawingContext, layout);
    }

    private static void OnFrameChanged(DependencyObject target, DependencyPropertyChangedEventArgs arguments)
    {
        if (arguments.NewValue is AnalysisFrame frame)
        {
            ((RealtimeAnalyzerView)target)._waterfall.Append(frame);
        }
    }

    private static FormattedText CreateText(string text, double x, double y) => new(
        text,
        System.Globalization.CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        new Typeface("Segoe UI"),
        12,
        Brushes.White,
        VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);
}
