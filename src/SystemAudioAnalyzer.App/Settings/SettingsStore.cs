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
            var settings = await JsonSerializer.DeserializeAsync<MeasurementSettings>(stream, _serializerOptions, cancellationToken);
            if (settings is null || !IsValid(settings))
            {
                _diagnostic?.Invoke("Settings fallback: file does not contain a complete, valid settings document.");
                return MeasurementSettings.Default;
            }

            return settings;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            _diagnostic?.Invoke("Settings fallback: " + exception.Message);
            return MeasurementSettings.Default;
        }
    }

    private static bool IsValid(MeasurementSettings settings)
    {
        if (settings.Analyzer is null || settings.Waterfall is null || settings.Meters is null
            || settings.Loudness is null || settings.Rta is null || settings.Phase is null)
        {
            return false;
        }

        return double.IsFinite(settings.Waterfall.DisplayFloorDb)
            && double.IsFinite(settings.Waterfall.DisplayOffsetDb)
            && IsColor(settings.Waterfall.PaletteColor)
            && double.IsFinite(settings.Meters.DisplayRangeDb) && settings.Meters.DisplayRangeDb < 0
            && IsColor(settings.Meters.MeterColor) && IsColor(settings.Meters.OverloadColor)
            && settings.Loudness.HistorySeconds is >= 15 and <= 4_320
            && Enum.IsDefined(settings.Loudness.Metric)
            && double.IsFinite(settings.Loudness.SpanLufs) && settings.Loudness.SpanLufs > 0
            && double.IsFinite(settings.Loudness.CentreLufs)
            && Enum.IsDefined(settings.Rta.Source) && Enum.IsDefined(settings.Rta.Resolution)
            && double.IsFinite(settings.Rta.ScaleTopDb) && double.IsFinite(settings.Rta.ScaleRangeDb) && settings.Rta.ScaleRangeDb > 0
            && double.IsFinite(settings.Rta.TargetLineDb)
            && double.IsFinite(settings.Phase.Gain) && settings.Phase.Gain is >= 0.25 and <= 4
            && double.IsFinite(settings.Analyzer.DisplayFloorDb)
            && IsColor(settings.Analyzer.CursorColor) && IsColor(settings.Analyzer.TextColor);
    }

    private static bool IsColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            return System.Windows.Media.ColorConverter.ConvertFromString(value) is System.Windows.Media.Color;
        }
        catch (FormatException)
        {
            return false;
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
