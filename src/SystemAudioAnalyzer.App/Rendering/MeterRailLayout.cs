namespace SystemAudioAnalyzer.App.Rendering;

public sealed record MeterRailLayout(Rect LeftMaximum, Rect RightMaximum, Rect LeftOverload, Rect RightOverload, Rect LeftMeter, Rect RightMeter)
{
    public static MeterRailLayout Calculate(double width, double height)
    {
        var half = width / 2d;
        var leftX = 8d;
        var rightX = half + 8d;
        var barWidth = Math.Max(1d, half - 16d);
        var meterTop = Math.Min(56d, height);
        return new MeterRailLayout(
            new Rect(leftX, 2, barWidth, 14), new Rect(rightX, 2, barWidth, 14),
            new Rect(leftX, 30, barWidth, 13), new Rect(rightX, 30, barWidth, 13),
            new Rect(leftX, meterTop, barWidth, Math.Max(1d, height - meterTop - 6)),
            new Rect(rightX, meterTop, barWidth, Math.Max(1d, height - meterTop - 6)));
    }
}
