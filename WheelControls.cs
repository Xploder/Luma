using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Luma;

// Wheel input belongs to the surrounding page. Pointer/keyboard interaction
// with the slider and an open combo-box popup retain their normal behavior.
public sealed partial class WheelSlider : Slider
{
    internal Action<PointerRoutedEventArgs>? ForwardWheel { get; set; }
    protected override void OnPointerWheelChanged(PointerRoutedEventArgs e) => ForwardWheel?.Invoke(e);
}

public sealed partial class WheelComboBox : ComboBox
{
    internal Action<PointerRoutedEventArgs>? ForwardWheel { get; set; }
    protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
    {
        if (IsDropDownOpen) base.OnPointerWheelChanged(e);
        else ForwardWheel?.Invoke(e);
    }
}
