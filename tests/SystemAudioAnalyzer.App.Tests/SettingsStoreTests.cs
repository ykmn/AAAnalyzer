using SystemAudioAnalyzer.App.Settings;
using SystemAudioAnalyzer.App.ViewModels;

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

    private static string CreateSettingsDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AAAnalyzerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
