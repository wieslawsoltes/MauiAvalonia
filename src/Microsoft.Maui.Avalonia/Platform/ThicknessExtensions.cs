using Microsoft.Maui.Graphics;

namespace Microsoft.Maui.Avalonia.Platform;

internal static class ThicknessExtensions
{
	public static AvaloniaThickness ToAvalonia(this Thickness thickness) =>
		new(thickness.Left, thickness.Top, thickness.Right, thickness.Bottom);
}
