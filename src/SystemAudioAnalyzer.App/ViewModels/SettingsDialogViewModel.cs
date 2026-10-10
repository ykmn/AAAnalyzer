using SystemAudioAnalyzer.App.Localization;
using System.Collections.ObjectModel;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using SystemAudioAnalyzer.App.Rendering;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.ViewModels;

public enum GradientKind { Waterfall, Loudness }

/// <summary>A list entry; a localized one re-reads its label when the UI language changes.</summary>
public sealed class SettingsOption<T> : INotifyPropertyChanged
{
    private readonly string? _key;
    private readonly string _label;

    public SettingsOption(string label, T value)
    {
        _label = label;
        Value = value;
    }

    private SettingsOption(string key, T value, bool localized)
    {
        _key = key;
        _label = key;
        Value = value;
        System.Windows.WeakEventManager<Localizer, EventArgs>.AddHandler(Localizer.Instance, nameof(Localizer.LanguageChanged), OnLanguageChanged);
    }

    public static SettingsOption<T> Tr(string key, T value) => new(key, value, localized: true);

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Label => _key is null ? _label : Localizer.T(_key);

    public T Value { get; }

    private void OnLanguageChanged(object? sender, EventArgs eventArgs) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
}

public sealed class SettingsDialogViewModel : INotifyPropertyChanged
{
    private readonly SettingsStore _store;
    private readonly SettingsEditSession _session;
    private readonly MeasurementSettings _openingRuntimeSnapshot;
    private readonly Func<bool> _confirmDiscardDraft;
    private SettingsProfileCatalog _catalog;
    private string _selectedProfileId;
    private InstrumentTab _selectedPage;
    private string _profileName = string.Empty;
    private string _validationMessage = string.Empty;
    private GradientKind _selectedGradientKind;
    private ColorStop? _selectedGradientStop;

