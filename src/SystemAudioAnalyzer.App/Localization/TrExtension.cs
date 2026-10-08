using System.Windows.Data;
using System.Windows.Markup;

namespace SystemAudioAnalyzer.App.Localization;

/// <summary>{loc:Tr Key}: a text that follows the UI language.</summary>
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new System.Windows.Data.Binding($"[{Key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
