namespace SystemAudioAnalyzer.App.Rendering;

using SystemAudioAnalyzer.App.Settings;

public static class SpectrumDisplayScale
{
    public static double EffectiveFloor(double analyzerFloorDb, double instrumentFloorDb) =>
        Math.Max(analyzerFloorDb, instrumentFloorDb);

    public static double ToNormalizedAmplitude(double magnitude, double gain, AnalyzerAmplitudeScale scale, double displayFloorDb)
    {
        if (!double.IsFinite(gain) || gain <= 0) throw new ArgumentOutOfRangeException(nameof(gain));
        if (!double.IsFinite(displayFloorDb) || displayFloorDb >= 0) throw new ArgumentOutOfRangeException(nameof(displayFloorDb));
        if (!Enum.IsDefined(scale)) throw new ArgumentOutOfRangeException(nameof(scale));
        var amplitude = Math.Max(0, double.IsFinite(magnitude) ? magnitude : 0) * gain;
        return scale switch
        {
            AnalyzerAmplitudeScale.Linear => Math.Clamp(amplitude, 0, 1),
            AnalyzerAmplitudeScale.Logarithmic => Math.Clamp((20 * Math.Log10(Math.Max(amplitude, Math.Pow(10, displayFloorDb / 20))) - displayFloorDb) / -displayFloorDb, 0, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(scale)),
        };
    }
}
