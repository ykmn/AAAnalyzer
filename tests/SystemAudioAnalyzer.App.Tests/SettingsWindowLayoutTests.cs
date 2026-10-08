using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SystemAudioAnalyzer.App.Settings;
using SystemAudioAnalyzer.App.ViewModels;
using SystemAudioAnalyzer.App.Views;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class SettingsWindowLayoutTests
{
    [Fact]
    public void ButtonsStayInsideWindowAndPresetNameFieldIsWide()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { AssertLayout(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    private static void AssertLayout()
    {
        var store = new SettingsStore(Path.Combine(Path.GetTempPath(), "AAAnalyzerTests", Guid.NewGuid().ToString("N")), null,
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "legacy.json"));
        var viewModel = new SettingsDialogViewModel(store, SettingsProfileCatalog.Default, MeasurementSettings.Default, InstrumentTab.Analyzer);
        var window = new SettingsWindow(viewModel);
        var root = (FrameworkElement)window.Content;
        var size = new Size(window.Width, window.Height);
        root.Measure(size);
        root.Arrange(new Rect(new Point(), size));
        root.UpdateLayout();

        var buttons = Descendants<Button>(root)
            .Where(button => button.Content is string)
            .GroupBy(button => (string)button.Content)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var nameBox = Descendants<TextBox>(root).First();

        var cancel = Bounds(buttons["Cancel"], root);
        var apply = Bounds(buttons["Apply"], root);
        Assert.True(cancel.Right <= size.Width, "Cancel must stay inside the window.");
        Assert.True(apply.Left >= 0);
        Assert.True(buttons["Cancel"].IsCancel, "Esc must close the dialog like Cancel.");
        Assert.True(Bounds(nameBox, root).Width >= 180, "Preset name field must be wide.");
        Assert.True(Bounds(buttons["Save"], root).Width <= 70, "Save must be compact.");
        Assert.True(Bounds(buttons["Delete"], root).Width <= 70, "Delete must be compact.");
    }

    private static Rect Bounds(FrameworkElement element, Visual relativeTo) =>
        element.TransformToAncestor(relativeTo).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
