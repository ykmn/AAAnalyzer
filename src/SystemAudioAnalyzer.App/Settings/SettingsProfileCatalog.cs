using System.Collections.Immutable;

namespace SystemAudioAnalyzer.App.Settings;

public sealed record SettingsProfileCatalog
{
    public const int CurrentSchemaVersion = 1;

    public SettingsProfileCatalog(int SchemaVersion, string DefaultProfileId, IReadOnlyList<SettingsProfile> Profiles)
    {
        if (SchemaVersion != CurrentSchemaVersion)
            throw new ArgumentException("Unsupported profile catalog schema.", nameof(SchemaVersion));
        ArgumentNullException.ThrowIfNull(Profiles);
        var snapshots = Profiles.ToImmutableArray();
        if (snapshots.IsEmpty || snapshots.Any(p => p is null || string.IsNullOrWhiteSpace(p.Id)
            || string.IsNullOrWhiteSpace(p.Name) || p.Settings is null || !MeasurementSettingsValidator.IsValid(p.Settings)))
            throw new ArgumentException("Profiles must have an ID, name and valid settings snapshot.", nameof(Profiles));
        if (snapshots.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != snapshots.Length
            || snapshots.Select(p => p.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshots.Length)
            throw new ArgumentException("Profile IDs and names must be unique.", nameof(Profiles));
        if (!snapshots.Any(p => p.Id == DefaultProfileId))
            throw new ArgumentException("The Default profile must exist.", nameof(DefaultProfileId));
        this.SchemaVersion = SchemaVersion;
        this.DefaultProfileId = DefaultProfileId;
        this.Profiles = snapshots;
    }

    public int SchemaVersion { get; }
    public string DefaultProfileId { get; }
    public IReadOnlyList<SettingsProfile> Profiles { get; }
    public static SettingsProfileCatalog Default { get; } = new(CurrentSchemaVersion, "default", [new("default", "Default", MeasurementSettings.Default)]);

    public SettingsProfileCatalog SaveProfile(string id, MeasurementSettings settings)
    {
        RequireProfile(id);
        return new(SchemaVersion, DefaultProfileId, Profiles.Select(p => p.Id == id ? p with { Settings = settings } : p).ToArray());
    }

    public SettingsProfileCatalog SaveAsProfile(string name, MeasurementSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(SchemaVersion, DefaultProfileId, Profiles.Append(new(Guid.NewGuid().ToString("N"), name.Trim(), settings)).ToArray());
    }

    public SettingsProfileCatalog DeleteProfile(string id)
    {
        RequireProfile(id);
        if (id == DefaultProfileId) throw new InvalidOperationException("The Default profile cannot be deleted.");
        return new(SchemaVersion, DefaultProfileId, Profiles.Where(p => p.Id != id).ToArray());
    }

    public SettingsProfileCatalog SetDefaultProfile(string id)
    {
        RequireProfile(id);
        return new(SchemaVersion, id, Profiles);
    }

    private void RequireProfile(string id)
    {
        if (!Profiles.Any(p => p.Id == id)) throw new ArgumentException("Profile does not exist.", nameof(id));
    }
}
