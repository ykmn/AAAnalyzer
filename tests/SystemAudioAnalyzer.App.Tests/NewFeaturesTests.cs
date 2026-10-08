using System.Text.Json.Nodes;
using SystemAudioAnalyzer.App.Rendering;
using SystemAudioAnalyzer.App.Services;
using SystemAudioAnalyzer.App.Settings;
using SystemAudioAnalyzer.App.ViewModels;
using SystemAudioAnalyzer.Core;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class NewFeaturesTests
{
    [Fact]
    public void CorrelationIsOneForMonoMinusOneForInvertedAndNullForSilence()
    {
        Assert.Equal(1d, MeterRailLayout.CalculateCorrelation([(0.5f, 0.5f), (-0.2f, -0.2f)])!.Value, 6);
        Assert.Equal(-1d, MeterRailLayout.CalculateCorrelation([(0.5f, -0.5f), (-0.2f, 0.2f)])!.Value, 6);
        Assert.Equal(0d, MeterRailLayout.CalculateCorrelation([(1f, 1f), (1f, -1f)])!.Value, 6);
        Assert.Null(MeterRailLayout.CalculateCorrelation([(0f, 0f)]));
        Assert.Null(MeterRailLayout.CalculateCorrelation(null));
    }

    [Fact]
    public void PhaseMeterSitsUnderTheCaptionInsideTheRail()
    {
        var layout = MeterRailLayout.Calculate(WorkspaceLayout.PeakRailWidth, 520);

        Assert.True(layout.LufsCaption.Bottom <= layout.PhaseValues.Top);
        Assert.True(layout.PhaseValues.Bottom <= layout.PhaseBar.Top);
        Assert.True(layout.PhaseBar.Bottom <= layout.PhaseScale.Top);
        Assert.True(layout.PhaseScale.Bottom <= 520);
    }

    [Fact]
    public void LoudnessTargetDefaultsToMinus23AndStepsWithinRange()
    {
        var defaults = MeasurementSettings.Default;

        Assert.Equal(-23, defaults.Loudness.TargetLufs);
        Assert.Equal(-24, ToolbarSettingsActions.WithLoudnessTarget(defaults, -1).Loudness.TargetLufs);
        Assert.Equal(0, ToolbarSettingsActions.WithLoudnessTarget(defaults, 100).Loudness.TargetLufs);
        Assert.Equal(-70, ToolbarSettingsActions.WithLoudnessTarget(defaults, -100).Loudness.TargetLufs);
    }

    [Fact]
    public async Task CatalogWrittenBeforeTheLoudnessTargetExistedStillLoads()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AAAnalyzerTests", Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(directory, null, Path.Combine(directory, "legacy.json"));
        await store.SaveCatalogAsync(SettingsProfileCatalog.Default);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(store.CatalogPath))!;
        foreach (var profile in json["Profiles"]!.AsArray()) profile!["Settings"]!["Loudness"]!.AsObject().Remove("TargetLufs");
        await File.WriteAllTextAsync(store.CatalogPath, json.ToJsonString());

        var loaded = await store.LoadCatalogAsync();

        Assert.Equal(-23, loaded.Profiles[0].Settings.Loudness.TargetLufs);
    }

    [Fact]
    public async Task StreamHistoryRoundTripsAndKeepsSixtyEntries()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AAAnalyzerTests", Guid.NewGuid().ToString("N"));
        var store = new StreamHistoryStore(directory);
        Assert.Empty(await store.LoadAsync());

        await store.SaveAsync(Enumerable.Range(0, 80).Select(index => $"http://radio.example/{index}"));
        var loaded = await store.LoadAsync();

        Assert.Equal(60, loaded.Count);
        Assert.Equal("http://radio.example/0", loaded[0]);
    }

    [Fact]
    public async Task OpeningAStreamMovesItsUrlToTheTopOnceAndClearEmptiesTheList()
    {
        var controller = new Controller();
        var viewModel = new MainViewModel(controller, []);
        var saved = 0;
        viewModel.StreamHistoryChanged += (_, _) => saved++;
        viewModel.SelectedSourceMode = SourceMode.Stream;

        foreach (var url in new[] { "http://a.example/1", "http://b.example/2", "http://a.example/1" })
        {
            viewModel.StreamUrl = url;
            await viewModel.StartCommand.ExecuteAsync();
            await viewModel.StopCommand.ExecuteAsync();
        }

        Assert.Equal(["http://a.example/1", "http://b.example/2"], viewModel.StreamHistory);
        viewModel.ClearStreamHistory();
        Assert.Empty(viewModel.StreamHistory);
        Assert.True(saved >= 3);
    }

    [Fact]
    public async Task StreamStatusShowsBufferingPercentUntilAudioArrives()
    {
        var controller = new Controller();
        var viewModel = new MainViewModel(controller, []);
        viewModel.SelectedSourceMode = SourceMode.Stream;
        viewModel.StreamUrl = "http://radio.example/live";

        await viewModel.StartCommand.ExecuteAsync();
        controller.Publish(new AudioSourceStateChangedEventArgs(AudioSourceState.Buffering, 42));

        Assert.Equal("Buffering the stream… 42%", viewModel.StatusText);
        Assert.Equal(AnalysisRunState.Starting, viewModel.RunState);
    }

    private sealed class Controller : IAnalyzerController
    {
        public event EventHandler<AnalysisFrame>? FrameAvailable { add { } remove { } }
        public event EventHandler<AudioSourceStateChangedEventArgs>? SourceStateChanged;
        public void Publish(AudioSourceStateChangedEventArgs args) => SourceStateChanged?.Invoke(this, args);
        public Task StartAsync(SourceSelection selection, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void ResetTruePeak(int channel) { }
        public void ResetTruePeakMaximum(int channel) { }
        public void ResetTruePeakOverload(int channel) { }
        public void ResetLoudness() { }
        public void SetAnalysisConfiguration(AnalysisConfiguration configuration) { }
        public void SetLoudnessIntegratedWindow(int seconds) { }
    }
}

