using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace Microsoft.Maui.Avalonia.Input;

static class FocusVisualManager
{
	static readonly ITemplate<Control> FocusTemplate = new FuncTemplate<Control>(() =>
		new AvaloniaBorderControl
		{
			BorderBrush = Brushes.DodgerBlue,
			BorderThickness = new global::Avalonia.Thickness(2),
			CornerRadius = new global::Avalonia.CornerRadius(3)
		});

	public static void EnsureFocusVisual(Control control)
	{
		if (control is null)
			return;

		if (control.FocusAdorner is null)
			control.FocusAdorner = FocusTemplate;
	}
}
