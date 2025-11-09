using System.Collections.Generic;
using Avalonia;
using Microsoft.Maui.Handlers;
using AvaloniaApplication = Avalonia.Application;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace Microsoft.Maui.Hosting;

/// <summary>
/// Provides a hook between the Avalonia lifetime model and the MAUI window system.
/// </summary>
public interface IAvaloniaWindowHost
{
	/// <summary>
	/// Attaches to the current Avalonia <paramref name="lifetimeOwner"/> and ensures at least one window
	/// is created for the supplied MAUI <paramref name="application"/>.
	/// </summary>
	void AttachLifetime(AvaloniaApplication lifetimeOwner, Microsoft.Maui.IApplication application, IMauiContext applicationContext);

	/// <summary>
	/// Opens a new Avalonia window tied to the specified MAUI <paramref name="application"/>.
	/// </summary>
	/// <param name="application">The MAUI application requesting the window.</param>
	/// <param name="request">Optional window request metadata (persisted state, routing id, etc.).</param>
	void OpenWindow(Microsoft.Maui.IApplication application, OpenWindowRequest? request);

	/// <summary>
	/// Attempts to close the specified MAUI <paramref name="window"/> and dispose its scope.
	/// </summary>
	void CloseWindow(Microsoft.Maui.IWindow window);

	/// <summary>
	/// Enumerates the currently tracked MAUI windows.
	/// </summary>
	IEnumerable<Microsoft.Maui.IWindow> EnumerateWindows();

	/// <summary>
	/// Retrieves the Avalonia platform window for the supplied MAUI <paramref name="window"/>.
	/// </summary>
	/// <param name="window">The MAUI window associated with the platform window.</param>
	/// <param name="platformWindow">The resulting Avalonia window instance, if found.</param>
	/// <returns><c>true</c> if the mapping was resolved; otherwise, <c>false</c>.</returns>
	bool TryGetPlatformWindow(Microsoft.Maui.IWindow window, out AvaloniaWindow platformWindow);
}
