using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using AvaloniaBlazorWebView;
using AvaloniaBlazorWebView.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Maui.Avalonia.Hosting;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Handlers;
using WebViewCore.Events;
using AvaloniaBlazorControl = AvaloniaBlazorWebView.BlazorWebView;

namespace Microsoft.Maui.Avalonia.Handlers;

public class AvaloniaBlazorWebViewHandler : AvaloniaViewHandler<IBlazorWebView, AvaloniaBlazorControl>
{
	static readonly PropertyMapper<IBlazorWebView, AvaloniaBlazorWebViewHandler> PropertyMapper = new(ViewHandler.ViewMapper)
	{
		[nameof(IBlazorWebView.HostPage)] = MapHostPage,
		[nameof(IBlazorWebView.RootComponents)] = MapRootComponents
	};

	static readonly MethodInfo? CreateUrlLoadingArgsMethod = typeof(UrlLoadingEventArgs).GetMethod("CreateWithDefaultLoadingStrategy", BindingFlags.NonPublic | BindingFlags.Static);

	readonly Dictionary<RootComponent, BlazorRootComponent> _rootComponentMap = new();

	public AvaloniaBlazorWebViewHandler()
		: base(PropertyMapper)
	{
	}

	protected override AvaloniaBlazorControl CreatePlatformView()
	{
		var options = new BlazorWebViewHostOptions(VirtualView?.StartPath, ResolveContentRoot(VirtualView), "localhost", null);
		using var scope = AvaloniaBlazorWebViewScope.Push(options);
		return new AvaloniaBlazorControl();
	}

	protected override void ConnectHandler(AvaloniaBlazorControl platformView)
	{
		base.ConnectHandler(platformView);
		platformView.NavigationStarting += OnNavigationStarting;
		platformView.WebViewCreating += OnWebViewCreating;
		platformView.WebViewCreated += OnWebViewCreated;

		if (VirtualView is not null)
		{
			VirtualView.RootComponents.CollectionChanged += OnRootComponentsCollectionChanged;
			SyncRootComponents();
			platformView.HostPage = VirtualView.HostPage;
		}
	}

	protected override void DisconnectHandler(AvaloniaBlazorControl platformView)
	{
		platformView.NavigationStarting -= OnNavigationStarting;
		platformView.WebViewCreating -= OnWebViewCreating;
		platformView.WebViewCreated -= OnWebViewCreated;

		if (VirtualView is not null)
		{
			VirtualView.RootComponents.CollectionChanged -= OnRootComponentsCollectionChanged;
		}

		_rootComponentMap.Clear();
		platformView.RootComponents.Clear();
		base.DisconnectHandler(platformView);
	}

	static void MapHostPage(AvaloniaBlazorWebViewHandler handler, IBlazorWebView view)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.HostPage = view.HostPage;
	}

	static void MapRootComponents(AvaloniaBlazorWebViewHandler handler, IBlazorWebView view)
	{
		handler.SyncRootComponents();
	}

	void OnRootComponentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		SyncRootComponents();
	}

	void SyncRootComponents()
	{
		if (PlatformView is null || VirtualView is null)
			return;

		PlatformView.RootComponents.CollectionChanged -= OnBlazorRootComponentsChanged;
		PlatformView.RootComponents.Clear();
		_rootComponentMap.Clear();

		foreach (var component in VirtualView.RootComponents)
		{
			var mapped = CreateBlazorRootComponent(component);
			PlatformView.RootComponents.Add(mapped);
			_rootComponentMap[component] = mapped;
		}

		PlatformView.RootComponents.CollectionChanged += OnBlazorRootComponentsChanged;
	}

	void OnBlazorRootComponentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		// keep map in sync if Avalonia collection changes internally
	}

	static BlazorRootComponent CreateBlazorRootComponent(RootComponent rootComponent)
	{
		var mapped = new BlazorRootComponent
		{
			Selector = rootComponent.Selector ?? string.Empty,
			ComponentType = rootComponent.ComponentType ?? throw new InvalidOperationException("ComponentType cannot be null."),
			Parameters = rootComponent.Parameters
		};

		return mapped;
	}

	void OnWebViewCreating(object? sender, WebViewCreatingEventArgs e)
	{
		VirtualView?.BlazorWebViewInitializing(new BlazorWebViewInitializingEventArgs());
	}

	void OnWebViewCreated(object? sender, WebViewCreatedEventArgs e)
	{
		VirtualView?.BlazorWebViewInitialized(new BlazorWebViewInitializedEventArgs());
	}

	void OnNavigationStarting(object? sender, WebViewUrlLoadingEventArg e)
	{
		if (VirtualView is null || e.Url is null)
			return;

		var args = CreateUrlLoadingArgs(e.Url);
		if (args is null)
			return;

		VirtualView.UrlLoading(args);

		if (args.UrlLoadingStrategy == UrlLoadingStrategy.OpenExternally)
		{
			e.Cancel = true;
			_ = Launcher.OpenAsync(args.Url);
		}
	}

	UrlLoadingEventArgs? CreateUrlLoadingArgs(Uri uri)
	{
		if (CreateUrlLoadingArgsMethod is null)
			return null;

		var appOrigin = new Uri("https://localhost/");
		return CreateUrlLoadingArgsMethod.Invoke(null, new object[] { uri, appOrigin }) as UrlLoadingEventArgs;
	}

	static string? ResolveContentRoot(IBlazorWebView? view)
	{
		if (view?.HostPage is null)
			return null;

		var normalized = view.HostPage.Replace('\\', Path.DirectorySeparatorChar);
		var directory = Path.GetDirectoryName(normalized);
		return string.IsNullOrWhiteSpace(directory) ? "wwwroot" : directory;
	}
}
