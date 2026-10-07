namespace SystemAudioAnalyzer.App.Rendering;

public static class PhaseScopeTransform
{
    private const double InverseRootTwo = 0.7071067811865476;

    public static Point Transform(float left, float right, double gain)
    {
        if (gain <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gain));
        }

        return new Point(
            (left - right) * gain * InverseRootTwo,
            (left + right) * gain * InverseRootTwo);
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
