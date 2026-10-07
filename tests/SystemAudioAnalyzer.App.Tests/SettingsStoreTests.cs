using SystemAudioAnalyzer.App.Settings;
using SystemAudioAnalyzer.App.ViewModels;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public async Task RoundTripPreservesWaterfallFloorAndRtaResolution()
    {
        var directory = CreateSettingsDirectory();
        var store = new SettingsStore(directory);
        var settings = MeasurementSettings.Default with
        {
            Waterfall = MeasurementSettings.Default.Waterfall with { DisplayFloorDb = -95 },
            Rta = MeasurementSettings.Default.Rta with { Resolution = RtaResolution.OneTwelfth },
        };

        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();

        Assert.Equal(-95, loaded.Waterfall.DisplayFloorDb);
        Assert.Equal(RtaResolution.OneTwelfth, loaded.Rta.Resolution);
    }

    [Fact]
    public async Task InvalidJsonReturnsDefaults()
    {
        var directory = CreateSettingsDirectory();
        var store = new SettingsStore(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"), "not json");

        var loaded = await store.LoadAsync();

        Assert.Equal(MeasurementSettings.Default, loaded);
    }

    [Fact]
    public async Task StructurallyIncompleteSettingsReturnDefaultsAndReportFallback()
    {
        var directory = CreateSettingsDirectory();
        var diagnostics = new List<string>();
        var store = new SettingsStore(directory, diagnostics.Add);
        await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"), "{}");

        var loaded = await store.LoadAsync();

        Assert.Equal(MeasurementSettings.Default, loaded);
        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task InvalidSavedSettingsDoNotPreventStartupWhenDiagnosticLoggerFails()
    {
        var directory = CreateSettingsDirectory();
        var store = new SettingsStore(directory, _ => throw new IOException("Log path unavailable."));
        await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"), "not json");

        var loaded = await store.LoadAsync();

        Assert.Equal(MeasurementSettings.Default, loaded);
    }

    [Fact]
    public async Task SaveRejectsInvalidSettingsWithoutCreatingAFile()
    {
        var directory = CreateSettingsDirectory();
        var store = new SettingsStore(directory);
        var invalid = MeasurementSettings.Default with
        {
            Analyzer = MeasurementSettings.Default.Analyzer with { CursorColor = "not-a-color" },
        };

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(invalid));
        Assert.False(File.Exists(store.SettingsPath));
    }

    [Fact]
    public async Task MissingNestedSettingReturnsDefaultsAndReportsFallback()
    {
        var directory = CreateSettingsDirectory();
        var diagnostics = new List<string>();
        var store = new SettingsStore(directory, diagnostics.Add);
        var json = JsonNode.Parse(JsonSerializer.Serialize(MeasurementSettings.Default))!;
        json["Analyzer"]!.AsObject().Remove(nameof(AnalyzerSettings.DisplayFloorDb));
        await File.WriteAllTextAsync(store.SettingsPath, json.ToJsonString());

        var loaded = await store.LoadAsync();

        Assert.Equal(MeasurementSettings.Default, loaded);
        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task ExpandedSettingsRoundTripPreservesGradientAndDynamics()
    {
        var store = new SettingsStore(CreateSettingsDirectory());
        var settings = MeasurementSettings.Default with
        {
            Analyzer = MeasurementSettings.Default.Analyzer with { FftSize = 4096, WindowFunction = AnalyzerWindowFunction.Hamming },
            Waterfall = MeasurementSettings.Default.Waterfall with { GradientStops = [new(-100, "Blue"), new(-20, "Cyan")] },
            Meters = MeasurementSettings.Default.Meters with { AttackMs = 10, ShowRmsBars = true },
            Rta = MeasurementSettings.Default.Rta with { AveragingCount = 100 },
        };
        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();
        Assert.Equal(4096, loaded.Analyzer.FftSize);
        Assert.Equal(AnalyzerWindowFunction.Hamming, loaded.Analyzer.WindowFunction);
        Assert.Equal(settings.Waterfall.GradientStops.ToArray(), loaded.Waterfall.GradientStops.ToArray());
        Assert.Equal(10, loaded.Meters.AttackMs);
        Assert.True(loaded.Meters.ShowRmsBars);
        Assert.Equal(100, loaded.Rta.AveragingCount);
        Assert.Equal(settings, loaded);
        Assert.Equal(settings.GetHashCode(), loaded.GetHashCode());
    }

    [Fact]
    public async Task SaveRejectsInvalidExpandedSettingsWithoutReplacingPreviousFile()
    {
        var store = new SettingsStore(CreateSettingsDirectory());
        await store.SaveAsync(MeasurementSettings.Default);
        var original = await File.ReadAllTextAsync(store.SettingsPath);
        var invalid = MeasurementSettings.Default with { Analyzer = MeasurementSettings.Default.Analyzer with { FftSize = 17 } };
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(invalid));
        Assert.Equal(original, await File.ReadAllTextAsync(store.SettingsPath));
    }

    [Fact]
    public async Task MissingGradientStopLevelReturnsDefaultsAndReportsFallback()
    {
        var diagnostics = new List<string>();
        var store = new SettingsStore(CreateSettingsDirectory(), diagnostics.Add);
        var json = JsonNode.Parse(JsonSerializer.Serialize(MeasurementSettings.Default))!;
        json["Waterfall"]!["GradientStops"]!.AsArray()[3]!.AsObject().Remove(nameof(ColorStop.LevelDb));
        await File.WriteAllTextAsync(store.SettingsPath, json.ToJsonString());
        Assert.Equal(MeasurementSettings.Default, await store.LoadAsync());
        Assert.Single(diagnostics);
    }

    private static string CreateSettingsDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AAAnalyzerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
