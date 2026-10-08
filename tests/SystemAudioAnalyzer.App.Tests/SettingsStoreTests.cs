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

    [Fact]
    public async Task CompatibilitySaveSurvivesRestartWithoutAnExistingCatalog()
    {
        var store = CreateCatalogStore();
        var settings = SettingsProfileCatalogTests.ChangedSettings;

        await store.SaveAsync(settings);

        var restarted = new SettingsStore(Path.GetDirectoryName(store.CatalogPath), null, store.CatalogPath + ".legacy");
        Assert.Equal(settings, await restarted.LoadStartupSettingsAsync());
        Assert.Equal(settings, await restarted.LoadAsync());
        Assert.Equal("Default", (await restarted.LoadCatalogAsync()).Profiles.Single().Name);
    }

    [Fact]
    public async Task DialogApplyAndSaveUseProfileCatalogInsteadOfCompatibilitySettingsFile()
    {
        var store = CreateCatalogStore();
        await store.SaveCatalogAsync(SettingsProfileCatalog.Default);
        var viewModel = new SettingsDialogViewModel(store, await store.LoadCatalogAsync(),
            MeasurementSettings.Default, InstrumentTab.Analyzer);
        viewModel.PhaseGain = 1.6;

        viewModel.Apply();
        await viewModel.SaveAsync();

        Assert.Equal(1.6, (await store.LoadStartupSettingsAsync()).Phase.Gain);
        Assert.False(File.Exists(store.SettingsPath));
    }

    [Fact]
    public async Task CompatibilitySaveUpdatesDesignatedDefaultAndPreservesOtherProfiles()
    {
        var store = CreateCatalogStore();
        var catalog = SettingsProfileCatalog.Default.SaveAsProfile("Studio", MeasurementSettings.Default);
        var studio = catalog.Profiles.Single(p => p.Name == "Studio");
        catalog = catalog.SetDefaultProfile(studio.Id);
        await store.SaveCatalogAsync(catalog);

        await store.SaveAsync(SettingsProfileCatalogTests.ChangedSettings);

        var restarted = new SettingsStore(Path.GetDirectoryName(store.CatalogPath), null, store.CatalogPath + ".legacy");
        Assert.Equal(SettingsProfileCatalogTests.ChangedSettings, await restarted.LoadStartupSettingsAsync());
        var loaded = await restarted.LoadCatalogAsync();
        Assert.Equal(studio.Id, loaded.DefaultProfileId);
        Assert.Equal(catalog.Profiles[0], loaded.Profiles[0]);
        Assert.Equal(studio.Name, loaded.Profiles[1].Name);
        Assert.Equal(2, loaded.Profiles.Count);
    }

    [Fact]
    public async Task CompatibilitySaveCatalogFailurePreservesBothPreviousDocuments()
    {
        var store = CreateCatalogStore();
        await store.SaveCatalogAsync(SettingsProfileCatalog.Default);
        await store.SaveAsync(MeasurementSettings.Default);
        var original = await File.ReadAllTextAsync(store.CatalogPath);
        var oldSettings = await File.ReadAllTextAsync(store.SettingsPath);
        using (var locked = new FileStream(store.CatalogPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => store.SaveAsync(SettingsProfileCatalogTests.ChangedSettings));
        }
        Assert.Equal(original, await File.ReadAllTextAsync(store.CatalogPath));
        Assert.Equal(oldSettings, await File.ReadAllTextAsync(store.SettingsPath));
        Assert.Equal(MeasurementSettings.Default, await store.LoadStartupSettingsAsync());
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(store.CatalogPath)!, "*.tmp"));
    }

    [Fact]
    public async Task StartupLoadsDesignatedDefaultAndIgnoresOldAppLocalSettings()
    {
        var store = CreateCatalogStore();
        var catalog = SettingsProfileCatalog.Default.SaveAsProfile("Studio", SettingsProfileCatalogTests.ChangedSettings);
        catalog = catalog.SetDefaultProfile(catalog.Profiles[1].Id);
        await store.SaveCatalogAsync(SettingsProfileCatalog.Default);
        await store.SaveCatalogAsync(catalog);
        await File.WriteAllTextAsync(store.SettingsPath, JsonSerializer.Serialize(MeasurementSettings.Default));

        var restarted = new SettingsStore(Path.GetDirectoryName(store.CatalogPath), null, store.CatalogPath + ".legacy");
        Assert.Equal(SettingsProfileCatalogTests.ChangedSettings, await restarted.LoadStartupSettingsAsync());
        var loaded = await restarted.LoadCatalogAsync();
        Assert.Equal(catalog.DefaultProfileId, loaded.DefaultProfileId);
        Assert.Equal(catalog.Profiles.ToArray(), loaded.Profiles.ToArray());
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(store.CatalogPath)!, "*.tmp"));
    }

    [Fact]
    public async Task NewInstallCreatesBuiltInDefaultAndDataDirectory()
    {
        var directory = Path.Combine(CreateSettingsDirectory(), "Data");
        var store = new SettingsStore(directory, null, Path.Combine(directory, "absent-legacy.json"));
        Assert.Equal(MeasurementSettings.Default, await store.LoadStartupSettingsAsync());
        Assert.True(File.Exists(Path.Combine(directory, "profiles.json")));
        Assert.Equal("Default", (await store.LoadCatalogAsync()).Profiles.Single().Name);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"SchemaVersion\":99,\"DefaultProfileId\":\"one\",\"Profiles\":[]}")]
    public async Task ExistingCorruptCatalogFallsBackWithoutImportingLegacy(string text)
    {
        var diagnostics = new List<string>();
        var store = CreateCatalogStore(diagnostics.Add);
        await File.WriteAllTextAsync(store.CatalogPath + ".legacy", LegacyJson);
        await File.WriteAllTextAsync(store.CatalogPath, text);

        Assert.Equal(MeasurementSettings.Default, await store.LoadStartupSettingsAsync());
        Assert.Single(diagnostics);
        Assert.Equal(text, await File.ReadAllTextAsync(store.CatalogPath));
        Assert.True(File.Exists(store.CatalogPath + ".legacy"));
    }

    [Theory]
    [InlineData("missing-default")]
    [InlineData("duplicate-id")]
    [InlineData("duplicate-name")]
    [InlineData("invalid-settings")]
    [InlineData("missing-snapshot-field")]
    [InlineData("missing-section")]
    public async Task CatalogValidationFailureReturnsBuiltInDefault(string defect)
    {
        var diagnostics = new List<string>();
        var store = CreateCatalogStore(diagnostics.Add);
        var json = JsonNode.Parse(JsonSerializer.Serialize(SettingsProfileCatalog.Default.SaveAsProfile("Studio", SettingsProfileCatalogTests.ChangedSettings)))!;
        var profiles = json["Profiles"]!.AsArray();
        switch (defect)
        {
            case "missing-default": json["DefaultProfileId"] = "absent"; break;
            case "duplicate-id": profiles[1]!["Id"] = profiles[0]!["Id"]!.GetValue<string>(); break;
            case "duplicate-name": profiles[1]!["Name"] = "default"; break;
            case "invalid-settings": profiles[1]!["Settings"]!["Phase"]!["Gain"] = 99; break;
            case "missing-snapshot-field": profiles[1]!["Settings"]!["Analyzer"]!.AsObject().Remove("FftSize"); break;
            case "missing-section": profiles[1]!["Settings"]!.AsObject().Remove("Rta"); break;
        }
        await File.WriteAllTextAsync(store.CatalogPath, json.ToJsonString());
        Assert.Equal(MeasurementSettings.Default, await store.LoadStartupSettingsAsync());
        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task MigrationPreservesEveryLegacyFieldAndUsesDefaultsForNewFieldsOnce()
    {
        var store = CreateCatalogStore();
        var legacy = store.CatalogPath + ".legacy";
        await File.WriteAllTextAsync(legacy, LegacyJson);
        var settings = await store.LoadStartupSettingsAsync();

        Assert.Equal(-115, settings.Analyzer.DisplayFloorDb);
        Assert.Equal("#112233", settings.Analyzer.CursorColor);
        Assert.Equal("#445566", settings.Analyzer.TextColor);
        Assert.Equal(-95, settings.Waterfall.DisplayFloorDb);
        Assert.Equal(7, settings.Waterfall.DisplayOffsetDb);
        Assert.Equal(-72, settings.Meters.DisplayRangeDb);
        Assert.Equal("#234567", settings.Meters.MeterColor);
        Assert.Equal("#345678", settings.Meters.OverloadColor);
        Assert.Equal(120, settings.Loudness.HistorySeconds);
        Assert.Equal(LoudnessMetric.ShortTerm, settings.Loudness.Metric);
        Assert.False(settings.Loudness.AutoScale);
        Assert.Equal(12, settings.Loudness.SpanLufs);
        Assert.Equal(-18, settings.Loudness.CentreLufs);
        Assert.Equal(RtaChannelMode.Right, settings.Rta.Source);
        Assert.Equal(RtaResolution.OneThird, settings.Rta.Resolution);
        Assert.Equal(3, settings.Rta.ScaleTopDb);
        Assert.Equal(72, settings.Rta.ScaleRangeDb);
        Assert.Equal(-24, settings.Rta.TargetLineDb);
        Assert.Equal(2, settings.Phase.Gain);
        Assert.Equal(8192, settings.Analyzer.FftSize);
        Assert.Equal(AnalyzerWindowFunction.Blackman, settings.Analyzer.WindowFunction);
        Assert.Equal(52, settings.Meters.AttackMs);
        Assert.Equal(50, settings.Rta.AveragingCount);
        Assert.Equal(MeasurementSettings.Default.Waterfall.GradientStops.ToArray(), settings.Waterfall.GradientStops.ToArray());
        Assert.Equal(MeasurementSettings.Default.Loudness.GradientStops.ToArray(), settings.Loudness.GradientStops.ToArray());
        Assert.Equal(LegacyJson, await File.ReadAllTextAsync(legacy));
        await File.WriteAllTextAsync(legacy, "not json");
        Assert.Equal(settings, await store.LoadStartupSettingsAsync());
        Assert.True(File.Exists(legacy));
    }

    [Fact]
    public async Task PartialLegacyMigrationPreservesValidFieldsAndDefaultsInvalidOrMissingFields()
    {
        var store = CreateCatalogStore();
        await File.WriteAllTextAsync(store.CatalogPath + ".legacy", "{\"Waterfall\":{\"DisplayFloorDb\":-85,\"DisplayOffsetDb\":\"bad\",\"PaletteColor\":\"invalid\"},\"Phase\":{\"Gain\":99},\"Rta\":{\"Source\":999,\"ScaleRangeDb\":80}}");
        var loaded = await store.LoadStartupSettingsAsync();
        Assert.Equal(-85, loaded.Waterfall.DisplayFloorDb);
        Assert.Equal(0, loaded.Waterfall.DisplayOffsetDb);
        Assert.Equal(1, loaded.Phase.Gain);
        Assert.Equal(RtaChannelMode.Mono, loaded.Rta.Source);
        Assert.Equal(80, loaded.Rta.ScaleRangeDb);
        Assert.True(MeasurementSettingsValidator.IsValid(loaded));
    }

    [Fact]
    public async Task FailedReplacementLeavesPreviousCatalogReadableAndCleansTemporaryFile()
    {
        var store = CreateCatalogStore();
        await store.SaveCatalogAsync(SettingsProfileCatalog.Default);
        var original = await File.ReadAllTextAsync(store.CatalogPath);
        using (var locked = new FileStream(store.CatalogPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => store.SaveCatalogAsync(SettingsProfileCatalog.Default.SaveAsProfile("Studio", SettingsProfileCatalogTests.ChangedSettings)));
        }
        Assert.Equal(original, await File.ReadAllTextAsync(store.CatalogPath));
        Assert.Single((await store.LoadCatalogAsync()).Profiles);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(store.CatalogPath)!, "*.tmp"));
    }

    [Fact]
    public async Task CancelledCatalogWriteLeavesPreviousCatalogReadable()
    {
        var store = CreateCatalogStore();
        await store.SaveCatalogAsync(SettingsProfileCatalog.Default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveCatalogAsync(SettingsProfileCatalog.Default.SaveAsProfile("Studio", SettingsProfileCatalogTests.ChangedSettings), cancellation.Token));
        Assert.Single((await store.LoadCatalogAsync()).Profiles);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(store.CatalogPath)!, "*.tmp"));
    }

    [Fact]
    public async Task CatalogReadFailuresDoNotPreventStartupWhenLoggerThrows()
    {
        var store = CreateCatalogStore(_ => throw new IOException("Logger unavailable"));
        await File.WriteAllTextAsync(store.CatalogPath, "bad");
        Assert.Equal(MeasurementSettings.Default, await store.LoadStartupSettingsAsync());
        File.Delete(store.CatalogPath);
        Directory.CreateDirectory(store.CatalogPath);
        Assert.Equal(MeasurementSettings.Default, await store.LoadStartupSettingsAsync());
    }

    private static SettingsStore CreateCatalogStore(Action<string>? diagnostic = null)
    {
        var directory = CreateSettingsDirectory();
        return new SettingsStore(directory, diagnostic, Path.Combine(directory, "profiles.json.legacy"));
    }

    private const string LegacyJson = """
        {
          "Analyzer":{"DisplayFloorDb":-115,"CursorColor":"#112233","TextColor":"#445566","FftSize":8192},
          "Waterfall":{"DisplayFloorDb":-95,"DisplayOffsetDb":7,"PaletteColor":"#123456"},
          "Meters":{"DisplayRangeDb":-72,"MeterColor":"#234567","OverloadColor":"#345678"},
          "Loudness":{"HistorySeconds":120,"Metric":1,"AutoScale":false,"SpanLufs":12,"CentreLufs":-18},
          "Rta":{"Source":2,"Resolution":1,"ScaleTopDb":3,"ScaleRangeDb":72,"TargetLineDb":-24},
          "Phase":{"Gain":2}
        }
        """;

    private static string CreateSettingsDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AAAnalyzerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
