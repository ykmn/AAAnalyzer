namespace SystemAudioAnalyzer.App.Settings;

/// <summary>Writes a library entry list out as an M3U/M3U8 playlist, the inverse of <see cref="M3uPlaylistParser"/>.</summary>
public static class M3uPlaylistWriter
{
    public static IEnumerable<string> Write(IEnumerable<UrlLibraryEntry> entries)
    {
        yield return "#EXTM3U";
        foreach (var entry in entries)
        {
            yield return $"#EXTINF:-1,{entry.Name}";
            yield return entry.Url;
        }
    }
}
