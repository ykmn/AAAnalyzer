using System.IO;
using System.Text.Json;

namespace SystemAudioAnalyzer.App.Settings;

/// <summary>Persists the user-curated URL library (named bookmarks, distinct from the automatic
/// <see cref="StreamHistoryStore"/>) in the config directory (url-library.json).</summary>
public sealed class UrlLibraryStore(string directory, Action<string>? diagnostic = null)
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public string Path { get; } = System.IO.Path.Combine(directory, "url-library.json");

    public async Task<IReadOnlyList<UrlLibraryEntry>> LoadAsync()
    {
        try
        {
            await using var stream = File.OpenRead(Path);
            return await JsonSerializer.DeserializeAsync<UrlLibraryEntry[]>(stream, _options) ?? [];
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return [];
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            diagnostic?.Invoke("URL library ignored: " + exception.Message);
            return [];
        }
    }

    public async Task SaveAsync(IEnumerable<UrlLibraryEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var temporary = Path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(entries.ToArray(), _options));
            File.Move(temporary, Path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostic?.Invoke("URL library was not saved: " + exception.Message);
        }
    }
}
