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
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReadOnlyCollection<OutputDeviceInfo> Devices { get; }

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
        set => SetField(ref _rtaResolution, value);
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
            }
        }
    }

    public bool IsRtaMono { get => RtaChannelMode == RtaChannelMode.Mono; set { if (value) RtaChannelMode = RtaChannelMode.Mono; } }

    public bool IsRtaLeft { get => RtaChannelMode == RtaChannelMode.Left; set { if (value) RtaChannelMode = RtaChannelMode.Left; } }

    public bool IsRtaRight { get => RtaChannelMode == RtaChannelMode.Right; set { if (value) RtaChannelMode = RtaChannelMode.Right; } }

    public InstrumentTab ActiveTab
    {
        get => _activeTab;
        private set => SetField(ref _activeTab, value);
    }

    public MeasurementSettings MeasurementSettings
    {
        get => _measurementSettings;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetField(ref _measurementSettings, value))
            {
                _controller.SetLoudnessIntegratedWindow(value.Meters.IntegratedWindowSeconds);
                RtaResolution = value.Rta.Resolution;
                RtaChannelMode = value.Rta.Source;
                PhaseGain = value.Phase.Gain;
                OnPropertyChanged(nameof(CurrentLoudnessText));
            }
        }
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
            StatusText = "Запуск анализа…";
            await _controller.StartAsync(selection);
            IsAnalyzing = true;
            StatusText = "Анализ выполняется.";
        }
        catch (Exception exception)
        {
            IsAnalyzing = false;
            StatusText = $"Не удалось запустить анализ: {exception.Message}";
        }
    }

    private async Task StopAsync()
    {
        try
        {
            await _controller.StopAsync();
            StatusText = "Анализ остановлен.";
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
        RunOnUi(() => LatestFrame = frame);

    private void OnSourceStateChanged(object? sender, AudioSourceStateChangedEventArgs eventArgs) =>
        RunOnUi(() => StatusText = eventArgs.State switch
        {
            AudioSourceState.Connecting => "Подключение к источнику…",
            AudioSourceState.Buffering => "Буферизация потока…",
            AudioSourceState.Running => "Анализ выполняется.",
            AudioSourceState.Faulted => "Ошибка источника. Можно повторить запуск.",
            _ => "Анализ остановлен.",
        });

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
