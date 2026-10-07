using System.Windows.Media;
using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed class RtaView : FrameworkElement
{
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(nameof(Frame), typeof(AnalysisFrame), typeof(RtaView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ResolutionProperty = DependencyProperty.Register(nameof(Resolution), typeof(RtaResolution), typeof(RtaView), new FrameworkPropertyMetadata(RtaResolution.OneThird, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ChannelModeProperty = DependencyProperty.Register(nameof(ChannelMode), typeof(RtaChannelMode), typeof(RtaView), new FrameworkPropertyMetadata(RtaChannelMode.Mono, FrameworkPropertyMetadataOptions.AffectsRender));

    public AnalysisFrame? Frame { get => (AnalysisFrame?)GetValue(FrameProperty); set => SetValue(FrameProperty, value); }
    public RtaResolution Resolution { get => (RtaResolution)GetValue(ResolutionProperty); set => SetValue(ResolutionProperty, value); }
    public RtaChannelMode ChannelMode { get => (RtaChannelMode)GetValue(ChannelModeProperty); set => SetValue(ChannelModeProperty, value); }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(17, 24, 39)), null, new Rect(new Point(), RenderSize));
        var stereo = Frame?.AdvancedMeasurements?.StereoSpectrum;
        var spectrum = ChannelMode switch { RtaChannelMode.Left => stereo?.Left, RtaChannelMode.Right => stereo?.Right, _ => stereo?.Mono ?? Frame?.Spectrum };
        if (spectrum is null || ActualWidth <= 1 || ActualHeight <= 1) return;
        for (var step = 0; step <= 8; step++)
        {
            var y = step * ActualHeight / 8d;
            context.DrawLine(new Pen(Brushes.DimGray, 0.5), new Point(0, y), new Point(ActualWidth, y));
        }
        var targetY = ActualHeight * 0.45;
        context.DrawLine(new Pen(Brushes.IndianRed, 1), new Point(0, targetY), new Point(ActualWidth, targetY));
        var bands = RtaBandAggregator.Aggregate(spectrum, Resolution);
        var width = ActualWidth / bands.Count;
        for (var index = 0; index < bands.Count; index++)
        {
            var db = 20 * Math.Log10(Math.Max(bands[index].Magnitude, 0.000001f));
            var height = Math.Clamp((db + 80) / 80, 0, 1) * ActualHeight;
            context.DrawRectangle(Brushes.DodgerBlue, null, new Rect(index * width + 1, ActualHeight - height, Math.Max(1, width - 2), height));
        }
    }
}
