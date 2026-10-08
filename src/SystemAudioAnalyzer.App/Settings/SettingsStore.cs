using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace SystemAudioAnalyzer.App.Settings;

public sealed class SettingsStore
{
    // Added after profiles.json files already existed; a missing value takes its default instead of discarding the catalog.
    private static readonly HashSet<string> OptionalProperties = ["TargetLufs"];
    private readonly string _settingsDirectory;
    private readonly Action<string>? _diagnostic;
    private readonly string _legacySettingsPath;
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };
    private readonly JsonSerializerOptions _catalogSerializerOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            // The catalog constructor validates its get-only fields. Settings/profile init
            // properties must all be present so a full snapshot cannot silently gain defaults.
            Modifiers = { typeInfo => { foreach (var property in typeInfo.Properties) if (property.Set is not null) property.IsRequired = !OptionalProperties.Contains(property.Name); } },
        },
    };

    public SettingsStore(string? settingsDirectory = null, Action<string>? diagnostic = null)
        : this(settingsDirectory, diagnostic, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AAAnalyzer", "settings.json"))
    {
    }

    public SettingsStore(string? settingsDirectory, Action<string>? diagnostic, string legacySettingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacySettingsPath);
        _settingsDirectory = settingsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Data");
        _diagnostic = diagnostic;
        _legacySettingsPath = legacySettingsPath;
    }

    public string SettingsPath => Path.Combine(_settingsDirectory, "settings.json");

    public string CatalogPath => Path.Combine(_settingsDirectory, "profiles.json");

    public async Task<SettingsProfileCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = File.OpenRead(CatalogPath);
            return await JsonSerializer.DeserializeAsync<SettingsProfileCatalog>(stream, _catalogSerializerOptions, cancellationToken)
                ?? throw new JsonException("Profile catalog is null.");
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return await InitializeCatalogAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException or ArgumentException)
        {
            ReportDiagnostic("Profiles fallback: " + exception.Message);
            return SettingsProfileCatalog.Default;
        }
    }

    public async Task<MeasurementSettings> LoadStartupSettingsAsync(CancellationToken cancellationToken = default)
    {
        var catalog = await LoadCatalogAsync(cancellationToken);
        return catalog.Profiles.Single(p => p.Id == catalog.DefaultProfileId).Settings;
    }

    public async Task SaveCatalogAsync(SettingsProfileCatalog catalog, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _ = new SettingsProfileCatalog(catalog.SchemaVersion, catalog.DefaultProfileId, catalog.Profiles);
        await WriteAtomicallyAsync(CatalogPath, catalog, _catalogSerializerOptions, cancellationToken);
    }

    private async Task<SettingsProfileCatalog> InitializeCatalogAsync(CancellationToken cancellationToken)
    {
        var settings = await ImportLegacySettingsAsync(cancellationToken);
        var catalog = SettingsProfileCatalog.Default.SaveProfile(SettingsProfileCatalog.Default.DefaultProfileId, settings);
        try { await SaveCatalogAsync(catalog, cancellationToken); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ReportDiagnostic("Profiles initialization could not be saved: " + exception.Message);
        }
        return catalog;
    }

    private async Task<MeasurementSettings> ImportLegacySettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(_legacySettingsPath);
            var legacy = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
            if (legacy is not JsonObject) throw new JsonException("Legacy settings must be an object.");
            var merged = JsonSerializer.SerializeToNode(MeasurementSettings.Default)!;
            var fields = new Dictionary<string, string[]>
            {
                ["Analyzer"] = ["DisplayFloorDb", "CursorColor", "TextColor"],
                ["Waterfall"] = ["DisplayFloorDb", "DisplayOffsetDb", "PaletteColor"],
                ["Meters"] = ["DisplayRangeDb", "MeterColor", "OverloadColor"],
                ["Loudness"] = ["HistorySeconds", "Metric", "AutoScale", "SpanLufs", "CentreLufs"],
                ["Rta"] = ["Source", "Resolution", "ScaleTopDb", "ScaleRangeDb", "TargetLineDb"],
                ["Phase"] = ["Gain"],
            };
            foreach (var (section, names) in fields)
            {
                if (legacy[section] is not JsonObject oldSection) continue;
                foreach (var name in names)
                {
                    if (!oldSection.TryGetPropertyValue(name, out var value)) continue;
                    var candidate = merged.DeepClone();
                    candidate[section]![name] = value?.DeepClone();
                    try
                    {
                        var snapshot = candidate.Deserialize<MeasurementSettings>()!;
                        if (MeasurementSettingsValidator.IsValid(snapshot)) merged = candidate;
                        else ReportDiagnostic($"Legacy settings ignored invalid {section}.{name}.");
                    }
                    catch (JsonException) { ReportDiagnostic($"Legacy settings ignored invalid {section}.{name}."); }
                }
            }
            return merged.Deserialize<MeasurementSettings>()!;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return MeasurementSettings.Default;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            ReportDiagnostic("Legacy settings fallback: " + exception.Message);
            return MeasurementSettings.Default;
        }
    }

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
            if (settings is null || !MeasurementSettingsValidator.IsValid(settings))
            {
                ReportDiagnostic("Settings fallback: file does not contain a complete, valid settings document.");
                return MeasurementSettings.Default;
            }

            return settings;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            ReportDiagnostic("Settings fallback: " + exception.Message);
            return MeasurementSettings.Default;
        }
    }

    private void ReportDiagnostic(string message)
    {
        try
        {
            _diagnostic?.Invoke(message);
        }
        catch
        {
            // Diagnostics must never prevent startup fallback.
        }
    }

    public async Task SaveAsync(MeasurementSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!MeasurementSettingsValidator.IsValid(settings))
        {
            throw new ArgumentException("Settings contain invalid values.", nameof(settings));
        }

        // Until the dialog uses explicit profile operations, its saves update the
        // same Default snapshot that startup reads. Keep the old file for LoadAsync callers.
        var catalog = File.Exists(CatalogPath)
            ? await LoadCatalogAsync(cancellationToken)
            : SettingsProfileCatalog.Default;
        await SaveCatalogAsync(catalog.SaveProfile(catalog.DefaultProfileId, settings), cancellationToken);
        await WriteAtomicallyAsync(SettingsPath, settings, _serializerOptions, cancellationToken);
    }

    private async Task WriteAtomicallyAsync<T>(string destinationPath, T value, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_settingsDirectory);
        var temporaryPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, value, options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(destinationPath)) File.Replace(temporaryPath, destinationPath, destinationBackupFileName: null);
            else File.Move(temporaryPath, destinationPath);
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            { ReportDiagnostic("Settings temporary-file cleanup failed: " + exception.Message); }

            throw;
        }
    }
}
