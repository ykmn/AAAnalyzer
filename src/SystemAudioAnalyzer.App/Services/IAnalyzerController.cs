using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App.Services;

public interface IAnalyzerController
{
    Task StartAsync(SourceSelection selection, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
