namespace SystemAudioAnalyzer.App.Rendering;

public static class SpectrumDisplayScale
{
    public static double EffectiveFloor(double analyzerFloorDb, double instrumentFloorDb) =>
        Math.Max(analyzerFloorDb, instrumentFloorDb);
}
