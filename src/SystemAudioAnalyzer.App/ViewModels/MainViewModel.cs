using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SystemAudioAnalyzer.App.Services;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IAnalyzerController _controller;
    private readonly SynchronizationContext? _synchronizationContext;
    private SourceMode _selectedSourceMode;
    private OutputDeviceInfo? _selectedDevice;
    private string _streamUrl = string.Empty;
    private string _statusText = "Готов к анализу.";
    private bool _isAnalyzing;
    private AnalysisFrame? _latestFrame;
    private RtaResolution _rtaResolution = RtaResolution.OneThird;
    private RtaChannelMode _rtaChannelMode = RtaChannelMode.Mono;
    private InstrumentTab _activeTab = InstrumentTab.Waterfall;
    private MeasurementSettings _measurementSettings = MeasurementSettings.Default;
    private double _phaseGain = MeasurementSettings.Default.Phase.Gain;
    private bool _syncingFromSettings;
    private bool _sourceReportedState;
    private AnalysisRunState _runState = AnalysisRunState.Stopped;
    private readonly FrameStatistics _frameStatistics = new();
    private DateTimeOffset _lastDiagnosticsUpdate = DateTimeOffset.MinValue;
    private string _diagnosticsText = string.Empty;

    public MainViewModel(IAnalyzerController controller, IEnumerable<OutputDeviceInfo> devices)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _synchronizationContext = SynchronizationContext.Current;
        Devices = new ReadOnlyCollection<OutputDeviceInfo>((devices ?? throw new ArgumentNullException(nameof(devices))).ToArray());
        _selectedDevice = Devices.FirstOrDefault(device => device.IsDefault) ?? Devices.FirstOrDefault();
        StartCommand = new AsyncCommand(StartAsync, () => CanStart);
        StopCommand = new AsyncCommand(StopAsync, () => IsAnalyzing);
        _controller.FrameAvailable += OnFrameAvailable;
        _controller.SourceStateChanged += OnSourceStateChanged;
        ApplyEngineSettings(_measurementSettings);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when a toolbar button (not the settings dialog or startup load) changed the settings.</summary>
    public event EventHandler<ToolbarSettingsChangedEventArgs>? ToolbarSettingsChanged;

    /// <summary>Raised after the stream URL history changed (a stream was opened or the list was cleared).</summary>
    public event EventHandler? StreamHistoryChanged;

    public ReadOnlyCollection<OutputDeviceInfo> Devices { get; }

    /// <summary>Most recently opened stream URLs, newest first, at most <see cref="StreamHistoryStore.Capacity"/>.</summary>
    public ObservableCollection<string> StreamHistory { get; } = [];

    public void LoadStreamHistory(IEnumerable<string> urls)
    {
        StreamHistory.Clear();
        foreach (var url in urls.Take(StreamHistoryStore.Capacity)) StreamHistory.Add(url);
    }

    public void ClearStreamHistory()
    {
        StreamHistory.Clear();
        StreamHistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RememberStream(string url)
    {
        url = url.Trim();
        if (url.Length == 0) return;
        var existing = StreamHistory.IndexOf(url);
        if (existing == 0) return;
        if (existing > 0) StreamHistory.RemoveAt(existing);
        StreamHistory.Insert(0, url);
        while (StreamHistory.Count > StreamHistoryStore.Capacity) StreamHistory.RemoveAt(StreamHistory.Count - 1);
        StreamHistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public AsyncCommand StartCommand { get; }

    public AsyncCommand StopCommand { get; }

    public SourceMode SelectedSourceMode
    {
        get => _selectedSourceMode;
        set
        {
            if (SetField(ref _selectedSourceMode, value))
            {
                NotifyValidationChanged();
                OnPropertyChanged(nameof(IsDeviceMode));
                OnPropertyChanged(nameof(IsStreamMode));
            }
        }
    }

    public bool IsDeviceMode
    {
        get => SelectedSourceMode == SourceMode.Device;
        set
        {
            if (value)
            {
                SelectedSourceMode = SourceMode.Device;
            }
        }
    }

    public bool IsStreamMode
    {
        get => SelectedSourceMode == SourceMode.Stream;
        set
        {
            if (value)
            {
                SelectedSourceMode = SourceMode.Stream;
            }
        }
    }

    public OutputDeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetField(ref _selectedDevice, value))
            {
                NotifyValidationChanged();
            }
        }
    }

    public string StreamUrl
    {
        get => _streamUrl;
        set
        {
            if (SetField(ref _streamUrl, value ?? string.Empty))
            {
                NotifyValidationChanged();
            }
        }
    }

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        private set
        {
            if (SetField(ref _isAnalyzing, value))
            {
                NotifyValidationChanged();
                StopCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanStart => !IsAnalyzing && TryCreateSelection(out _);

    public string ValidationMessage => SelectedSourceMode == SourceMode.Stream && !TryGetStreamUri(out _)
        ? "Введите корректный HTTP/HTTPS адрес Icecast или HLS-потока."
        : string.Empty;

    /// <summary>Drives the Start/Stop button highlighting.</summary>
    public AnalysisRunState RunState
    {
        get => _runState;
        private set => SetField(ref _runState, value);
    }

    /// <summary>Live frame rate, delivery delay, UI cost and dropped buffers; empty while stopped.</summary>
    public string DiagnosticsText
    {
        get => _diagnosticsText;
        private set => SetField(ref _diagnosticsText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public AnalysisFrame? LatestFrame
    {
        get => _latestFrame;
        set
        {
            if (SetField(ref _latestFrame, value))
            {
                OnPropertyChanged(nameof(CurrentLoudnessText));
            }
        }
    }

    public string CurrentLoudnessText
    {
        get
        {
            var loudness = LatestFrame?.AdvancedMeasurements?.Loudness;
            var value = MeasurementSettings.Loudness.Metric switch
            {
                LoudnessMetric.Momentary => loudness?.MomentaryLufs,
                LoudnessMetric.ShortTerm => loudness?.ShortTermLufs,
                _ => loudness?.IntegratedLufs,
            };
            return value.HasValue ? $"{value.Value:0.0} LUFS" : "— LUFS";
        }
    }

    public RtaResolution RtaResolution
    {
        get => _rtaResolution;
        set
        {
            if (SetField(ref _rtaResolution, value) && !_syncingFromSettings)
                ApplyToolbarChange(settings => settings with { Rta = settings.Rta with { Resolution = value } });
        }
    }

    public RtaChannelMode RtaChannelMode
    {
        get => _rtaChannelMode;
        set
        {
            if (SetField(ref _rtaChannelMode, value))
            {
                OnPropertyChanged(nameof(IsRtaMono));
                OnPropertyChanged(nameof(IsRtaLeft));
                OnPropertyChanged(nameof(IsRtaRight));
                if (!_syncingFromSettings)
                    ApplyToolbarChange(settings => settings with { Rta = settings.Rta with { Source = value } });
            }
        }
    }

    public bool IsRtaMono { get => RtaChannelMode == RtaChannelMode.Mono; set { if (value) RtaChannelMode = RtaChannelMode.Mono; } }

    public bool IsRtaLeft { get => RtaChannelMode == RtaChannelMode.Left; set { if (value) RtaChannelMode = RtaChannelMode.Left; } }

    public bool IsRtaRight { get => RtaChannelMode == RtaChannelMode.Right; set { if (value) RtaChannelMode = RtaChannelMode.Right; } }

    public InstrumentTab ActiveTab
    {
        get => _activeTab;
        set => SetField(ref _activeTab, value);
    }

    public MeasurementSettings MeasurementSettings
    {
        get => _measurementSettings;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetField(ref _measurementSettings, value))
            {
                ApplyEngineSettings(value);
                _syncingFromSettings = true;
                try
                {
                    RtaResolution = value.Rta.Resolution;
                    RtaChannelMode = value.Rta.Source;
                }
                finally
                {
                    _syncingFromSettings = false;
                }

                PhaseGain = value.Phase.Gain;
                OnPropertyChanged(nameof(CurrentLoudnessText));
                OnPropertyChanged(nameof(LoudnessMetric));
                OnPropertyChanged(nameof(LoudnessWindowSeconds));
                OnPropertyChanged(nameof(LoudnessScaleText));
                OnPropertyChanged(nameof(RtaAverageText));
                OnPropertyChanged(nameof(RtaTargetText));
                OnPropertyChanged(nameof(LoudnessTargetText));
            }
        }
    }

    public LoudnessMetric LoudnessMetric
    {
        get => MeasurementSettings.Loudness.Metric;
        set { if (value != LoudnessMetric) ApplyToolbarChange(settings => ToolbarSettingsActions.WithLoudnessMetric(settings, value)); }
    }

    public int LoudnessWindowSeconds
    {
        get => MeasurementSettings.Loudness.HistorySeconds;
        set { if (value != LoudnessWindowSeconds) ApplyToolbarChange(settings => ToolbarSettingsActions.WithLoudnessWindow(settings, value)); }
    }

    public string LoudnessScaleText => ToolbarSettingsActions.LoudnessScaleText(MeasurementSettings);

    public string LoudnessTargetText => $"Tgt {MeasurementSettings.Loudness.TargetLufs:0}";

    public string RtaAverageText => $"Avg {MeasurementSettings.Rta.AveragingCount}";

    public string RtaTargetText => $"Tgt {MeasurementSettings.Rta.TargetLineDb:0}";

    public void ZoomLoudness(double factor) => ApplyToolbarChange(settings => ToolbarSettingsActions.ZoomLoudness(settings, factor));

    public void ShiftLoudness(double deltaLufs) => ApplyToolbarChange(settings => ToolbarSettingsActions.ShiftLoudness(settings, deltaLufs));

    public void AdjustLoudnessTarget(double deltaLufs) => ApplyToolbarChange(settings => ToolbarSettingsActions.WithLoudnessTarget(settings, deltaLufs));

    public void CycleRollingWindow() => ApplyToolbarChange(ToolbarSettingsActions.CycleRollingWindow);

    public void AdjustRtaAveraging(int delta) => ApplyToolbarChange(settings => ToolbarSettingsActions.WithRtaAveraging(settings, delta));

    public void AdjustRtaTarget(double deltaDb) => ApplyToolbarChange(settings => ToolbarSettingsActions.WithRtaTarget(settings, deltaDb));

    private void ApplyToolbarChange(Func<MeasurementSettings, MeasurementSettings> change)
    {
        MeasurementSettings = change(MeasurementSettings);
        ToolbarSettingsChanged?.Invoke(this, new ToolbarSettingsChangedEventArgs(MeasurementSettings));
    }

    public double PhaseGain
    {
        get => _phaseGain;
        set => SetField(ref _phaseGain, value);
    }

    public void SelectTab(InstrumentTab tab) => ActiveTab = tab;

    public void ResetAllMeasurements()
    {
        _controller.ResetTruePeak(0);
        _controller.ResetTruePeak(1);
        _controller.ResetLoudness();
        StatusText = "Измерения сброшены.";
    }

    public void ResetTruePeakMaximum(int channel) => _controller.ResetTruePeakMaximum(channel);

    public void ResetTruePeakOverload(int channel) => _controller.ResetTruePeakOverload(channel);

    private async Task StartAsync()
    {
        if (!TryCreateSelection(out var selection))
        {
            return;
        }

        try
        {
            StatusText = TryGetStreamUri(out var host) && SelectedSourceMode == SourceMode.Stream
                ? $"Подключение к {host!.Host}…"
                : "Запуск анализа…";
            RunState = AnalysisRunState.Starting;
            _sourceReportedState = false;
            _frameStatistics.Reset();
            _lastDiagnosticsUpdate = DateTimeOffset.MinValue;
            await _controller.StartAsync(selection);
            IsAnalyzing = true;
            if (selection.Mode == SourceMode.Stream)
            {
                // The stream is still connecting or buffering; the source events and the first frame
                // move the state on, so the status keeps saying what is happening.
                RememberStream(StreamUrl);
                if (RunState is AnalysisRunState.Starting && !_sourceReportedState) StatusText = $"Подключение к {selection.StreamUri?.Host}…";
            }
            else
            {
                if (RunState is AnalysisRunState.Starting) RunState = AnalysisRunState.Running;
                StatusText = "Анализ выполняется.";
            }
        }
        catch (Exception exception)
        {
            IsAnalyzing = false;
            RunState = AnalysisRunState.Faulted;
            StatusText = $"Не удалось запустить анализ: {exception.Message}";
        }
    }

    private async Task StopAsync()
    {
        try
        {
            await _controller.StopAsync();
            RunState = AnalysisRunState.Stopped;
            StatusText = "Анализ остановлен.";
            DiagnosticsText = string.Empty;
        }
        catch (Exception exception)
        {
            StatusText = $"Не удалось остановить анализ: {exception.Message}";
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    private void OnFrameAvailable(object? sender, AnalysisFrame frame) =>
        RunOnUi(() =>
        {
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            LatestFrame = frame;
            if (IsAnalyzing && RunState is AnalysisRunState.Starting)
            {
                // Audio is flowing, whatever the source last reported.
                RunState = AnalysisRunState.Running;
                StatusText = "Анализ выполняется.";
            }
            var uiWork = System.Diagnostics.Stopwatch.GetElapsedTime(started);
            var now = DateTimeOffset.UtcNow;
            _frameStatistics.Record(frame.Timestamp, now, frame.DroppedBufferCount, uiWork);
            if (IsAnalyzing && now - _lastDiagnosticsUpdate >= TimeSpan.FromMilliseconds(250))
            {
                _lastDiagnosticsUpdate = now;
                DiagnosticsText = _frameStatistics.Text;
            }
        });

    private void OnSourceStateChanged(object? sender, AudioSourceStateChangedEventArgs eventArgs) =>
        RunOnUi(() =>
        {
            _sourceReportedState = true;
            RunState = eventArgs.State switch
            {
                AudioSourceState.Connecting or AudioSourceState.Buffering => AnalysisRunState.Starting,
                AudioSourceState.Running => AnalysisRunState.Running,
                AudioSourceState.Faulted => AnalysisRunState.Faulted,
                _ => AnalysisRunState.Stopped,
            };
            ApplySourceStatus(eventArgs);
        });

    private void ApplySourceStatus(AudioSourceStateChangedEventArgs eventArgs) => StatusText = eventArgs.State switch
        {
            AudioSourceState.Connecting => "Подключение к источнику…",
            AudioSourceState.Buffering => eventArgs.BufferingPercent is { } percent
                ? $"Буферизация потока… {percent:0}%"
                : "Буферизация потока…",
            AudioSourceState.Running => "Анализ выполняется.",
            AudioSourceState.Faulted => "Ошибка источника. Можно повторить запуск.",
            _ => "Анализ остановлен.",
        };

    private void RunOnUi(Action action)
    {
        if (_synchronizationContext is null || SynchronizationContext.Current == _synchronizationContext)
        {
            action();
            return;
        }

        _synchronizationContext.Post(_ => action(), null);
    }

    private bool TryCreateSelection(out SourceSelection selection)
    {
        if (SelectedSourceMode == SourceMode.Device)
        {
            selection = new SourceSelection(SourceMode.Device, SelectedDevice, null);
            return SelectedDevice is not null;
        }

        if (TryGetStreamUri(out var streamUri))
        {
            selection = new SourceSelection(SourceMode.Stream, null, streamUri);
            return true;
        }

        selection = default!;
        return false;
    }

    private bool TryGetStreamUri(out Uri? streamUri)
    {
        if (!Uri.TryCreate(StreamUrl, UriKind.Absolute, out streamUri) || streamUri is null)
        {
            return false;
        }

        return string.Equals(streamUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(streamUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private void NotifyValidationChanged()
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(ValidationMessage));
        StartCommand.RaiseCanExecuteChanged();
    }

    private void ApplyEngineSettings(MeasurementSettings settings)
    {
        var window = settings.Analyzer.WindowFunction switch
        {
            AnalyzerWindowFunction.Rectangular => SpectrumWindow.Rectangular,
            AnalyzerWindowFunction.Hann => SpectrumWindow.Hann,
            AnalyzerWindowFunction.Hamming => SpectrumWindow.Hamming,
            AnalyzerWindowFunction.Blackman => SpectrumWindow.Blackman,
            _ => throw new ArgumentOutOfRangeException(nameof(settings), "Analyzer window function is not supported."),
        };
        _controller.SetAnalysisConfiguration(new AnalysisConfiguration(settings.Analyzer.FftSize, window));
        _controller.SetLoudnessIntegratedWindow(settings.Meters.IntegratedWindowSeconds);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