public sealed class LocalizationTests
{
    [Fact]
    public void EveryKeyHasBothLanguagesAndPlaceholdersMatch()
    {
        var table = (IReadOnlyDictionary<string, (string English, string Russian)>)typeof(SystemAudioAnalyzer.App.Localization.Localizer).Assembly
            .GetType("SystemAudioAnalyzer.App.Localization.Strings")!.GetField("Table")!.GetValue(null)!;

        Assert.All(table, pair =>
        {
            Assert.False(string.IsNullOrWhiteSpace(pair.Value.English), pair.Key);
            Assert.False(string.IsNullOrWhiteSpace(pair.Value.Russian), pair.Key);
            var placeholders = new Func<string, string[]>(text => System.Text.RegularExpressions.Regex.Matches(text, @"\{\d\}").Select(match => match.Value).OrderBy(value => value).ToArray());
            Assert.Equal(placeholders(pair.Value.English), placeholders(pair.Value.Russian));
        });
    }

    [Fact]
    public void SwitchingLanguageChangesTextsAndRaisesTheEvent()
    {
        // A private instance: the shared one is read by tests that run in parallel.
        var localizer = new SystemAudioAnalyzer.App.Localization.Localizer();
        var raised = 0;
        localizer.LanguageChanged += (_, _) => raised++;

        localizer.Language = SystemAudioAnalyzer.App.Localization.AppLanguage.Russian;
        Assert.Equal("Настройки", localizer["SettingsTitle"]);
        localizer.Language = SystemAudioAnalyzer.App.Localization.AppLanguage.English;
        Assert.Equal("Settings", localizer["SettingsTitle"]);
        Assert.Equal(2, raised);
    }

    [Fact]
    public void LanguageIsStoredInTheDataFolder()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AAAnalyzerTests", Guid.NewGuid().ToString("N"));
        var preferences = new SystemAudioAnalyzer.App.Localization.AppPreferences(directory);

        preferences.SaveLanguage(SystemAudioAnalyzer.App.Localization.AppLanguage.Russian);

