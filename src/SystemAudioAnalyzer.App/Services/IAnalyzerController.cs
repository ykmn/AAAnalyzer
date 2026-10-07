using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App.Services;

public interface IAnalyzerController
{
    event EventHandler<AnalysisFrame>? FrameAvailable;

    event EventHandler<AudioSourceStateChangedEventArgs>? SourceStateChanged;

    Task StartAsync(SourceSelection selection, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    void ResetTruePeak(int channel);

    void ResetLoudness();
}
