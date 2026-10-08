using System.IO;
using System.Text.Json;

namespace SystemAudioAnalyzer.App.Localization;

/// <summary>Application-wide preferences that are not part of a settings profile (currently the language), in Data\app.json.</summary>
public sealed class AppPreferences(string directory, Action<string>? diagnostic = null)
{
    private sealed record Document(AppLanguage Language);

    public string Path { get; } = System.IO.Path.Combine(directory, "app.json");

    public AppLanguage LoadLanguage()
    {
        try
        {
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(Path));
            if (document is not null && Enum.IsDefined(document.Language)) return document.Language;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            diagnostic?.Invoke("Preferences ignored: " + exception.Message);
        }

        return Localizer.DetectSystemLanguage();
    }

    public void SaveLanguage(AppLanguage language)
    {
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path, JsonSerializer.Serialize(new Document(language)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostic?.Invoke("Preferences were not saved: " + exception.Message);
        }
    }
}
