using SystemAudioAnalyzer.App.Settings;
using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class ToolbarSettingsPersisterTests
{
    private static SettingsStore CreateStore() => new(Path.Combine(Path.GetTempPath(), "AAAnalyzerTests", Guid.NewGuid().ToString("N")), null,
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "legacy.json"));

    [Fact]
    public async Task ToolbarValuesAreWrittenToTheDefaultProfileAndSurviveReload()
    {
        var store = CreateStore();
        var persister = new ToolbarSettingsPersister(store, _ => { });
        var runtime = MeasurementSettings.Default with
        {
            Rta = MeasurementSettings.Default.Rta with { TargetLineDb = -30, AveragingCount = 80 },
            Analyzer = MeasurementSettings.Default.Analyzer with { Gain = 3 },
        };

        await persister.PersistAsync(runtime);

        var catalog = await store.LoadCatalogAsync();
        var profile = catalog.Profiles.Single(p => p.Id == catalog.DefaultProfileId);
        Assert.Equal(-30, profile.Settings.Rta.TargetLineDb);
        Assert.Equal(80, profile.Settings.Rta.AveragingCount);
        Assert.Equal(MeasurementSettings.Default.Analyzer, profile.Settings.Analyzer);
        Assert.Equal(profile.Settings, await store.LoadStartupSettingsAsync());
    }

    [Fact]
    public async Task RapidSuccessiveChangesAreAppliedInOrder()
    {
        var store = CreateStore();
        var persister = new ToolbarSettingsPersister(store, _ => { });
        var tasks = Enumerable.Range(1, 8)
            .Select(step => persister.PersistAsync(MeasurementSettings.Default with
            {
                Rta = MeasurementSettings.Default.Rta with { AveragingCount = 10 * step },
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        var catalog = await store.LoadCatalogAsync();
        Assert.Equal(80, catalog.Profiles.Single(p => p.Id == catalog.DefaultProfileId).Settings.Rta.AveragingCount);
    }

    [Fact]
    public async Task StoreFailuresAreReportedInsteadOfThrown()
    {
        var blockedDirectory = Path.Combine(Path.GetTempPath(), "AAAnalyzerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(blockedDirectory)!);
        File.WriteAllText(blockedDirectory, "not a directory");
        var store = new SettingsStore(blockedDirectory, null, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "legacy.json"));
        Exception? reported = null;
        var persister = new ToolbarSettingsPersister(store, exception => reported = exception);

        await persister.PersistAsync(MeasurementSettings.Default with { Rta = MeasurementSettings.Default.Rta with { TargetLineDb = -30 } });

        Assert.NotNull(reported);
    }
}
