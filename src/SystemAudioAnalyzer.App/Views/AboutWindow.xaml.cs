using System.Diagnostics;
using System.Windows.Documents;
using System.Windows.Navigation;
using SystemAudioAnalyzer.App.Localization;

namespace SystemAudioAnalyzer.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        NameText.Text = AppInfo.Name;
        VersionText.Text = Localizer.T("AboutVersion", AppInfo.Version);
        BuildDateText.Text = Localizer.T("AboutBuildDate", AppInfo.BuildDate);
        RepositoryLink.NavigateUri = new Uri(AppInfo.RepositoryUrl);
        RepositoryLink.Inlines.Add(new Run(AppInfo.RepositoryUrl));
    }

    private void OpenRepository(object sender, RequestNavigateEventArgs eventArgs)
    {
        try
        {
            Process.Start(new ProcessStartInfo(eventArgs.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No browser registered: the address stays visible and can be copied.
        }

        eventArgs.Handled = true;
    }

    private void Close(object sender, RoutedEventArgs eventArgs) => DialogResult = true;
}
