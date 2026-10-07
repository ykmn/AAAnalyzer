namespace SystemAudioAnalyzer.App.Rendering;

/// <summary>Stereo waterfall layout: left channel on top, right channel below, frequency axis underneath.</summary>
public sealed record WaterfallLayout(Rect LeftBounds, Rect RightBounds, Rect AxisBounds)
{
    public const double AxisHeight = 18d;
    public const double ChannelGap = 1d;

    public double GetNormalizedX(double x) => Math.Clamp((x - LeftBounds.Left) / LeftBounds.Width, 0d, 1d);

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

        var plotHeight = Math.Max(3d, availableHeight - AxisHeight);
        var channelHeight = Math.Max(1d, (plotHeight - ChannelGap) / 2d);
        return new WaterfallLayout(
            new Rect(0, 0, availableWidth, channelHeight),
            new Rect(0, channelHeight + ChannelGap, availableWidth, channelHeight),
            new Rect(0, plotHeight, availableWidth, AxisHeight));
    }
}
