using System.IO;
using System.Text.Json;

namespace SystemAudioAnalyzer.App.Settings;

/// <summary>Remembers the most recently opened stream URLs in the config directory (stream-history.json).</summary>
public sealed class StreamHistoryStore(string directory, Action<string>? diagnostic = null)
{
    public const int Capacity = 60;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public string Path { get; } = System.IO.Path.Combine(directory, "stream-history.json");

    public async Task<IReadOnlyList<string>> LoadAsync()
    {
        try
        {
            await using var stream = File.OpenRead(Path);
            var urls = await JsonSerializer.DeserializeAsync<string[]>(stream, _options) ?? [];
            return urls.Where(url => !string.IsNullOrWhiteSpace(url)).Distinct(StringComparer.Ordinal).Take(Capacity).ToArray();
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return [];
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            diagnostic?.Invoke("Stream history ignored: " + exception.Message);
            return [];
        }
    }

    public async Task SaveAsync(IEnumerable<string> urls)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var temporary = Path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(urls.Take(Capacity).ToArray(), _options));
            File.Move(temporary, Path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostic?.Invoke("Stream history was not saved: " + exception.Message);
        }
    }
}
