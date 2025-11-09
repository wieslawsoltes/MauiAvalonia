using System;

namespace Microsoft.Maui.LifecycleEvents;

/// <summary>
/// Provides helper methods for wiring Avalonia lifecycle delegates.
/// </summary>
public static class AvaloniaLifecycleBuilderExtensions
{
	/// <summary>Registers a callback for the Avalonia initializing event.</summary>
	public static IAvaloniaLifecycleBuilder OnInitializing(this IAvaloniaLifecycleBuilder builder, AvaloniaLifecycle.OnInitializing del) =>
		builder.AddLifecycleEvent(nameof(AvaloniaLifecycle.OnInitializing), del);

	/// <summary>Registers a callback for <see cref="AvaloniaLifecycle.OnFrameworkInitialized"/>.</summary>
	public static IAvaloniaLifecycleBuilder OnFrameworkInitialized(this IAvaloniaLifecycleBuilder builder, AvaloniaLifecycle.OnFrameworkInitialized del) =>
		builder.AddLifecycleEvent(nameof(AvaloniaLifecycle.OnFrameworkInitialized), del);

	/// <summary>Registers a callback for Avalonia startup events.</summary>
	public static IAvaloniaLifecycleBuilder OnStartup(this IAvaloniaLifecycleBuilder builder, AvaloniaLifecycle.OnStartup del) =>
		builder.AddLifecycleEvent(nameof(AvaloniaLifecycle.OnStartup), del);

	/// <summary>Registers a callback for Avalonia exit events.</summary>
	public static IAvaloniaLifecycleBuilder OnExit(this IAvaloniaLifecycleBuilder builder, AvaloniaLifecycle.OnExit del) =>
		builder.AddLifecycleEvent(nameof(AvaloniaLifecycle.OnExit), del);

	/// <summary>Registers a callback for newly created Avalonia windows.</summary>
	public static IAvaloniaLifecycleBuilder OnWindowCreated(this IAvaloniaLifecycleBuilder builder, AvaloniaLifecycle.OnWindowCreated del) =>
		builder.AddLifecycleEvent(nameof(AvaloniaLifecycle.OnWindowCreated), del);

	/// <summary>Registers a callback for destroyed Avalonia windows.</summary>
	public static IAvaloniaLifecycleBuilder OnWindowDestroyed(this IAvaloniaLifecycleBuilder builder, AvaloniaLifecycle.OnWindowDestroyed del) =>
		builder.AddLifecycleEvent(nameof(AvaloniaLifecycle.OnWindowDestroyed), del);

	/// <summary>Registers a callback for Avalonia window activation.</summary>
	public static IAvaloniaLifecycleBuilder OnWindowActivated(this IAvaloniaLifecycleBuilder builder, AvaloniaLifecycle.OnWindowActivated del) =>
		builder.AddLifecycleEvent(nameof(AvaloniaLifecycle.OnWindowActivated), del);

	/// <summary>Registers a callback for Avalonia window deactivation.</summary>
	public static IAvaloniaLifecycleBuilder OnWindowDeactivated(this IAvaloniaLifecycleBuilder builder, AvaloniaLifecycle.OnWindowDeactivated del) =>
		builder.AddLifecycleEvent(nameof(AvaloniaLifecycle.OnWindowDeactivated), del);

	/// <summary>Registers a callback for Avalonia window theme changes.</summary>
	public static IAvaloniaLifecycleBuilder OnWindowThemeChanged(this IAvaloniaLifecycleBuilder builder, AvaloniaLifecycle.OnWindowThemeChanged del) =>
		builder.AddLifecycleEvent(nameof(AvaloniaLifecycle.OnWindowThemeChanged), del);

	/// <summary>Registers a callback for theme change notifications.</summary>
	public static IAvaloniaLifecycleBuilder OnThemeChanged(this IAvaloniaLifecycleBuilder builder, AvaloniaLifecycle.OnThemeChanged del) =>
		builder.AddLifecycleEvent(nameof(AvaloniaLifecycle.OnThemeChanged), del);

	private static IAvaloniaLifecycleBuilder AddLifecycleEvent(this IAvaloniaLifecycleBuilder builder, string eventName, Delegate del)
	{
		builder.AddEvent(eventName, del);
		return builder;
	}
}
