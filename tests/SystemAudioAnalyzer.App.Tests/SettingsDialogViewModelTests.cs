using SystemAudioAnalyzer.App.Settings;
using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class SettingsDialogViewModelTests
{
    [Fact]
    public void DirtyDraftRequiresConfirmationBeforeProfileSwitch()
    {
        var studio = SettingsProfileCatalog.Default.SaveAsProfile("Studio", ChangedSettings);
        var store = CreateStore();
        var accepted = false;
        var viewModel = new SettingsDialogViewModel(store, studio, MeasurementSettings.Default,
            InstrumentTab.Analyzer, () => accepted);
        viewModel.PhaseGain = 1.5;

        Assert.False(viewModel.SelectProfile(studio.Profiles.Single(p => p.Name == "Studio").Id));
        Assert.Equal("default", viewModel.SelectedProfileId);
        Assert.Equal(1.5, viewModel.Current.Phase.Gain);

        accepted = true;
        Assert.True(viewModel.SelectProfile(studio.Profiles.Single(p => p.Name == "Studio").Id));
        Assert.Equal(ChangedSettings, viewModel.Current);
        Assert.Equal(studio.Profiles.Single(p => p.Name == "Studio").Id, viewModel.SelectedProfileId);
    }

    [Fact]
    public async Task ApplyEmitsRuntimeSnapshotWithoutPersistingProfile()
    {
        var store = CreateStore();
        await store.SaveCatalogAsync(SettingsProfileCatalog.Default);
        var viewModel = CreateViewModel(store);
        MeasurementSettings? applied = null;
        viewModel.Applied += settings => applied = settings;
        viewModel.PhaseGain = 1.5;

        var result = viewModel.Apply();

        Assert.Equal(viewModel.Current, result);
        Assert.Equal(result, applied);
        Assert.Equal(MeasurementSettings.Default, (await store.LoadCatalogAsync()).Profiles.Single().Settings);
        Assert.False(File.Exists(store.SettingsPath));
    }

    [Fact]
    public async Task SavePersistsOnlyTheSelectedProfile()
    {
        var store = CreateStore();
        var catalog = SettingsProfileCatalog.Default.SaveAsProfile("Studio", ChangedSettings);
        await store.SaveCatalogAsync(catalog);
        var studio = catalog.Profiles.Single(p => p.Name == "Studio");
        var viewModel = new SettingsDialogViewModel(store, catalog, MeasurementSettings.Default, InstrumentTab.Waterfall);
        Assert.True(viewModel.SelectProfile(studio.Id));
        viewModel.PhaseGain = 2;

        await viewModel.SaveAsync();

        var saved = await store.LoadCatalogAsync();
        Assert.Equal(MeasurementSettings.Default, saved.Profiles.Single(p => p.Name == "Default").Settings);
        Assert.Equal(2, saved.Profiles.Single(p => p.Name == "Studio").Settings.Phase.Gain);
        Assert.False(File.Exists(store.SettingsPath));
    }

    [Fact]
    public async Task SaveAsCreatesAndSelectsNewProfileWithoutChangingDefault()
    {
        var store = CreateStore();
        await store.SaveCatalogAsync(SettingsProfileCatalog.Default);
        var viewModel = CreateViewModel(store);
        viewModel.PhaseGain = 1.5;

        await viewModel.SaveAsAsync("Studio");

        var saved = await store.LoadCatalogAsync();
        Assert.Equal("default", saved.DefaultProfileId);
        Assert.Equal("Studio", saved.Profiles.Single(p => p.Id == viewModel.SelectedProfileId).Name);
        Assert.Equal(1.5, saved.Profiles.Single(p => p.Id == viewModel.SelectedProfileId).Settings.Phase.Gain);
    }

    [Fact]
    public async Task FailedProfileWriteLeavesTheEditSessionAndProfileListUntouched()
    {
        var blockedDirectory = Path.Combine(Path.GetTempPath(), "AAAnalyzerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(blockedDirectory)!);
        await File.WriteAllTextAsync(blockedDirectory, "not a directory");
        var store = new SettingsStore(blockedDirectory, null, blockedDirectory + ".legacy");
        var viewModel = CreateViewModel(store);
        viewModel.PhaseGain = 1.5;

        await Assert.ThrowsAnyAsync<IOException>(() => viewModel.SaveAsAsync("Studio"));

        Assert.Single(viewModel.Profiles);
        Assert.Equal("default", viewModel.SelectedProfileId);
        Assert.Equal(1.5, viewModel.Current.Phase.Gain);
        Assert.True(viewModel.HasUnsavedDraft);
    }

    [Fact]
    public async Task SetDefaultAndDeletePersistExplicitProfileOperations()
    {
        var store = CreateStore();
        var catalog = SettingsProfileCatalog.Default.SaveAsProfile("Studio", ChangedSettings);
        await store.SaveCatalogAsync(catalog);
        var viewModel = CreateViewModel(store, catalog);
        var studioId = catalog.Profiles.Single(p => p.Name == "Studio").Id;
        Assert.True(viewModel.SelectProfile(studioId));

        await viewModel.SetDefaultAsync();
        Assert.Equal(studioId, (await store.LoadCatalogAsync()).DefaultProfileId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.DeleteAsync());
        Assert.True(viewModel.SelectProfile("default"));
        await viewModel.SetDefaultAsync();
        Assert.True(viewModel.SelectProfile(studioId));
        await viewModel.DeleteAsync();
        Assert.Single((await store.LoadCatalogAsync()).Profiles);
        Assert.Equal("default", viewModel.SelectedProfileId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.DeleteAsync());
    }

    [Fact]
    public async Task ProfileChoicesMarkTheDesignatedDefaultAndRefreshAfterSetDefault()
    {
        var store = CreateStore();
        var catalog = SettingsProfileCatalog.Default.SaveAsProfile("Studio", ChangedSettings);
        await store.SaveCatalogAsync(catalog);
        var viewModel = CreateViewModel(store, catalog);
        var studioId = catalog.Profiles.Single(profile => profile.Name == "Studio").Id;

        Assert.Equal("Default (default)", viewModel.ProfileOptions.Single(choice => choice.Value == "default").Label);
        Assert.Equal("Studio", viewModel.ProfileOptions.Single(choice => choice.Value == studioId).Label);
        Assert.True(viewModel.SelectProfile(studioId));
        await viewModel.SetDefaultAsync();

        Assert.Equal("Default", viewModel.ProfileOptions.Single(choice => choice.Value == "default").Label);
        Assert.Equal("Studio (default)", viewModel.ProfileOptions.Single(choice => choice.Value == studioId).Label);
        Assert.Equal(2, viewModel.Profiles.Count);
    }

    [Fact]
    public async Task LoudnessDefaultActionCopiesOpeningRuntimeIntoStartupDefaultNotDraft()
    {
        var opening = ChangedSettings;
        var catalog = SettingsProfileCatalog.Default.SaveAsProfile("Studio", MeasurementSettings.Default);
        var store = CreateStore();
        await store.SaveCatalogAsync(catalog);
        var viewModel = new SettingsDialogViewModel(store, catalog, opening, InstrumentTab.Loudness);
        viewModel.PhaseGain = 3;

        await viewModel.SaveCurrentRuntimeAsDefaultAsync();

        var updated = await store.LoadCatalogAsync();
        Assert.Equal(opening, updated.Profiles.Single(p => p.Id == updated.DefaultProfileId).Settings);
        Assert.Equal(3, viewModel.Current.Phase.Gain);
        Assert.Equal(catalog.DefaultProfileId, viewModel.SelectedProfileId);
    }

    [Fact]
    public void CancelAfterApplyEmitsOpeningRuntimeSnapshot()
    {
        var opening = MeasurementSettings.Default with { Phase = new PhaseDisplaySettings(1.25) };
        var viewModel = new SettingsDialogViewModel(CreateStore(), SettingsProfileCatalog.Default,
            opening, InstrumentTab.Phase);
        viewModel.PhaseGain = 2;
        viewModel.Apply();
        MeasurementSettings? cancelled = null;
        viewModel.Cancelled += settings => cancelled = settings;

        var restored = viewModel.Cancel();

        Assert.Equal(opening, restored);
        Assert.Equal(opening, cancelled);
    }

    [Fact]
    public void OpensDefaultProfileAsDraftButCancelRestoresOpeningRuntimeSnapshot()
    {
        var openingRuntime = MeasurementSettings.Default with { Phase = new PhaseDisplaySettings(1.25) };
        var persistedDefault = MeasurementSettings.Default with { Phase = new PhaseDisplaySettings(1.75) };
        var catalog = SettingsProfileCatalog.Default.SaveProfile("default", persistedDefault);
        var viewModel = new SettingsDialogViewModel(CreateStore(), catalog, openingRuntime, InstrumentTab.Phase);

        Assert.Equal("default", viewModel.SelectedProfileId);
        Assert.Equal(persistedDefault, viewModel.Current);
        Assert.False(viewModel.HasUnsavedDraft);

        viewModel.PhaseGain = 2.25;
        var appliedRuntime = viewModel.Apply();
        var reopened = new SettingsDialogViewModel(CreateStore(), catalog, appliedRuntime, InstrumentTab.Phase);
        Assert.Equal(persistedDefault, reopened.Current);
        Assert.Equal(appliedRuntime, reopened.Cancel());
        Assert.Equal(openingRuntime, viewModel.Cancel());
    }

    [Fact]
    public void GradientEditingChangesOnlySelectedStopAndAddInterpolatesMidpoint()
    {
        var viewModel = CreateViewModel(CreateStore());
        viewModel.SelectedGradientKind = GradientKind.Waterfall;
        viewModel.SelectedGradientStop = viewModel.GradientStops[1];
        viewModel.UpdateSelectedGradientColor("#123456");

        Assert.Equal("#123456", viewModel.Current.Waterfall.GradientStops[1].Color);
        Assert.Equal("#2FA84F", viewModel.Current.Waterfall.GradientStops[2].Color);
        Assert.True(viewModel.AddGradientStop());
        Assert.Contains(viewModel.Current.Waterfall.GradientStops, stop => stop.LevelDb == -67.5);
    }

    [Fact]
    public void ApplyRejectsInvalidDraftWithValidationFeedback()
    {
        var viewModel = CreateViewModel(CreateStore());
        viewModel.AnalyzerGain = double.NaN;

        Assert.Throws<InvalidOperationException>(() => viewModel.Apply());
        Assert.Contains("Analyzer.Gain", viewModel.ValidationMessage);
    }

    private static SettingsDialogViewModel CreateViewModel(SettingsStore store, SettingsProfileCatalog? catalog = null) =>
        new(store, catalog ?? SettingsProfileCatalog.Default, MeasurementSettings.Default, InstrumentTab.Analyzer);

    private static SettingsStore CreateStore() => new(Path.Combine(Path.GetTempPath(), "AAAnalyzerTests", Guid.NewGuid().ToString("N")), null,
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "legacy.json"));

    private static MeasurementSettings ChangedSettings => MeasurementSettings.Default with
    {
        Phase = new PhaseDisplaySettings(2),
    };
}
