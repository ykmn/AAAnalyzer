using System.Windows.Media;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed class PhaseScopeView : FrameworkElement
{
    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(MeasurementSettings), typeof(PhaseScopeView), new FrameworkPropertyMetadata(MeasurementSettings.Default, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(nameof(Frame), typeof(AnalysisFrame), typeof(PhaseScopeView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty GainProperty = DependencyProperty.Register(nameof(Gain), typeof(double), typeof(PhaseScopeView), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));
    public AnalysisFrame? Frame { get => (AnalysisFrame?)GetValue(FrameProperty); set => SetValue(FrameProperty, value); }
    public double Gain { get => (double)GetValue(GainProperty); set => SetValue(GainProperty, value); }
    public MeasurementSettings Settings { get => (MeasurementSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(17, 24, 39)), null, new Rect(new Point(), RenderSize));
        var points = Frame?.AdvancedMeasurements?.PhaseScope?.Points;
        if (points is null) return;
        var viewport = PhaseScopeTransform.CalculateViewport(ActualWidth, ActualHeight);
        var center = new Point(viewport.Left + viewport.Width / 2, viewport.Top + viewport.Height / 2);
        context.DrawRectangle(null, new Pen(Brushes.DimGray, 1), viewport);
        context.DrawLine(new Pen(Brushes.DimGray, 1), new Point(center.X, viewport.Top), new Point(center.X, viewport.Bottom));
        context.DrawLine(new Pen(Brushes.DimGray, 1), new Point(viewport.Left, center.Y), new Point(viewport.Right, center.Y));
        foreach (var pair in points)
        {
            var rotated = PhaseScopeTransform.Transform(pair.Left, pair.Right, Gain);
            context.DrawEllipse(Brushes.LimeGreen, null, new Point(center.X + rotated.X * viewport.Width / 2, center.Y - rotated.Y * viewport.Height / 2), 1, 1);
        }
    }
}
