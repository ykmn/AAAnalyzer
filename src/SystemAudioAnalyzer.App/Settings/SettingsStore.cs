using System.IO;
using System.Text.Json;

namespace SystemAudioAnalyzer.App.Settings;

public sealed class SettingsStore
{
    private readonly string _settingsDirectory;
    private readonly Action<string>? _diagnostic;
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };

    public SettingsStore(string? settingsDirectory = null, Action<string>? diagnostic = null)
    {
        _settingsDirectory = settingsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AAAnalyzer");
        _diagnostic = diagnostic;
    }

    public string SettingsPath => Path.Combine(_settingsDirectory, "settings.json");

    public async Task<MeasurementSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return MeasurementSettings.Default;
            }

            await using var stream = File.OpenRead(SettingsPath);
            return await JsonSerializer.DeserializeAsync<MeasurementSettings>(stream, _serializerOptions, cancellationToken)
                ?? MeasurementSettings.Default;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            _diagnostic?.Invoke("Settings fallback: " + exception.Message);
            return MeasurementSettings.Default;
        }
    }

    public async Task SaveAsync(MeasurementSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(_settingsDirectory);
        var temporaryPath = SettingsPath + ".tmp";
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, _serializerOptions, cancellationToken);
            }

            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }
}
