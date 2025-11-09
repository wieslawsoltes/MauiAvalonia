using System;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Internal;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Hosting;
using AvaloniaApplication = Avalonia.Application;

namespace Microsoft.Maui.Avalonia.Handlers;

/// <summary>
/// Minimal application handler that keeps MAUI's <see cref="IApplication"/> attached to the Avalonia lifetime.
/// </summary>
public class AvaloniaApplicationHandler : ElementHandler<IApplication, AvaloniaApplication>
{
	public static IPropertyMapper<IApplication, AvaloniaApplicationHandler> Mapper =
		new PropertyMapper<IApplication, AvaloniaApplicationHandler>(ElementMapper);

	public static CommandMapper<IApplication, AvaloniaApplicationHandler> CommandMapper = new(ElementCommandMapper)
	{
		[nameof(IApplication.OpenWindow)] = MapOpenWindow,
		[nameof(IApplication.CloseWindow)] = MapCloseWindow,
	};

	public AvaloniaApplicationHandler()
		: base(Mapper, CommandMapper)
	{
	}

	protected override AvaloniaApplication CreatePlatformElement()
	{
		if (MauiContext is MauiContext context)
		{
			var app = context.Services.GetService(typeof(AvaloniaApplication)) as AvaloniaApplication;
			if (app is not null)
				return app;
		}

		return AvaloniaApplication.Current ?? throw new InvalidOperationException("Avalonia Application is not available.");
	}

	static void MapOpenWindow(AvaloniaApplicationHandler handler, IApplication application, object? args)
	{
		if (handler.MauiContext?.Services.GetService<IAvaloniaWindowHost>() is not { } windowHost)
			return;

		windowHost.OpenWindow(application, args as OpenWindowRequest);
	}

	static void MapCloseWindow(AvaloniaApplicationHandler handler, IApplication application, object? args)
	{
		if (args is not IWindow window)
			return;

		if (handler.MauiContext?.Services.GetService<IAvaloniaWindowHost>() is { } windowHost)
		{
			windowHost.CloseWindow(window);
			return;
		}

		if (window.Handler?.PlatformView is AvaloniaWindowControl platformWindow)
			platformWindow.Close();
	}

}
