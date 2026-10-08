using System.Reflection;

namespace SystemAudioAnalyzer.App;

/// <summary>Name, version (from VERSION.txt at build time) and build date of the running program.</summary>
public static class AppInfo
{
    public const string Name = "AA Analyzer";
    public const string RepositoryUrl = "https://github.com/ykmn/AAAnalyzer";

    public static string Version { get; } = typeof(AppInfo).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.000";

    public static string BuildDate { get; } = typeof(AppInfo).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == "BuildDate")?.Value ?? string.Empty;

    public static string Title => $"{Name} {Version}";
}
