using SystemAudioAnalyzer.App.Services;
using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var provider = new NaudioAudioOutputDeviceProvider();
        DataContext = new MainViewModel(new AnalyzerController(), provider.GetActiveDevices());
    }
}
