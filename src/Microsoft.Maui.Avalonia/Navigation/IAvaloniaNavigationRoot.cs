using System;
using Avalonia.Controls;
using Microsoft.Maui.Graphics;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace Microsoft.Maui.Avalonia.Navigation;

public interface IAvaloniaNavigationRoot
{
	Control RootView { get; }

	void Attach(AvaloniaWindow window);
	void Detach();

	void SetPlaceholder(string message);

	void SetContent(Control? control);

	void SetContentPadding(Thickness padding);

	void SetToolbar(Control? control);

	void SetMenu(Control? control);

	void SetTitle(string? title);

	void SetTitleBar(Control? control, IReadOnlyList<Control>? passthroughElements);

	void SetDragRectangles(IReadOnlyList<Rect> rectangles);

	event EventHandler? SafeAreaChanged;

	Thickness GetSafeAreaInsets();
}
