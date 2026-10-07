using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App.Services;

public interface IAnalyzerController
{
    event EventHandler<AnalysisFrame>? FrameAvailable;

    event EventHandler<AudioSourceStateChangedEventArgs>? SourceStateChanged;

    Task StartAsync(SourceSelection selection, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    void ResetTruePeak(int channel);

    void ResetTruePeakMaximum(int channel);

    void ResetTruePeakOverload(int channel);

    void ResetLoudness();

    void SetAnalysisConfiguration(AnalysisConfiguration configuration);

    void SetLoudnessIntegratedWindow(int seconds);
}
