using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Luma.UiText;

namespace Luma;

internal static class UiLocalization
{
    internal static void Apply(DependencyObject element)
    {
        if (element is TextBlock text) text.Text = T(text.Text);
        if (element is ContentControl content && content.Content is string value) content.Content = T(value);
        if (element is ToggleSwitch toggle)
        {
            if (toggle.OnContent is string on) toggle.OnContent = T(on);
            if (toggle.OffContent is string off) toggle.OffContent = T(off);
        }
        if (element is ComboBox combo) combo.PlaceholderText = T(combo.PlaceholderText);
        string name = AutomationProperties.GetName(element);
        if (!string.IsNullOrEmpty(name)) AutomationProperties.SetName(element, T(name));
        if (ToolTipService.GetToolTip(element) is string tip) ToolTipService.SetToolTip(element, T(tip));
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++) Apply(VisualTreeHelper.GetChild(element, i));
    }
}
