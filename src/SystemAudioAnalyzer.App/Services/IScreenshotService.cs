namespace SystemAudioAnalyzer.App.Services;

public interface IScreenshotService
{
    Task<string> SaveAsync(FrameworkElement element, string paneName, CancellationToken cancellationToken = default);
}
