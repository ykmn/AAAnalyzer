namespace SystemAudioAnalyzer.App.Rendering;

/// <summary>Quantises RTA bar heights into LED-style segments.</summary>
public static class RtaSegments
{
    public static int Count(double heightPixels, double pitchPixels)
    {
        if (!double.IsFinite(heightPixels) || heightPixels <= 0 || !double.IsFinite(pitchPixels) || pitchPixels <= 0) return 0;
        return (int)Math.Floor(heightPixels / pitchPixels);
    }
}
