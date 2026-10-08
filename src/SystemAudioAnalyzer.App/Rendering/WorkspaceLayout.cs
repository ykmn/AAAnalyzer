namespace SystemAudioAnalyzer.App.Rendering;

public static class WorkspaceLayout
{
    public const double PeakRailWidth = 130;

    // The peak/LU bars and the Loudness plot share one vertical extent: both start PlotTop below the top of the
    // workspace row and end PlotBottomReserve above its bottom. MainWindow.xaml row heights must match these values.
    public const double TabBarHeight = 26;
    public const double LoudnessToolbarHeight = 54;
    public const double LoudnessPlotTopGutter = 16;
    public const double PlotBottomReserve = 94;
    public const double PlotTop = TabBarHeight + LoudnessToolbarHeight + LoudnessPlotTopGutter;
}
