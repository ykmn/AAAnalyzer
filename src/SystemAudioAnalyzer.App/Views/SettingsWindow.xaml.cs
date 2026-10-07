using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Pages.SelectedIndex = viewModel.SelectedPage switch
        {
            InstrumentTab.Analyzer => 0,
            InstrumentTab.Waterfall => 1,
            InstrumentTab.Meters => 2,
            InstrumentTab.Loudness => 3,
            InstrumentTab.Rta => 4,
            _ => 5,
        };
    }

    private void Accept(object sender, RoutedEventArgs eventArgs) => DialogResult = true;

    private void Cancel(object sender, RoutedEventArgs eventArgs) => DialogResult = false;
}
