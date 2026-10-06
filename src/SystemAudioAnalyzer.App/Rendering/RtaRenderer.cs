using System.Windows.Media;

namespace SystemAudioAnalyzer.App.Rendering;

public static class RtaRenderer
{
    public static void Render(DrawingContext context, AnalysisFrame frame, Rect bounds)
    {
        var spectrum = frame.Spectrum;
        if (spectrum is null || spectrum.Magnitudes.Count < 2 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var drawing = geometry.Open())
        {
            for (var index = 1; index < spectrum.Magnitudes.Count; index++)
            {
                var normalizedFrequency = Math.Log10(index + 1d) / Math.Log10(spectrum.Magnitudes.Count);
                var magnitude = Math.Clamp(spectrum.Magnitudes[index] / 50d, 0d, 1d);
                var point = new Point(
                    bounds.Left + (normalizedFrequency * bounds.Width),
                    bounds.Bottom - (magnitude * bounds.Height));
                if (index == 1)
                {
                    drawing.BeginFigure(point, false, false);
                }
                else
                {
                    drawing.LineTo(point, true, false);
                }
            }
        }

        geometry.Freeze();
        context.DrawGeometry(null, new Pen(Brushes.LimeGreen, 1.5), geometry);
    }
}
