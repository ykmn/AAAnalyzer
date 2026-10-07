using System.Text.RegularExpressions;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class PortableBuildTests
{
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
