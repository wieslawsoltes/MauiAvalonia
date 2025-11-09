using System;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Avalonia.Internal;

using AvaloniaApplication = Avalonia.Application;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace Microsoft.Maui.Avalonia;

internal static class AvaloniaMauiContextExtensions
{
	public static IMauiContext MakeApplicationScope(this IMauiContext rootContext, AvaloniaApplication application)
	{
		ArgumentNullException.ThrowIfNull(rootContext);
		ArgumentNullException.ThrowIfNull(application);

		var applicationContext = new MauiContext(rootContext.Services);
		MauiContextAccessor.TryAddSpecific(applicationContext, application);
		MauiServiceUtilities.InitializeScopedServices(applicationContext.Services);
		return applicationContext;
	}

	public static IMauiContext MakeWindowScope(this IMauiContext applicationContext, AvaloniaWindow window, out IServiceScope scope)
	{
		ArgumentNullException.ThrowIfNull(applicationContext);
		ArgumentNullException.ThrowIfNull(window);

		scope = applicationContext.Services.CreateScope();
		var windowContext = new MauiContext(scope.ServiceProvider);

		MauiContextAccessor.TrySetWindowScope(windowContext, scope);
		MauiContextAccessor.TryAddSpecific(windowContext, window);
		MauiContextAccessor.TryAddWeakSpecific(windowContext, window);
		MauiServiceUtilities.InitializeScopedServices(scope.ServiceProvider);

		return windowContext;
	}
}
