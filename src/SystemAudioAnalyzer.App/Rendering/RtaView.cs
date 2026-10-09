using System.Windows;
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

    public void Reset()
    {
        _aggregator.Reset();
        _bands = [];
        InvalidateVisual();
    }

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
            settings.ShowPeakHoldCaps, frame.Timestamp, view.Settings.Analyzer.MaxFrequencyHz);
    }

    private const double LeftGutter = 30;
    private const double BottomGutter = 16;
    private const double RightGutter = 4;
    private const double SegmentPitch = 4;

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(8, 11, 16)), null, new Rect(new Point(), RenderSize));
        if (_bands.Count == 0 || ActualWidth <= LeftGutter + RightGutter + 1 || ActualHeight <= BottomGutter + 1) return;
        var rta = Settings.Rta;
        var plot = new Rect(LeftGutter, 0, ActualWidth - LeftGutter - RightGutter, ActualHeight - BottomGutter);
        var displayFloor = SpectrumDisplayScale.EffectiveFloor(Settings.Analyzer.DisplayFloorDb, rta.ScaleTopDb - rta.ScaleRangeDb);
        var range = Math.Max(1, rta.ScaleTopDb - displayFloor);
        double ToY(double db) => plot.Bottom - (Math.Clamp((db - displayFloor) / range, 0, 1) * plot.Height);
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 0.5);
        var textBrush = new SolidColorBrush(Color.FromRgb(150, 165, 180));

        var targetTop = rta.TargetLineDb + (rta.TargetRangeDb / 2);
        var targetBottom = rta.TargetLineDb - (rta.TargetRangeDb / 2);
        context.DrawRectangle(_targetBrush, null, new Rect(plot.Left, ToY(targetTop), plot.Width, Math.Max(0, ToY(targetBottom) - ToY(targetTop))));

        foreach (var tick in AxisTicks.RtaDb(rta.ScaleTopDb, range, 10))
        {
            var y = plot.Bottom - (tick.Ratio * plot.Height);
            context.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            var label = CreateText(tick.Label, 9, textBrush);
            context.DrawText(label, new Point(LeftGutter - label.Width - 3, Math.Clamp(y - (label.Height / 2), 0, ActualHeight - BottomGutter - label.Height)));
        }

        var width = plot.Width / _bands.Count;
        var barWidth = Math.Max(1, width - 2);
        for (var index = 0; index < _bands.Count; index++)
        {
            var db = RtaBandAggregator.ApplyTilt(_bands[index].DisplayDb, _bands[index].CenterHz, rta.TiltDbPerOctave);
            var x = plot.Left + (index * width) + 1;
            var segments = RtaSegments.Count(plot.Bottom - ToY(db), SegmentPitch);
            for (var segment = 0; segment < segments; segment++)
            {
                var segmentDb = displayFloor + (((segment + 0.5) * SegmentPitch / plot.Height) * range);
                var inTarget = segmentDb >= targetBottom && segmentDb <= targetTop;
                context.DrawRectangle(inTarget ? _peakBrush : _barBrush, null, new Rect(x, plot.Bottom - ((segment + 1) * SegmentPitch) + 1, barWidth, SegmentPitch - 1));
            }
            if (rta.ShowPeakHoldCaps)
            {
                var peakDb = RtaBandAggregator.ApplyTilt(_bands[index].PeakDb, _bands[index].CenterHz, rta.TiltDbPerOctave);
                var capY = plot.Bottom - (RtaSegments.Count(plot.Bottom - ToY(peakDb), SegmentPitch) * SegmentPitch);
                if (capY < plot.Bottom) context.DrawRectangle(_peakBrush, null, new Rect(x, capY - 2, barWidth, 2));
            }
        }

        var lastRight = double.NegativeInfinity;
        foreach (var label in AxisTicks.RtaFrequencyLabels())
        {
            var nearest = 0;
            var nearestDistance = double.MaxValue;
            for (var index = 0; index < _bands.Count; index++)
            {
                var distance = Math.Abs(Math.Log(_bands[index].CenterHz / label.Hertz));
                if (distance < nearestDistance) { nearest = index; nearestDistance = distance; }
            }
            var text = CreateText(label.Label, 9, textBrush);
            var left = Math.Clamp(plot.Left + ((nearest + 0.5) * width) - (text.Width / 2), 0, ActualWidth - text.Width);
            if (left < lastRight + 4) continue;
            context.DrawText(text, new Point(left, plot.Bottom + 2));
            lastRight = left + text.Width;
        }
    }

    private FormattedText CreateText(string text, double size, Brush brush) =>
        new(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static SolidColorBrush CreateBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
