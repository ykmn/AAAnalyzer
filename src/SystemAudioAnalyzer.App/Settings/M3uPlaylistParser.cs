using System.IO;

namespace SystemAudioAnalyzer.App.Settings;

/// <summary>Minimal M3U/M3U8 reader: pairs an "#EXTINF:...,Name" line with the URL line that
/// follows it; a bare URL with no preceding EXTINF is named after itself. Other "#" lines
/// (e.g. "#EXTM3U") and blank lines are ignored.</summary>
public static class M3uPlaylistParser
{
    public static IReadOnlyList<UrlLibraryEntry> Parse(IEnumerable<string> lines)
    {
        var entries = new List<UrlLibraryEntry>();
        string? pendingName = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                var commaIndex = line.LastIndexOf(',');
                pendingName = commaIndex >= 0 ? line[(commaIndex + 1)..].Trim() : null;
                continue;
            }

            if (line.StartsWith('#')) continue;

            entries.Add(new UrlLibraryEntry(string.IsNullOrWhiteSpace(pendingName) ? line : pendingName, line));
            pendingName = null;
        }

        return entries;
    }

    public static async Task<IReadOnlyList<UrlLibraryEntry>> ParseFileAsync(string path) =>
        Parse(await File.ReadAllLinesAsync(path));
}
