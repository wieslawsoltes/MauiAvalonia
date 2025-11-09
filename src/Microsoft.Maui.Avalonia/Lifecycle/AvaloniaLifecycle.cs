using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaApplication = Avalonia.Application;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace Microsoft.Maui.LifecycleEvents;

/// <summary>
/// Defines delegate types used when raising Avalonia-specific lifecycle events.
/// </summary>
public static class AvaloniaLifecycle
{
	/// <summary>Raised before the MAUI host wires up window services.</summary>
	public delegate void OnInitializing(AvaloniaApplication application);

	/// <summary>Raised when <see cref="Application.OnFrameworkInitializationCompleted"/> finishes.</summary>
	public delegate void OnFrameworkInitialized(AvaloniaApplication application);

	/// <summary>Raised when the Avalonia lifetime emits a startup signal.</summary>
	public delegate void OnStartup(AvaloniaApplication application, ControlledApplicationLifetimeStartupEventArgs args);

	/// <summary>Raised when the Avalonia lifetime emits an exit signal.</summary>
	public delegate void OnExit(AvaloniaApplication application, ControlledApplicationLifetimeExitEventArgs args);

	/// <summary>Raised after a new Avalonia <see cref="Window"/> is created.</summary>
	public delegate void OnWindowCreated(AvaloniaApplication application, AvaloniaWindow window);

	/// <summary>Raised after an Avalonia <see cref="Window"/> closes.</summary>
	public delegate void OnWindowDestroyed(AvaloniaApplication application, AvaloniaWindow window);

	/// <summary>Raised when an Avalonia <see cref="Window"/> becomes active.</summary>
	public delegate void OnWindowActivated(AvaloniaApplication application, AvaloniaWindow window);

	/// <summary>Raised when an Avalonia <see cref="Window"/> loses activation.</summary>
	public delegate void OnWindowDeactivated(AvaloniaApplication application, AvaloniaWindow window);

	/// <summary>Raised when an Avalonia <see cref="Window"/> reports a theme variant change.</summary>
	public delegate void OnWindowThemeChanged(AvaloniaApplication application, AvaloniaWindow window, EventArgs args);

	/// <summary>Raised when the application's <see cref="Application.ActualThemeVariant"/> changes.</summary>
	public delegate void OnThemeChanged(AvaloniaApplication application, EventArgs args);
}