        Assert.Equal(SystemAudioAnalyzer.App.Localization.AppLanguage.Russian, new SystemAudioAnalyzer.App.Localization.AppPreferences(directory).LoadLanguage());
    }
}

public sealed class ProfileCompatibilityTests
{
    [Fact]
    public async Task ProfilesSavedWithTheRemovedPaletteColorStillLoad()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AAAnalyzerTests", Guid.NewGuid().ToString("N"));
        var store = new SystemAudioAnalyzer.App.Settings.SettingsStore(directory, null, Path.Combine(directory, "legacy.json"));
        await store.SaveCatalogAsync(SystemAudioAnalyzer.App.Settings.SettingsProfileCatalog.Default);
        var json = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(store.CatalogPath))!;
        foreach (var profile in json["Profiles"]!.AsArray()) profile!["Settings"]!["Waterfall"]!["PaletteColor"] = "#06B6D4";
        await File.WriteAllTextAsync(store.CatalogPath, json.ToJsonString());

        var loaded = await store.LoadCatalogAsync();

        Assert.Equal("Default", loaded.Profiles[0].Name);
        Assert.Equal(-110, loaded.Profiles[0].Settings.Waterfall.DisplayFloorDb);
    }
}

public sealed class RtaChannelAlignmentTests
{
    private static int PeakBand(SystemAudioAnalyzer.Core.Spectrum spectrum, SystemAudioAnalyzer.App.ViewModels.RtaResolution resolution, out double centerHz)
    {
        var bands = SystemAudioAnalyzer.App.Rendering.RtaBandAggregator.Aggregate(spectrum, resolution);
        var peak = 0;
        for (var index = 1; index < bands.Count; index++) if (bands[index].Magnitude > bands[peak].Magnitude) peak = index;
        centerHz = bands[peak].CenterHz;
        return peak;
    }

    [Theory]
    [InlineData(1_000.0, true, SystemAudioAnalyzer.App.ViewModels.RtaResolution.OneTwelfth)]
    [InlineData(3_150.0, false, SystemAudioAnalyzer.App.ViewModels.RtaResolution.OneThird)]
    [InlineData(120.0, true, SystemAudioAnalyzer.App.ViewModels.RtaResolution.OneSixth)]
    public void MonoLeftAndRightPutTheSameToneInTheSameBand(double toneHz, bool toneInBothChannels, SystemAudioAnalyzer.App.ViewModels.RtaResolution resolution)
    {
        var format = new SystemAudioAnalyzer.Core.AudioFormat(44_100, 2);
        var analyzer = new SystemAudioAnalyzer.Core.SpectrumAnalyzer(8192, SystemAudioAnalyzer.Core.SpectrumWindow.Blackman);
        var samples = new float[8192 * 2 * 2];
        for (var frame = 0; frame < samples.Length / 2; frame++)
        {
            var value = (float)(0.5 * Math.Sin(2 * Math.PI * toneHz * frame / format.SampleRate));
            samples[frame * 2] = value;
            samples[(frame * 2) + 1] = toneInBothChannels ? value : 0f;
        }

        Assert.True(analyzer.TryProcessStereo(samples, format, out var stereo));

        var mono = PeakBand(stereo!.Mono, resolution, out var monoCenter);
        var left = PeakBand(stereo.Left, resolution, out var leftCenter);
        Assert.Equal(mono, left);
        Assert.Equal(monoCenter, leftCenter);
        if (toneInBothChannels)
        {
            Assert.Equal(mono, PeakBand(stereo.Right, resolution, out _));
        }

        // The peak band must contain the tone: its centre is within half a band of the tone's frequency.
        var bandsPerOctave = resolution switch { SystemAudioAnalyzer.App.ViewModels.RtaResolution.One => 1, SystemAudioAnalyzer.App.ViewModels.RtaResolution.OneThird => 3, SystemAudioAnalyzer.App.ViewModels.RtaResolution.OneSixth => 6, _ => 12 };
        Assert.InRange(Math.Abs(Math.Log2(monoCenter / toneHz)), 0, 0.5 / bandsPerOctave + 1e-9);
    }
}
