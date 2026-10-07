namespace SystemAudioAnalyzer.App.ViewModels;

/// <summary>Coarse state of the analysis source, used to highlight the Start and Stop buttons.</summary>
public enum AnalysisRunState
{
    Stopped,
    Starting,
    Running,
    Faulted,
}
