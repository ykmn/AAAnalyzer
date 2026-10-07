namespace SystemAudioAnalyzer.App.Rendering;

public static class PhaseScopeTransform
{
    private const double InverseRootTwo = 0.7071067811865476;

    public static Point Transform(float left, float right, double gain)
    {
        if (!double.IsFinite(gain) || gain <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gain));
        }

        var x = (left - right) * gain * InverseRootTwo;
        var y = (left + right) * gain * InverseRootTwo;
        return new Point(
            double.IsFinite(x) ? Math.Clamp(x, -1d, 1d) : 0d,
            double.IsFinite(y) ? Math.Clamp(y, -1d, 1d) : 0d);
    }

    public static Rect CalculateViewport(double availableWidth, double availableHeight)
    {
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableWidth), "Viewport dimensions must be positive.");
        }

        var side = Math.Min(availableWidth, availableHeight);
        return new Rect((availableWidth - side) / 2, (availableHeight - side) / 2, side, side);
    }
}
