using System.Windows.Media;
using SystemAudioAnalyzer.App.Settings;
using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed class RtaView : FrameworkElement
{
    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(MeasurementSettings), typeof(RtaView), new FrameworkPropertyMetadata(MeasurementSettings.Default, FrameworkPropertyMetadataOptions.AffectsRender, OnSettingsChanged));
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(nameof(Frame), typeof(AnalysisFrame), typeof(RtaView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnFrameChanged));
    public static readonly DependencyProperty ResolutionProperty = DependencyProperty.Register(nameof(Resolution), typeof(RtaResolution), typeof(RtaView), new FrameworkPropertyMetadata(RtaResolution.OneThird, FrameworkPropertyMetadataOptions.AffectsRender, OnConfigurationChanged));
    public static readonly DependencyProperty ChannelModeProperty = DependencyProperty.Register(nameof(ChannelMode), typeof(RtaChannelMode), typeof(RtaView), new FrameworkPropertyMetadata(RtaChannelMode.Mono, FrameworkPropertyMetadataOptions.AffectsRender, OnConfigurationChanged));

    private readonly RtaBandAggregator _aggregator = new();
    private IReadOnlyList<RtaBand> _bands = [];
    private SolidColorBrush _barBrush = new((Color)ColorConverter.ConvertFromString(MeasurementSettings.Default.Rta.BarColor));
    private SolidColorBrush _peakBrush = new((Color)ColorConverter.ConvertFromString(MeasurementSettings.Default.Rta.PeakCapColor));
    private SolidColorBrush _targetBrush = new((Color)ColorConverter.ConvertFromString(MeasurementSettings.Default.Rta.TargetBandColor));

    public AnalysisFrame? Frame { get => (AnalysisFrame?)GetValue(FrameProperty); set => SetValue(FrameProperty, value); }
    public RtaResolution Resolution { get => (RtaResolution)GetValue(ResolutionProperty); set => SetValue(ResolutionProperty, value); }
    public RtaChannelMode ChannelMode { get => (RtaChannelMode)GetValue(ChannelModeProperty); set => SetValue(ChannelModeProperty, value); }
    public MeasurementSettings Settings { get => (MeasurementSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }

    private static void OnSettingsChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        var view = (RtaView)target;
        view._barBrush = CreateBrush(view.Settings.Rta.BarColor);
        view._peakBrush = CreateBrush(view.Settings.Rta.PeakCapColor);
        view._targetBrush = CreateBrush(view.Settings.Rta.TargetBandColor);
    }

    private static void OnConfigurationChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        ((RtaView)target)._aggregator.Reset();
    }

    private static void OnFrameChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is not AnalysisFrame frame) return;
        var view = (RtaView)target;
        var stereo = frame.AdvancedMeasurements?.StereoSpectrum;
        var spectrum = view.ChannelMode switch
        {
            RtaChannelMode.Left => stereo?.Left,
            RtaChannelMode.Right => stereo?.Right,
            _ => stereo?.Mono ?? frame.Spectrum,
        };
        if (spectrum is null)
        {
            view._bands = [];
            return;
        }

        var settings = view.Settings.Rta;
        view._bands = view._aggregator.Update(spectrum, view.Resolution, view.ChannelMode,
            settings.AveragingCount, settings.ReleaseDbPerSecond, settings.PeakHoldMs,
            settings.ShowPeakHoldCaps, frame.Timestamp);
    }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(17, 24, 39)), null, new Rect(new Point(), RenderSize));
        if (_bands.Count == 0 || ActualWidth <= 1 || ActualHeight <= 1) return;
        for (var step = 0; step <= 8; step++)
        {
            var y = step * ActualHeight / 8d;
            context.DrawLine(new Pen(Brushes.DimGray, 0.5), new Point(0, y), new Point(ActualWidth, y));
        }
        var displayFloor = SpectrumDisplayScale.EffectiveFloor(Settings.Analyzer.DisplayFloorDb, Settings.Rta.ScaleTopDb - Settings.Rta.ScaleRangeDb);
        var range = Math.Max(1, Settings.Rta.ScaleTopDb - displayFloor);
        var targetTop = Settings.Rta.TargetLineDb + Settings.Rta.TargetRangeDb / 2;
        var targetBottom = Settings.Rta.TargetLineDb - Settings.Rta.TargetRangeDb / 2;
        var topY = ActualHeight * Math.Clamp((Settings.Rta.ScaleTopDb - targetTop) / range, 0, 1);
        var bottomY = ActualHeight * Math.Clamp((Settings.Rta.ScaleTopDb - targetBottom) / range, 0, 1);
        context.DrawRectangle(_targetBrush, null, new Rect(0, topY, ActualWidth, Math.Max(0, bottomY - topY)));
        var targetY = ActualHeight * Math.Clamp((Settings.Rta.ScaleTopDb - Settings.Rta.TargetLineDb) / range, 0, 1);
        context.DrawLine(new Pen(_targetBrush, 1), new Point(0, targetY), new Point(ActualWidth, targetY));
        var width = ActualWidth / _bands.Count;
        for (var index = 0; index < _bands.Count; index++)
        {
            var db = RtaBandAggregator.ApplyTilt(_bands[index].DisplayDb, _bands[index].CenterHz, Settings.Rta.TiltDbPerOctave);
            var height = Math.Clamp((db - displayFloor) / range, 0, 1) * ActualHeight;
            var x = index * width + 1;
            context.DrawRectangle(_barBrush, null, new Rect(x, ActualHeight - height, Math.Max(1, width - 2), height));
            if (Settings.Rta.ShowPeakHoldCaps)
            {
                var peakDb = RtaBandAggregator.ApplyTilt(_bands[index].PeakDb, _bands[index].CenterHz, Settings.Rta.TiltDbPerOctave);
                var peakY = ActualHeight * Math.Clamp((Settings.Rta.ScaleTopDb - peakDb) / range, 0, 1);
                context.DrawLine(new Pen(_peakBrush, 1), new Point(x, peakY), new Point(x + Math.Max(1, width - 2), peakY));
            }
        }
    }

    private static SolidColorBrush CreateBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
