using System.Windows.Media;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed class PhaseScopeView : FrameworkElement
{
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(nameof(Frame), typeof(AnalysisFrame), typeof(PhaseScopeView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty GainProperty = DependencyProperty.Register(nameof(Gain), typeof(double), typeof(PhaseScopeView), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));
    public AnalysisFrame? Frame { get => (AnalysisFrame?)GetValue(FrameProperty); set => SetValue(FrameProperty, value); }
    public double Gain { get => (double)GetValue(GainProperty); set => SetValue(GainProperty, value); }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(17, 24, 39)), null, new Rect(new Point(), RenderSize));
        var points = Frame?.AdvancedMeasurements?.PhaseScope?.Points;
        if (points is null) return;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        context.DrawLine(new Pen(Brushes.DimGray, 1), new Point(center.X, 0), new Point(center.X, ActualHeight));
        context.DrawLine(new Pen(Brushes.DimGray, 1), new Point(0, center.Y), new Point(ActualWidth, center.Y));
        foreach (var pair in points)
        {
            var rotated = PhaseScopeTransform.Transform(pair.Left, pair.Right, Gain);
            context.DrawEllipse(Brushes.LimeGreen, null, new Point(center.X + rotated.X * ActualWidth / 2, center.Y - rotated.Y * ActualHeight / 2), 1, 1);
        }
    }
}