    public SettingsDialogViewModel(SettingsStore store, SettingsProfileCatalog catalog,
        MeasurementSettings openingRuntimeSnapshot, InstrumentTab selectedPage,
        Func<bool>? confirmDiscardDraft = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _openingRuntimeSnapshot = openingRuntimeSnapshot ?? throw new ArgumentNullException(nameof(openingRuntimeSnapshot));
        _selectedProfileId = catalog.DefaultProfileId;
        var selectedProfileSettings = catalog.Profiles.Single(profile => profile.Id == _selectedProfileId).Settings;
        _session = new SettingsEditSession(openingRuntimeSnapshot);
        _session.Replace(selectedProfileSettings);
        _selectedPage = selectedPage;
        _confirmDiscardDraft = confirmDiscardDraft ?? (() => false);
        GradientStops = [];
        SyncGradientStops();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<MeasurementSettings>? Applied;
    public event Action<MeasurementSettings>? Cancelled;

    public IReadOnlyList<SettingsProfile> Profiles => _catalog.Profiles;
    public IReadOnlyList<SettingsOption<string>> ProfileOptions => _catalog.Profiles
        .Select(profile => new SettingsOption<string>(
            profile.Name + (profile.Id == _catalog.DefaultProfileId ? Localizer.T("DefaultSuffix") : string.Empty), profile.Id))
        .ToArray();
    public IReadOnlyList<int> FftSizes { get; } = [512, 1024, 2048, 4096, 8192, 16384];
    public IReadOnlyList<AnalyzerWindowFunction> WindowFunctions { get; } = Enum.GetValues<AnalyzerWindowFunction>();
    public IReadOnlyList<AnalyzerFrequencyScale> FrequencyScales { get; } = Enum.GetValues<AnalyzerFrequencyScale>();
    public IReadOnlyList<AnalyzerAmplitudeScale> AmplitudeScales { get; } = Enum.GetValues<AnalyzerAmplitudeScale>();
    public IReadOnlyList<SettingsOption<LoudnessMetric>> LoudnessMetricOptions { get; } =
    [SettingsOption<LoudnessMetric>.Tr("Momentary", LoudnessMetric.Momentary), SettingsOption<LoudnessMetric>.Tr("ShortTerm", LoudnessMetric.ShortTerm), SettingsOption<LoudnessMetric>.Tr("Integrated", LoudnessMetric.Integrated)];
    public IReadOnlyList<SettingsOption<RtaChannelMode>> RtaSourceOptions { get; } =
    [SettingsOption<RtaChannelMode>.Tr("RtaMono", RtaChannelMode.Mono), new("L", RtaChannelMode.Left), new("R", RtaChannelMode.Right)];
    public IReadOnlyList<SettingsOption<RtaResolution>> RtaResolutionOptions { get; } =
    [SettingsOption<RtaResolution>.Tr("Oct1", RtaResolution.One), SettingsOption<RtaResolution>.Tr("Oct3", RtaResolution.OneThird), SettingsOption<RtaResolution>.Tr("Oct6", RtaResolution.OneSixth), SettingsOption<RtaResolution>.Tr("Oct12", RtaResolution.OneTwelfth)];
    public IReadOnlyList<SettingsOption<MeterFontSize>> MeterFontSizeOptions { get; } =
    [SettingsOption<MeterFontSize>.Tr("Small", MeterFontSize.Small), SettingsOption<MeterFontSize>.Tr("Medium", MeterFontSize.Medium), SettingsOption<MeterFontSize>.Tr("Large", MeterFontSize.Large)];
    public IReadOnlyList<SettingsOption<LufsScalePreset>> LufsScaleOptions { get; } =
    [SettingsOption<LufsScalePreset>.Tr("ScaleBroadcast", LufsScalePreset.Broadcast), SettingsOption<LufsScalePreset>.Tr("ScaleFull", LufsScalePreset.FullScale), SettingsOption<LufsScalePreset>.Tr("ScaleCustom", LufsScalePreset.Custom)];
    public ObservableCollection<ColorStop> GradientStops { get; }

    public MeasurementSettings Current => _session.Current;
    public bool HasUnsavedDraft => Current != SelectedProfile.Settings;
    public string SelectedProfileId
    {
        get => _selectedProfileId;
        set { if (!string.Equals(value, _selectedProfileId, StringComparison.Ordinal)) SelectProfile(value); }
    }
    public SettingsProfile SelectedProfile => _catalog.Profiles.Single(profile => profile.Id == _selectedProfileId);
    public string ProfileName { get => _profileName; set => SetField(ref _profileName, value ?? string.Empty); }
    public string ValidationMessage { get => _validationMessage; private set => SetField(ref _validationMessage, value); }
    public InstrumentTab SelectedPage { get => _selectedPage; set => SetField(ref _selectedPage, value); }
    public GradientKind SelectedGradientKind
    {
        get => _selectedGradientKind;
        set { if (SetField(ref _selectedGradientKind, value)) SyncGradientStops(); }
    }
    public ColorStop? SelectedGradientStop
    {
        get => _selectedGradientStop;
        set
        {
            if (SetField(ref _selectedGradientStop, value))
            {
                OnPropertyChanged(nameof(SelectedGradientLevel));
                OnPropertyChanged(nameof(SelectedGradientColor));
                OnPropertyChanged(nameof(CanDeleteGradientStop));
            }
        }
    }
    public double SelectedGradientLevel
    {
        get => SelectedGradientStop?.LevelDb ?? 0;
        set => UpdateSelectedGradientLevel(value);
    }
    public string SelectedGradientColor => SelectedGradientStop?.Color ?? "#FFFFFF";
    public bool CanDeleteGradientStop => CurrentStops.Length > 2;

    public int AnalyzerFftSize { get => Current.Analyzer.FftSize; set => Update(analyzer: Current.Analyzer with { FftSize = value }); }
    public AnalyzerWindowFunction AnalyzerWindowFunction { get => Current.Analyzer.WindowFunction; set => Update(analyzer: Current.Analyzer with { WindowFunction = value }); }
    public AnalyzerFrequencyScale AnalyzerFrequencyScale { get => Current.Analyzer.FrequencyScale; set => Update(analyzer: Current.Analyzer with { FrequencyScale = value }); }
    public AnalyzerAmplitudeScale AnalyzerAmplitudeScale { get => Current.Analyzer.AmplitudeScale; set => Update(analyzer: Current.Analyzer with { AmplitudeScale = value }); }
    public bool IsLogarithmicFrequencyScale { get => AnalyzerFrequencyScale == AnalyzerFrequencyScale.Logarithmic; set => AnalyzerFrequencyScale = value ? AnalyzerFrequencyScale.Logarithmic : AnalyzerFrequencyScale.Linear; }
    public bool IsLogarithmicAmplitudeScale { get => AnalyzerAmplitudeScale == AnalyzerAmplitudeScale.Logarithmic; set => AnalyzerAmplitudeScale = value ? AnalyzerAmplitudeScale.Logarithmic : AnalyzerAmplitudeScale.Linear; }
    public double AnalyzerFloorDb { get => Current.Analyzer.DisplayFloorDb; set => Update(analyzer: Current.Analyzer with { DisplayFloorDb = value }); }
    public double AnalyzerGain { get => Current.Analyzer.Gain; set => Update(analyzer: Current.Analyzer with { Gain = value }); }
    public string AnalyzerCursorColor { get => Current.Analyzer.CursorColor; set => Update(analyzer: Current.Analyzer with { CursorColor = value ?? string.Empty }); }
    public string AnalyzerTextColor { get => Current.Analyzer.TextColor; set => Update(analyzer: Current.Analyzer with { TextColor = value ?? string.Empty }); }

    // Clamped at the source (not just where it's consumed) so a value typed mid-edit, or loaded from a hand-edited
    // settings file, can never reach the live renderers below the scale's floor while analysis keeps redrawing.
    public double AnalyzerMaxFrequencyHz
    {
        get => Current.Analyzer.MaxFrequencyHz;
        set => Update(analyzer: Current.Analyzer with { MaxFrequencyHz = Math.Clamp(value, FrequencyScale.MinimumHertz + 1, 192_000) });
    }

    public string? AnalyzerPlaybackDeviceId { get => Current.Analyzer.PlaybackDeviceId; set => Update(analyzer: Current.Analyzer with { PlaybackDeviceId = string.IsNullOrEmpty(value) ? null : value }); }
    public int AnalyzerPlaybackBufferMs { get => Current.Analyzer.PlaybackBufferMs; set => Update(analyzer: Current.Analyzer with { PlaybackBufferMs = Math.Clamp(value, 0, 5000) }); }
    public IReadOnlyList<SettingsOption<string?>> PlaybackDeviceOptions { get; } = LoadPlaybackDevices();

    private static IReadOnlyList<SettingsOption<string?>> LoadPlaybackDevices()
    {
        var options = new List<SettingsOption<string?>> { SettingsOption<string?>.Tr("DefaultDevice", null) };
        try
        {
            options.AddRange(new NaudioAudioOutputDeviceProvider().GetActiveDevices().Select(device => new SettingsOption<string?>(device.Name, device.Id)));
        }
        catch (Exception)
        {
        }

        return options;
    }

    public double WaterfallFloorDb { get => Current.Waterfall.DisplayFloorDb; set => Update(waterfall: Current.Waterfall with { DisplayFloorDb = value }); }
    public double WaterfallOffsetDb { get => Current.Waterfall.DisplayOffsetDb; set => Update(waterfall: Current.Waterfall with { DisplayOffsetDb = value }); }

    public double WaterfallWindowSeconds
    {
        get => Current.Waterfall.WindowSeconds;
        set => Update(waterfall: Current.Waterfall with { WindowSeconds = Math.Clamp(value, 1, 300) });
    }

    public double MeterAttackMs { get => Current.Meters.AttackMs; set => Update(meters: Current.Meters with { AttackMs = value }); }
    public double MeterReleaseMs { get => Current.Meters.ReleaseMs; set => Update(meters: Current.Meters with { ReleaseMs = value }); }
    public double MeterPeakHoldMs { get => Current.Meters.PeakHoldMs; set => Update(meters: Current.Meters with { PeakHoldMs = value }); }
    public double MeterRangeDb { get => Current.Meters.DisplayRangeDb; set => Update(meters: Current.Meters with { DisplayRangeDb = value }); }
    public bool ShowClipAndPeakReadout { get => Current.Meters.ShowClipIndicator && Current.Meters.ShowPeakReadout; set => Update(meters: Current.Meters with { ShowClipIndicator = value, ShowPeakReadout = value }); }
    public bool DisableRmsBar { get => !Current.Meters.ShowRmsBars; set => Update(meters: Current.Meters with { ShowRmsBars = !value }); }
    public bool ShowDbScale { get => Current.Meters.ShowDbScale; set => Update(meters: Current.Meters with { ShowDbScale = value }); }
    public bool ShowLufsIndicator { get => Current.Meters.ShowLufsIndicator; set => Update(meters: Current.Meters with { ShowLufsIndicator = value }); }
    public LoudnessMetric MeterLufsMetric { get => Current.Meters.LufsMetric; set => Update(meters: Current.Meters with { LufsMetric = value }); }
    public int IntegratedWindowSeconds { get => Current.Meters.IntegratedWindowSeconds; set => Update(meters: Current.Meters with { IntegratedWindowSeconds = value }); }
    public bool ShowLkfsReadout { get => Current.Meters.ShowLkfsReadout; set => Update(meters: Current.Meters with { ShowLkfsReadout = value }); }
    public MeterFontSize MeterFontSize { get => Current.Meters.FontSize; set => Update(meters: Current.Meters with { FontSize = value }); }
    public LufsScalePreset LufsScale { get => Current.Meters.LufsScale; set => Update(meters: Current.Meters with { LufsScale = value }); }
    public double LufsScaleTop { get => Current.Meters.LufsScaleTop; set => Update(meters: Current.Meters with { LufsScaleTop = value }); }
    public double LufsScaleBottom { get => Current.Meters.LufsScaleBottom; set => Update(meters: Current.Meters with { LufsScaleBottom = value }); }
    public double LufsScaleStep { get => Current.Meters.LufsScaleStep; set => Update(meters: Current.Meters with { LufsScaleStep = value }); }
    public string PeakColor { get => Current.Meters.PeakColor; set => Update(meters: Current.Meters with { PeakColor = value ?? string.Empty }); }
    public string RmsColor { get => Current.Meters.RmsColor; set => Update(meters: Current.Meters with { RmsColor = value ?? string.Empty }); }
    public string LufsColor { get => Current.Meters.LufsColor; set => Update(meters: Current.Meters with { LufsColor = value ?? string.Empty }); }
    public string ClipColor { get => Current.Meters.ClipColor; set => Update(meters: Current.Meters with { ClipColor = value ?? string.Empty }); }
    public string MeterColor { get => Current.Meters.MeterColor; set => Update(meters: Current.Meters with { MeterColor = value ?? string.Empty }); }
    public string OverloadColor { get => Current.Meters.OverloadColor; set => Update(meters: Current.Meters with { OverloadColor = value ?? string.Empty }); }

    public int LoudnessHistorySeconds { get => Current.Loudness.HistorySeconds; set => Update(loudness: Current.Loudness with { HistorySeconds = value }); }
    public LoudnessMetric LoudnessMetric { get => Current.Loudness.Metric; set => Update(loudness: Current.Loudness with { Metric = value }); }
    public double LoudnessSpan { get => Current.Loudness.SpanLufs; set => Update(loudness: Current.Loudness with { SpanLufs = value, AutoScale = false }); }
    public double LoudnessTarget { get => Current.Loudness.TargetLufs; set => Update(loudness: Current.Loudness with { TargetLufs = value }); }
    public double LoudnessTargetRange { get => Current.Loudness.TargetRangeLu; set => Update(loudness: Current.Loudness with { TargetRangeLu = value }); }
    public double LoudnessCentre { get => Current.Loudness.CentreLufs; set => Update(loudness: Current.Loudness with { CentreLufs = value, AutoScale = false }); }

    public RtaChannelMode RtaSource { get => Current.Rta.Source; set => Update(rta: Current.Rta with { Source = value }); }
    public RtaResolution RtaResolution { get => Current.Rta.Resolution; set => Update(rta: Current.Rta with { Resolution = value }); }
    public int RtaAveragingCount { get => Current.Rta.AveragingCount; set => Update(rta: Current.Rta with { AveragingCount = value }); }
    public double RtaTiltDbPerOctave { get => Current.Rta.TiltDbPerOctave; set => Update(rta: Current.Rta with { TiltDbPerOctave = value }); }
    public double RtaReleaseDbPerSecond { get => Current.Rta.ReleaseDbPerSecond; set => Update(rta: Current.Rta with { ReleaseDbPerSecond = value }); }
    public double RtaScaleTop { get => Current.Rta.ScaleTopDb; set => Update(rta: Current.Rta with { ScaleTopDb = value }); }
    public double RtaScaleRange { get => Current.Rta.ScaleRangeDb; set => Update(rta: Current.Rta with { ScaleRangeDb = value }); }
    public double RtaTargetLine { get => Current.Rta.TargetLineDb; set => Update(rta: Current.Rta with { TargetLineDb = value }); }
    public double RtaTargetRange { get => Current.Rta.TargetRangeDb; set => Update(rta: Current.Rta with { TargetRangeDb = value }); }
    public bool ShowRtaPeakHoldCaps { get => Current.Rta.ShowPeakHoldCaps; set => Update(rta: Current.Rta with { ShowPeakHoldCaps = value }); }
    public double RtaPeakHoldMs { get => Current.Rta.PeakHoldMs; set => Update(rta: Current.Rta with { PeakHoldMs = value }); }
    public string RtaBarColor { get => Current.Rta.BarColor; set => Update(rta: Current.Rta with { BarColor = value ?? string.Empty }); }
    public string RtaPeakCapColor { get => Current.Rta.PeakCapColor; set => Update(rta: Current.Rta with { PeakCapColor = value ?? string.Empty }); }
    public string RtaTargetBandColor { get => Current.Rta.TargetBandColor; set => Update(rta: Current.Rta with { TargetBandColor = value ?? string.Empty }); }
    public double PhaseGain { get => Current.Phase.Gain; set => Update(phase: Current.Phase with { Gain = value }); }

    public bool SelectProfile(string profileId)
    {
        var profile = _catalog.Profiles.SingleOrDefault(item => item.Id == profileId)
            ?? throw new ArgumentException("Profile does not exist.", nameof(profileId));
        if (profile.Id == _selectedProfileId) return true;
        if (HasUnsavedDraft && !_confirmDiscardDraft())
        {
            OnPropertyChanged(nameof(SelectedProfileId));
            return false;
        }
        _selectedProfileId = profile.Id;
        _session.Replace(profile.Settings);
        ValidationMessage = string.Empty;
        NotifySettingsChanged();
        OnPropertyChanged(nameof(SelectedProfileId));
        OnPropertyChanged(nameof(SelectedProfile));
        OnPropertyChanged(nameof(Profiles));
        OnPropertyChanged(nameof(HasUnsavedDraft));
        return true;
    }

    public MeasurementSettings Apply()
    {
        ValidateDraft();
        var applied = _session.Apply();
        Applied?.Invoke(applied);
        OnPropertyChanged(nameof(HasUnsavedDraft));
        return applied;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        ValidateDraft();
        var updated = _catalog.SaveProfile(_selectedProfileId, Current);
        await _store.SaveCatalogAsync(updated, cancellationToken);
        SetCatalog(updated);
    }

    /// <summary>Writes the applied settings to the default profile (the startup config); named profiles stay untouched.</summary>
    public async Task PersistAppliedAsync(CancellationToken cancellationToken = default)
    {
        var updated = _catalog.SaveProfile(_catalog.DefaultProfileId, Current);
        await _store.SaveCatalogAsync(updated, cancellationToken);
        SetCatalog(updated);
    }

    public async Task SaveAsAsync(string name, CancellationToken cancellationToken = default)
    {
        ValidateDraft();
        var updated = _catalog.SaveAsProfile(name, Current);
        await _store.SaveCatalogAsync(updated, cancellationToken);
        _catalog = updated;
        _selectedProfileId = updated.Profiles[^1].Id;
        ProfileName = string.Empty;
        OnPropertyChanged(nameof(Profiles));
        OnPropertyChanged(nameof(ProfileOptions));
        OnPropertyChanged(nameof(SelectedProfileId));
        OnPropertyChanged(nameof(SelectedProfile));
        OnPropertyChanged(nameof(HasUnsavedDraft));
    }

    public async Task SetDefaultAsync(CancellationToken cancellationToken = default)
    {
        var updated = _catalog.SetDefaultProfile(_selectedProfileId);
        await _store.SaveCatalogAsync(updated, cancellationToken);
        SetCatalog(updated);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (_selectedProfileId == _catalog.DefaultProfileId)
            throw new InvalidOperationException("The Default profile cannot be deleted.");
        if (HasUnsavedDraft && !_confirmDiscardDraft()) return;
        var updated = _catalog.DeleteProfile(_selectedProfileId);
        await _store.SaveCatalogAsync(updated, cancellationToken);
        _catalog = updated;
        _selectedProfileId = updated.DefaultProfileId;
        _session.Replace(SelectedProfile.Settings);
        NotifySettingsChanged();
        OnPropertyChanged(nameof(Profiles));
        OnPropertyChanged(nameof(ProfileOptions));
        OnPropertyChanged(nameof(SelectedProfileId));
        OnPropertyChanged(nameof(SelectedProfile));
        OnPropertyChanged(nameof(HasUnsavedDraft));
    }

    public async Task SaveCurrentRuntimeAsDefaultAsync(CancellationToken cancellationToken = default)
    {
        var updated = _catalog.SaveProfile(_catalog.DefaultProfileId, _openingRuntimeSnapshot);
        await _store.SaveCatalogAsync(updated, cancellationToken);
        SetCatalog(updated);
    }

    public MeasurementSettings Cancel()
    {
        var restored = _session.Cancel();
        Cancelled?.Invoke(restored);
        NotifySettingsChanged();
        return restored;
    }

    public bool AddGradientStop()
    {
        var stops = CurrentStops;
        if (stops.Length < 2) return false;
        var index = SelectedGradientStop is null ? 0 : Array.IndexOf(stops.ToArray(), SelectedGradientStop);
        if (index < 0) index = 0;
        if (index >= stops.Length - 1) index = stops.Length - 2;
        var lower = stops[index];
        var upper = stops[index + 1];
        var level = (lower.LevelDb + upper.LevelDb) / 2;
        var color = ColorGradient.Sample(stops, level);
        var colorHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        ReplaceStops(stops.Append(new ColorStop(level, colorHex)).OrderBy(stop => stop.LevelDb).ToArray());
        SelectedGradientStop = CurrentStops.Single(stop => stop.LevelDb == level);
        return true;
    }

    public bool DeleteGradientStop()
    {
        var stops = CurrentStops;
        if (stops.Length <= 2 || SelectedGradientStop is null) return false;
        var index = Array.IndexOf(stops.ToArray(), SelectedGradientStop);
        if (index < 0) return false;
        var next = stops.Where((_, itemIndex) => itemIndex != index).ToArray();
        ReplaceStops(next);
        SelectedGradientStop = next[Math.Min(index, next.Length - 1)];
        return true;
    }

    public void UpdateSelectedGradientColor(string color)
    {
        if (SelectedGradientStop is null || string.IsNullOrWhiteSpace(color)) return;
        ReplaceSelectedStop(SelectedGradientStop with { Color = color });
    }

    public IReadOnlyList<string> ValidateDraft()
    {
        var errors = MeasurementSettingsValidator.Validate(Current);
        ValidationMessage = string.Join(" ", errors);
        if (errors.Count > 0) throw new InvalidOperationException("Settings are invalid: " + ValidationMessage);
        return errors;
    }

    private ImmutableArray<ColorStop> CurrentStops => SelectedGradientKind == GradientKind.Waterfall
        ? Current.Waterfall.GradientStops : Current.Loudness.GradientStops;

    private void UpdateSelectedGradientLevel(double level)
    {
        if (SelectedGradientStop is null) return;
        var old = SelectedGradientStop;
        var replacement = old with { LevelDb = level };
        var ordered = CurrentStops.Select(stop => stop == old ? replacement : stop).OrderBy(stop => stop.LevelDb).ToArray();
        ReplaceStops(ordered);
        SelectedGradientStop = replacement;
    }

    private void ReplaceSelectedStop(ColorStop replacement)
    {
        if (SelectedGradientStop is null) return;
        var old = SelectedGradientStop;
        var updated = CurrentStops.Select(stop => stop == old ? replacement : stop).ToArray();
        ReplaceStops(updated);
        SelectedGradientStop = replacement;
    }

    private void ReplaceStops(IReadOnlyList<ColorStop> stops)
    {
        if (SelectedGradientKind == GradientKind.Waterfall)
            Update(waterfall: Current.Waterfall with { GradientStops = stops.ToImmutableArray() });
        else
            Update(loudness: Current.Loudness with { GradientStops = stops.ToImmutableArray() });
        SyncGradientStops();
    }

    private void SyncGradientStops()
    {
        if (GradientStops is null) return;
        var previous = SelectedGradientStop;
        GradientStops.Clear();
        foreach (var stop in CurrentStops) GradientStops.Add(stop);
        SelectedGradientStop = previous is not null && GradientStops.Contains(previous)
            ? previous : GradientStops.FirstOrDefault();
        OnPropertyChanged(nameof(CanDeleteGradientStop));
    }

    private void Update(AnalyzerSettings? analyzer = null, WaterfallSettings? waterfall = null,
        MeterSettings? meters = null, LoudnessDisplaySettings? loudness = null,
        RtaDisplaySettings? rta = null, PhaseDisplaySettings? phase = null)
    {
        _session.Replace(Current with
        {
            Analyzer = analyzer ?? Current.Analyzer,
            Waterfall = waterfall ?? Current.Waterfall,
            Meters = meters ?? Current.Meters,
            Loudness = loudness ?? Current.Loudness,
            Rta = rta ?? Current.Rta,
            Phase = phase ?? Current.Phase,
        });
        ValidationMessage = string.Empty;
        NotifySettingsChanged();
    }

    private void SetCatalog(SettingsProfileCatalog catalog)
    {
        _catalog = catalog;
        OnPropertyChanged(nameof(Profiles));
        OnPropertyChanged(nameof(ProfileOptions));
        OnPropertyChanged(nameof(SelectedProfile));
        OnPropertyChanged(nameof(HasUnsavedDraft));
    }

    private void NotifySettingsChanged()
    {
        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(HasUnsavedDraft));
        foreach (var property in GetType().GetProperties())
            if (property.Name is not (nameof(Current) or nameof(HasUnsavedDraft) or nameof(Profiles) or nameof(SelectedProfile)))
                OnPropertyChanged(property.Name);
        SyncGradientStops();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
