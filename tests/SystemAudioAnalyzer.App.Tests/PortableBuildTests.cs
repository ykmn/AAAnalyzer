using System.Text.RegularExpressions;
using System.Diagnostics;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class PortableBuildTests
{
    [Fact]
    public void SettingsStoreDefaultsToExecutableDataDirectory()
    {
        var store = new SettingsStore(settingsDirectory: null);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "Data", "profiles.json"), store.CatalogPath);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "Data", "settings.json"), store.SettingsPath);
    }

    [Fact]
    public async Task BuildAndPublishCreateAppLocalDataDirectories()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AAAnalyzerTests", Guid.NewGuid().ToString("N"));
        var output = Path.Combine(directory, "build");
        var publish = Path.Combine(directory, "publish");
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("msbuild");
        start.ArgumentList.Add(Path.Combine(FindRepositoryRoot(), "src", "SystemAudioAnalyzer.App", "SystemAudioAnalyzer.App.csproj"));
        start.ArgumentList.Add("-target:CreateAppDataDirectory");
        start.ArgumentList.Add("-property:OutDir=" + output + Path.DirectorySeparatorChar);
        start.ArgumentList.Add("-property:PublishDir=" + publish + Path.DirectorySeparatorChar);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await stdout + await stderr);
        Assert.True(Directory.Exists(Path.Combine(output, "Data")));
        Assert.True(Directory.Exists(Path.Combine(publish, "Data")));
    }

    [Fact]
    public void PortableBuildLocaleFilterRecognizesScriptAndRegionTags()
    {
        var repositoryRoot = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(repositoryRoot, "release", "rebuild.ps1"));
        var patternMatch = Regex.Match(script, @"\$localePattern\s*=\s*'([^']+)'");

        Assert.True(patternMatch.Success, "The portable build must define a reusable locale-folder pattern.");
        var pattern = patternMatch.Groups[1].Value;
        Assert.Matches(pattern, "ru");
        Assert.Matches(pattern, "en");
        Assert.Matches(pattern, "zh-Hans");
        Assert.Matches(pattern, "zh-Hant");
        Assert.Matches(pattern, "sr-Latn-RS");
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
