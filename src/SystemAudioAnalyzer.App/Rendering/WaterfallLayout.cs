namespace SystemAudioAnalyzer.App.Rendering;

public sealed record WaterfallLayout(Rect LeftBounds, Rect RightBounds)
{
    public double GetNormalizedX(double x)
    {
        var bounds = x <= LeftBounds.Right ? LeftBounds : RightBounds;
        return Math.Clamp((x - bounds.Left) / bounds.Width, 0d, 1d);
    }

    public static WaterfallLayout Calculate(double availableWidth, double availableHeight)
    {
        if (availableWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableWidth));
        }

        if (availableHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableHeight));
        }

        var halfWidth = availableWidth / 2d;
        return new WaterfallLayout(
            new Rect(0, 0, halfWidth, availableHeight),
            new Rect(halfWidth, 0, availableWidth - halfWidth, availableHeight));
    }
}
