using System.ComponentModel;
using System.Runtime.CompilerServices;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.ViewModels;

public sealed class SettingsDialogViewModel : INotifyPropertyChanged
{
    private readonly SettingsStore _store;
    private readonly SettingsEditSession _session;
    private InstrumentTab _selectedPage;
    private double _waterfallFloorDb;
    private double _waterfallOffsetDb;
    private string _waterfallPaletteColor;
    private double _meterRangeDb;
    private string _meterColor;
    private string _overloadColor;
    private int _loudnessHistorySeconds;
    private LoudnessMetric _loudnessMetric;
    private bool _loudnessAutoScale;
    private double _loudnessSpan;
    private double _loudnessCentre;
    private RtaChannelMode _rtaSource;
    private RtaResolution _rtaResolution;
    private double _rtaScaleTop;
    private double _rtaScaleRange;
    private double _rtaTargetLine;
    private double _phaseGain;
    private double _analyzerFloorDb;
    private string _analyzerCursorColor;
    private string _analyzerTextColor;

    public SettingsDialogViewModel(SettingsStore store, MeasurementSettings settings, InstrumentTab selectedPage)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _session = new SettingsEditSession(settings);
        _selectedPage = selectedPage;
        _waterfallFloorDb = settings.Waterfall.DisplayFloorDb;
        _waterfallOffsetDb = settings.Waterfall.DisplayOffsetDb;
        _waterfallPaletteColor = settings.Waterfall.PaletteColor;
        _meterRangeDb = settings.Meters.DisplayRangeDb;
        _meterColor = settings.Meters.MeterColor;
        _overloadColor = settings.Meters.OverloadColor;
        _loudnessHistorySeconds = settings.Loudness.HistorySeconds;
        _loudnessMetric = settings.Loudness.Metric;
        _loudnessAutoScale = settings.Loudness.AutoScale;
        _loudnessSpan = settings.Loudness.SpanLufs;
        _loudnessCentre = settings.Loudness.CentreLufs;
        _rtaSource = settings.Rta.Source;
        _rtaResolution = settings.Rta.Resolution;
        _rtaScaleTop = settings.Rta.ScaleTopDb;
        _rtaScaleRange = settings.Rta.ScaleRangeDb;
        _rtaTargetLine = settings.Rta.TargetLineDb;
        _phaseGain = settings.Phase.Gain;
        _analyzerFloorDb = settings.Analyzer.DisplayFloorDb;
        _analyzerCursorColor = settings.Analyzer.CursorColor;
        _analyzerTextColor = settings.Analyzer.TextColor;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MeasurementSettings Current => _session.Current;

    public double AnalyzerFloorDb { get => _analyzerFloorDb; set { _analyzerFloorDb = value; Update(analyzer: Current.Analyzer with { DisplayFloorDb = value }); } }
    public string AnalyzerCursorColor { get => _analyzerCursorColor; set { _analyzerCursorColor = value ?? string.Empty; Update(analyzer: Current.Analyzer with { CursorColor = _analyzerCursorColor }); } }
    public string AnalyzerTextColor { get => _analyzerTextColor; set { _analyzerTextColor = value ?? string.Empty; Update(analyzer: Current.Analyzer with { TextColor = _analyzerTextColor }); } }
    public double WaterfallFloorDb { get => _waterfallFloorDb; set { _waterfallFloorDb = value; Update(waterfall: Current.Waterfall with { DisplayFloorDb = value }); } }
    public double WaterfallOffsetDb { get => _waterfallOffsetDb; set { _waterfallOffsetDb = value; Update(waterfall: Current.Waterfall with { DisplayOffsetDb = value }); } }
    public string WaterfallPaletteColor { get => _waterfallPaletteColor; set { _waterfallPaletteColor = value ?? string.Empty; Update(waterfall: Current.Waterfall with { PaletteColor = _waterfallPaletteColor }); } }
    public double MeterRangeDb { get => _meterRangeDb; set { _meterRangeDb = value; Update(meters: Current.Meters with { DisplayRangeDb = value }); } }
    public string MeterColor { get => _meterColor; set { _meterColor = value ?? string.Empty; Update(meters: Current.Meters with { MeterColor = _meterColor }); } }
    public string OverloadColor { get => _overloadColor; set { _overloadColor = value ?? string.Empty; Update(meters: Current.Meters with { OverloadColor = _overloadColor }); } }
    public int LoudnessHistorySeconds { get => _loudnessHistorySeconds; set { _loudnessHistorySeconds = value; Update(loudness: Current.Loudness with { HistorySeconds = value }); } }
    public LoudnessMetric LoudnessMetric { get => _loudnessMetric; set { _loudnessMetric = value; Update(loudness: Current.Loudness with { Metric = value }); } }
    public bool LoudnessAutoScale { get => _loudnessAutoScale; set { _loudnessAutoScale = value; Update(loudness: Current.Loudness with { AutoScale = value }); } }
    public double LoudnessSpan { get => _loudnessSpan; set { _loudnessSpan = value; Update(loudness: Current.Loudness with { SpanLufs = value }); } }
    public double LoudnessCentre { get => _loudnessCentre; set { _loudnessCentre = value; Update(loudness: Current.Loudness with { CentreLufs = value }); } }
    public RtaChannelMode RtaSource { get => _rtaSource; set { _rtaSource = value; Update(rta: Current.Rta with { Source = value }); } }
    public RtaResolution RtaResolution { get => _rtaResolution; set { _rtaResolution = value; Update(rta: Current.Rta with { Resolution = value }); } }
    public double RtaScaleTop { get => _rtaScaleTop; set { _rtaScaleTop = value; Update(rta: Current.Rta with { ScaleTopDb = value }); } }
    public double RtaScaleRange { get => _rtaScaleRange; set { _rtaScaleRange = value; Update(rta: Current.Rta with { ScaleRangeDb = value }); } }
    public double RtaTargetLine { get => _rtaTargetLine; set { _rtaTargetLine = value; Update(rta: Current.Rta with { TargetLineDb = value }); } }
    public double PhaseGain { get => _phaseGain; set { _phaseGain = value; Update(phase: Current.Phase with { Gain = value }); } }

    public InstrumentTab SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (_selectedPage != value)
            {
                _selectedPage = value;
                OnPropertyChanged();
            }
        }
    }

    public void Replace(MeasurementSettings settings)
    {
        _session.Replace(settings);
        OnPropertyChanged(nameof(Current));
    }

    public async Task<MeasurementSettings> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var settings = _session.Apply();
        await _store.SaveAsync(settings, cancellationToken);
        return settings;
    }

    public MeasurementSettings Cancel()
    {
        var settings = _session.Cancel();
        OnPropertyChanged(nameof(Current));
        return settings;
    }

    public async Task<MeasurementSettings> CancelAsync(CancellationToken cancellationToken = default)
    {
        var settings = Cancel();
        await _store.SaveAsync(settings, cancellationToken);
        return settings;
    }

    private void Update(AnalyzerSettings? analyzer = null, WaterfallSettings? waterfall = null, MeterSettings? meters = null, LoudnessDisplaySettings? loudness = null, RtaDisplaySettings? rta = null, PhaseDisplaySettings? phase = null)
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
        OnPropertyChanged(nameof(Current));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
