using SystemAudioAnalyzer.App.Settings;
using System.Windows.Media;

namespace SystemAudioAnalyzer.App.Rendering;

public static class ColorGradient
{
    // Settings validation guarantees finite, strictly increasing stops and valid colors.
    public static Color Sample(IReadOnlyList<ColorStop> stops, double levelDb)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (stops.Count < 2) throw new ArgumentException("A gradient requires at least two stops.", nameof(stops));
        if (double.IsNaN(levelDb)) throw new ArgumentOutOfRangeException(nameof(levelDb));
        static Color Parse(ColorStop stop) => (Color)ColorConverter.ConvertFromString(stop.Color);
        if (levelDb <= stops[0].LevelDb) return Parse(stops[0]);
        for (var index = 1; index < stops.Count; index++)
        {
            var upper = stops[index];
            if (levelDb > upper.LevelDb) continue;
            var lower = stops[index - 1];
            var position = (levelDb - lower.LevelDb) / (upper.LevelDb - lower.LevelDb);
            var first = Parse(lower);
            var last = Parse(upper);
            byte Interpolate(byte from, byte to) => (byte)Math.Round(from + (to - from) * position, MidpointRounding.AwayFromZero);
            return Color.FromArgb(Interpolate(first.A, last.A), Interpolate(first.R, last.R), Interpolate(first.G, last.G), Interpolate(first.B, last.B));
        }
        return Parse(stops[^1]);
    }
}
