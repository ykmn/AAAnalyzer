using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SystemAudioAnalyzer.App.Services;

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
        set => SetField(ref _latestFrame, value);
    }

    private async Task StartAsync()
    {
        if (!TryCreateSelection(out var selection))
        {
            return;
        }

        try
        {
            StatusText = "Запуск анализа…";
            await _controller.StartAsync(selection).ConfigureAwait(false);
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
            await _controller.StopAsync().ConfigureAwait(false);
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
