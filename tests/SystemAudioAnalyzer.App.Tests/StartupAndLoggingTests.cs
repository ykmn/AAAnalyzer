using SystemAudioAnalyzer.App.Diagnostics;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class StartupAndLoggingTests
{
    [Fact]
    public void ApplicationConfigurationStartsMainWindow()
    {
        var repositoryRoot = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(repositoryRoot, "src", "SystemAudioAnalyzer.App", "App.xaml"));

        Assert.Contains("StartupUri=\"MainWindow.xaml\"", appXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void LoggerWritesMessageAndExceptionToApplicationLog()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"AAAnalyzerTests-{Guid.NewGuid():N}");
        try
        {
            var logger = new AppLogger(directory);
            logger.Write("Application started.");
            logger.Write(new InvalidOperationException("Test failure."));

            var content = File.ReadAllText(Path.Combine(directory, "logs", "AAAnalyzer.log"));
            Assert.Contains("Application started.", content, StringComparison.Ordinal);
            Assert.Contains("Test failure.", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SystemAudioAnalyzer.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
