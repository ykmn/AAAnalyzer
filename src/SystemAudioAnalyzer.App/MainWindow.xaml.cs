using SystemAudioAnalyzer.App.Services;
using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var provider = new NaudioAudioOutputDeviceProvider();
        DataContext = new MainViewModel(new UnavailableAnalyzerController(), provider.GetActiveDevices());
    }

    private sealed class UnavailableAnalyzerController : IAnalyzerController
    {
        public Task StartAsync(SourceSelection selection, CancellationToken cancellationToken = default) =>
            Task.FromException(new InvalidOperationException("Audio sources are being configured."));

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
