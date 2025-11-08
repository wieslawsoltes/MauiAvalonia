using System;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Avalonia.Handlers;
using Microsoft.Maui.Avalonia.Internal;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Maui.Platform;
using AvaloniaApplication = Avalonia.Application;

namespace Microsoft.Maui;

/// <summary>
/// Bootstraps the MAUI application model inside an Avalonia <see cref="Application"/>.
/// </summary>
public abstract class AvaloniaMauiApplication : AvaloniaApplication, IPlatformApplication
{
	MauiApp? _mauiApp;
	IMauiContext? _rootContext;
	IMauiContext? _applicationContext;
	IServiceProvider? _services;
	IApplication? _application;
	bool _initialized;
	bool _themeSubscribed;

	/// <summary>
	/// Initializes a new instance and sets the global <see cref="IPlatformApplication.Current"/> reference.
	/// </summary>
	protected AvaloniaMauiApplication()
	{
		IPlatformApplication.Current = this;
	}

	/// <summary>
	/// Implementations must build and return the configured <see cref="MauiApp"/>.
	/// </summary>
	protected abstract MauiApp CreateMauiApp();

	/// <summary>
	/// Builds the MAUI application, sets up the DI scopes, and attaches the Avalonia lifetime
	/// so MAUI windows can be created. Subsequent calls are ignored.
	/// </summary>
	protected virtual void EnsureMauiInitialized()
	{
		if (_initialized)
			return;

		_mauiApp = CreateMauiApp() ?? throw new InvalidOperationException("CreateMauiApp() must return a MauiApp instance.");
		_rootContext = new MauiContext(_mauiApp.Services);
		_applicationContext = _rootContext;
		MauiContextAccessor.TryAddSpecific(_applicationContext, this);
		_services = _applicationContext.Services;

		MauiServiceUtilities.InitializeAppServices(_mauiApp);
		LifecycleInvoker.Invoke<AvaloniaLifecycle.OnInitializing>(_services, del => del(this));

		_application = _services.GetRequiredService<IApplication>();
		InitializeApplicationHandler();

		var windowHost = _services.GetService<IAvaloniaWindowHost>();
		windowHost?.AttachLifetime(this, _application, _applicationContext);

		if (!_themeSubscribed)
		{
			ActualThemeVariantChanged += OnActualThemeVariantChanged;
			_themeSubscribed = true;
		}

		NotifyAppThemeChanged();

		_initialized = true;
	}

	/// <inheritdoc/>
	public override void OnFrameworkInitializationCompleted()
	{
		EnsureMauiInitialized();
		LifecycleInvoker.Invoke<AvaloniaLifecycle.OnFrameworkInitialized>(_services, del => del(this));
		base.OnFrameworkInitializationCompleted();
	}

	IServiceProvider IPlatformApplication.Services =>
		_services ?? throw new InvalidOperationException("The MAUI services container has not been initialized.");

	IApplication IPlatformApplication.Application =>
		_application ?? throw new InvalidOperationException("The MAUI application handler has not been initialized.");

	/// <summary>
	/// Provides derived classes or tests a way to inject pre-built services.
	/// </summary>
	protected void SetMauiEnvironment(IServiceProvider services, IApplication application)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
		_application = application ?? throw new ArgumentNullException(nameof(application));
		_initialized = services is not null && application is not null;
	}

	/// <summary>
	/// Exposes the application-level MAUI context for subclasses.
	/// </summary>
	protected IMauiContext ApplicationContext =>
		_applicationContext ?? throw new InvalidOperationException("The MAUI application context has not been created.");

	void OnActualThemeVariantChanged(object? sender, EventArgs e)
	{
		NotifyAppThemeChanged();
		LifecycleInvoker.Invoke<AvaloniaLifecycle.OnThemeChanged>(_services, del => del(this, e));
	}

	void NotifyAppThemeChanged() =>
		_application?.ThemeChanged();

	void InitializeApplicationHandler()
	{
		if (_application is null || _applicationContext is null)
			throw new InvalidOperationException("The MAUI application context has not been created.");

		var handler = new AvaloniaApplicationHandler();
		handler.SetMauiContext(_applicationContext);
		handler.SetVirtualView(_application);
		_application.Handler = handler;
	}
}
