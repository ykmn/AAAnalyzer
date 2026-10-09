using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Rendering;

/// <summary>Stereo waterfall: left channel above right, time flowing top to bottom, one bitmap per channel.</summary>
public sealed class WaterfallView : FrameworkElement
{
    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(MeasurementSettings), typeof(WaterfallView), new FrameworkPropertyMetadata(MeasurementSettings.Default, FrameworkPropertyMetadataOptions.AffectsRender, OnSettingsChanged));
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame), typeof(AnalysisFrame), typeof(WaterfallView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnFrameChanged));

    private const uint Background = 0xFF000000;

    private WaterfallBitmapBuffer? _left;
    private WaterfallBitmapBuffer? _right;
    private WriteableBitmap? _leftBitmap;
    private WriteableBitmap? _rightBitmap;
    private WaterfallPixelSettings _pixels = WaterfallPixelSettings.From(MeasurementSettings.Default);
    private double _cursor = 0.5;

    public WaterfallView()
    {
        MouseMove += OnMouseMove;
        Localization.Localizer.Instance.LanguageChanged += (_, _) => InvalidateVisual();
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    public AnalysisFrame? Frame
    {
        get => (AnalysisFrame?)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    public MeasurementSettings Settings { get => (MeasurementSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }

    public void Reset()
    {
        _left?.Clear();
        _right?.Clear();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(Brushes.Black, null, new Rect(new Point(), RenderSize));
        if (ActualWidth <= 1 || ActualHeight <= 1) return;
        var layout = WaterfallLayout.Calculate(ActualWidth, ActualHeight);
        EnsureBuffers(layout);
        if (_left is null || _right is null || _leftBitmap is null || _rightBitmap is null) return;
        Upload(_leftBitmap, _left);
        Upload(_rightBitmap, _right);
        context.DrawImage(_leftBitmap, layout.LeftBounds);
        context.DrawImage(_rightBitmap, layout.RightBounds);
        DrawCursor(context, layout);
        DrawAxis(context, layout);
        DrawTimeAxis(context, layout);
    }

    private static void OnFrameChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is AnalysisFrame frame) ((WaterfallView)target).AppendFrame(frame);
    }

    private static void OnSettingsChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        var view = (WaterfallView)target;
        view._pixels = WaterfallPixelSettings.From(view.Settings);
    }

    private void AppendFrame(AnalysisFrame frame)
    {
        var stereo = frame.AdvancedMeasurements?.StereoSpectrum;
        if (stereo is null || _left is null || _right is null) return;
        var sampleRate = frame.Format.SampleRate;
        _left.Append(frame.Timestamp, WaterfallRowPixelizer.CreateRow(stereo.Left.Magnitudes, sampleRate, stereo.Left.FftSize, _left.Width, _pixels));
        _right.Append(frame.Timestamp, WaterfallRowPixelizer.CreateRow(stereo.Right.Magnitudes, sampleRate, stereo.Right.FftSize, _right.Width, _pixels));
    }

    private double _buffersWindowSeconds;

    private void EnsureBuffers(WaterfallLayout layout)
    {
        var width = Math.Max(1, (int)Math.Round(layout.LeftBounds.Width));
        var height = Math.Max(1, (int)Math.Round(layout.LeftBounds.Height));
        var windowSeconds = Math.Max(1, Settings.Waterfall.WindowSeconds);
        if (_left is not null && _left.Width == width && _left.Height == height && _buffersWindowSeconds == windowSeconds) return;
        _buffersWindowSeconds = windowSeconds;
        var window = TimeSpan.FromSeconds(windowSeconds);
        _left = new WaterfallBitmapBuffer(width, height, window, Background);
        _right = new WaterfallBitmapBuffer(width, height, window, Background);
        _leftBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        _rightBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
    }

    private static void Upload(WriteableBitmap bitmap, WaterfallBitmapBuffer buffer) =>
        bitmap.WritePixels(new Int32Rect(0, 0, buffer.Width, buffer.Height), buffer.Pixels, buffer.Width * 4, 0);

    private void OnMouseMove(object sender, MouseEventArgs args)
    {
        if (ActualWidth <= 1 || ActualHeight <= 1) return;
        _cursor = WaterfallLayout.Calculate(ActualWidth, ActualHeight).GetNormalizedX(args.GetPosition(this).X);
        InvalidateVisual();
    }

    private void DrawCursor(DrawingContext context, WaterfallLayout layout)
    {
        var x = layout.LeftBounds.Left + (layout.LeftBounds.Width * _cursor);
        context.DrawLine(new Pen(ColorBrush(Settings.Analyzer.CursorColor), 1), new Point(x, layout.LeftBounds.Top), new Point(x, layout.RightBounds.Bottom));
        var hertz = FrequencyScale.ToHertz(_cursor, Settings.Analyzer.FrequencyScale, Settings.Analyzer.MaxFrequencyHz);
        var text = Format(FrequencyScale.Format(hertz), 11, Brushes.White);
        var plaque = new Rect(Math.Clamp(x - text.Width - 8, 0, Math.Max(0, ActualWidth - text.Width - 8)), 3, text.Width + 8, text.Height + 2);
        context.DrawRectangle(Brushes.Black, null, plaque);
        context.DrawText(text, new Point(plaque.Left + 4, plaque.Top + 1));
    }

    private void DrawAxis(DrawingContext context, WaterfallLayout layout)
    {
        var axis = layout.AxisBounds;
        var textBrush = ColorBrush(Settings.Analyzer.TextColor);
        var lastRight = double.NegativeInfinity;
        foreach (var label in AxisTicks.WaterfallFrequencyLabels())
        {
            var x = FrequencyScale.ToNormalized(label.Hertz, Settings.Analyzer.FrequencyScale, Settings.Analyzer.MaxFrequencyHz) * axis.Width;
            context.DrawLine(new Pen(Brushes.DimGray, 1), new Point(x, axis.Top), new Point(x, axis.Top + 4));
            var text = Format(label.Label, 10, textBrush);
            var left = Math.Clamp(x - (text.Width / 2), 1, Math.Max(1, axis.Width - text.Width - 1));
            if (left < lastRight + 2) continue;
            context.DrawText(text, new Point(left, axis.Top + 4));
            lastRight = left + text.Width;
        }
    }

    /// <summary>Time-since-now scale in the gutter to the right of each channel: 0 at the top (newest), -WindowSeconds
    /// at the bottom (oldest). Each gutter is headed with its channel letter (L/R).</summary>
    private void DrawTimeAxis(DrawingContext context, WaterfallLayout layout)
    {
        var textBrush = ColorBrush(Settings.Analyzer.TextColor);
        var windowSeconds = Settings.Waterfall.WindowSeconds;
        DrawTimeAxisColumn(context, layout.TimeAxisLeftBounds, textBrush, "L", windowSeconds);
        DrawTimeAxisColumn(context, layout.TimeAxisRightBounds, textBrush, "R", windowSeconds);
    }

    private void DrawTimeAxisColumn(DrawingContext context, Rect bounds, Brush textBrush, string channelLabel, double windowSeconds)
    {
        var channelText = Format(channelLabel, 11, Brushes.White);
        context.DrawText(channelText, new Point(bounds.Left + 4, bounds.Top));

        var rulerTop = bounds.Top + channelText.Height + 2;
        var rulerBounds = new Rect(bounds.Left, rulerTop, bounds.Width, Math.Max(0, bounds.Bottom - rulerTop));

        const int steps = 5;
        for (var i = 0; i <= steps; i++)
        {
            var fraction = (double)i / steps;
            var y = rulerBounds.Top + (fraction * rulerBounds.Height);
            var seconds = fraction * windowSeconds;
            var label = i == 0 ? "0s" : $"-{seconds:0}s";
            context.DrawLine(new Pen(Brushes.DimGray, 1), new Point(bounds.Left, y), new Point(bounds.Left + 4, y));
            var text = Format(label, 9, textBrush);
            var top = Math.Clamp(y - (text.Height / 2), rulerBounds.Top, rulerBounds.Bottom - text.Height);
            context.DrawText(text, new Point(bounds.Left + 6, top));
        }
    }

    private FormattedText Format(string text, double size, Brush brush) =>
        new(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static Brush ColorBrush(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
}
