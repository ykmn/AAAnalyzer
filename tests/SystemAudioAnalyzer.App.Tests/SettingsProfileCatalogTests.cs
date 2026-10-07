using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class SettingsProfileCatalogTests
{
    [Fact]
    public void SaveAsAddsFullSnapshotWithoutChangingDefaultOrOriginal()
    {
        var original = SettingsProfileCatalog.Default;
        var settings = ChangedSettings;
        var saved = original.SaveAsProfile("Studio", settings);

        Assert.Single(original.Profiles);
        Assert.Equal(original.DefaultProfileId, saved.DefaultProfileId);
        Assert.Equal(2, saved.Profiles.Count);
        Assert.Equal(settings, saved.Profiles.Single(p => p.Name == "Studio").Settings);
        Assert.NotEqual(saved.Profiles[0].Id, saved.Profiles[1].Id);
    }

    [Fact]
    public void SaveOverwritesOnlySelectedProfile()
    {
        var original = SettingsProfileCatalog.Default.SaveAsProfile("Studio", MeasurementSettings.Default);
        var id = original.Profiles.Single(p => p.Name == "Studio").Id;
        var saved = original.SaveProfile(id, ChangedSettings);

        Assert.Equal(MeasurementSettings.Default, original.Profiles.Single(p => p.Id == id).Settings);
        Assert.Equal(ChangedSettings, saved.Profiles.Single(p => p.Id == id).Settings);
        Assert.Equal("Studio", saved.Profiles.Single(p => p.Id == id).Name);
        Assert.Equal(original.Profiles[0], saved.Profiles[0]);
    }

    [Fact]
    public void SetDefaultSelectsExistingProfileWithoutSavingDraft()
    {
        var original = SettingsProfileCatalog.Default.SaveAsProfile("Studio", ChangedSettings);
        var id = original.Profiles[1].Id;
        var updated = original.SetDefaultProfile(id);

        Assert.NotEqual(id, original.DefaultProfileId);
        Assert.Equal(id, updated.DefaultProfileId);
        Assert.Equal(original.Profiles.ToArray(), updated.Profiles.ToArray());
    }

    [Fact]
    public void DeleteRemovesNonDefaultAndPreservesOriginal()
    {
        var original = SettingsProfileCatalog.Default.SaveAsProfile("Studio", ChangedSettings);
        var deleted = original.DeleteProfile(original.Profiles[1].Id);
        Assert.Equal(2, original.Profiles.Count);
        Assert.Single(deleted.Profiles);
        Assert.Equal(original.DefaultProfileId, deleted.DefaultProfileId);
    }

    [Fact]
    public void DeleteRefusesCurrentDefaultEvenAfterDefaultIsChanged()
    {
        var catalog = SettingsProfileCatalog.Default.SaveAsProfile("Studio", ChangedSettings);
        Assert.Throws<InvalidOperationException>(() => catalog.DeleteProfile(catalog.DefaultProfileId));
        catalog = catalog.SetDefaultProfile(catalog.Profiles[1].Id);
        Assert.Throws<InvalidOperationException>(() => catalog.DeleteProfile(catalog.DefaultProfileId));
        Assert.Single(catalog.DeleteProfile(catalog.Profiles[0].Id).Profiles);
    }

    [Fact]
    public void OperationsRejectUnknownProfileIds()
    {
        var catalog = SettingsProfileCatalog.Default;
        Assert.Throws<ArgumentException>(() => catalog.SaveProfile("missing", ChangedSettings));
        Assert.Throws<ArgumentException>(() => catalog.SetDefaultProfile("missing"));
        Assert.Throws<ArgumentException>(() => catalog.DeleteProfile("missing"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Default")]
    [InlineData("default")]
    [InlineData(" Default ")]
    public void SaveAsRejectsEmptyOrDuplicateNames(string name) =>
        Assert.Throws<ArgumentException>(() => SettingsProfileCatalog.Default.SaveAsProfile(name, ChangedSettings));

    [Fact]
    public void CatalogRejectsDuplicateIdsNamesMissingDefaultAndUnsupportedSchema()
    {
        var first = new SettingsProfile("one", "One", MeasurementSettings.Default);
        Assert.Throws<ArgumentException>(() => new SettingsProfileCatalog(1, "one", [first, first with { Name = "Two" }]));
        Assert.Throws<ArgumentException>(() => new SettingsProfileCatalog(1, "one", [first, first with { Id = "two", Name = "one" }]));
        Assert.Throws<ArgumentException>(() => new SettingsProfileCatalog(1, "absent", [first]));
        Assert.Throws<ArgumentException>(() => new SettingsProfileCatalog(1, "one", []));
        Assert.Throws<ArgumentException>(() => new SettingsProfileCatalog(99, "one", [first]));
    }

    [Fact]
    public void CatalogAndOperationsRejectInvalidSnapshots()
    {
        var invalid = MeasurementSettings.Default with { Phase = new PhaseDisplaySettings(99) };
        Assert.Throws<ArgumentException>(() => new SettingsProfileCatalog(1, "one", [new("one", "One", invalid)]));
        Assert.Throws<ArgumentException>(() => SettingsProfileCatalog.Default.SaveProfile(SettingsProfileCatalog.Default.DefaultProfileId, invalid));
        Assert.Throws<ArgumentException>(() => SettingsProfileCatalog.Default.SaveAsProfile("Invalid", invalid));
    }

    [Fact]
    public void CatalogCopiesMutableInputList()
    {
        var profiles = new List<SettingsProfile> { new("one", "One", MeasurementSettings.Default) };
        var catalog = new SettingsProfileCatalog(1, "one", profiles);
        profiles.Clear();
        Assert.Single(catalog.Profiles);
        Assert.False(catalog.Profiles is SettingsProfile[]);
        Assert.False(catalog.Profiles is List<SettingsProfile>);
    }

    internal static MeasurementSettings ChangedSettings => MeasurementSettings.Default with
    {
        Analyzer = MeasurementSettings.Default.Analyzer with { FftSize = 4096, WindowFunction = AnalyzerWindowFunction.Hann },
        Waterfall = MeasurementSettings.Default.Waterfall with { GradientStops = [new(-100, "Blue"), new(-20, "Cyan")] },
        Meters = MeasurementSettings.Default.Meters with { AttackMs = 17, ShowRmsBars = true },
        Loudness = MeasurementSettings.Default.Loudness with { GradientStops = [new(-30, "Blue"), new(-5, "Red")] },
        Rta = MeasurementSettings.Default.Rta with { AveragingCount = 99 },
        Phase = new PhaseDisplaySettings(2),
    };
}
