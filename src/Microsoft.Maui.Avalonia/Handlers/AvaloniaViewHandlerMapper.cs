using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Input;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Navigation;
using Microsoft.Maui.Avalonia.Platform;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace Microsoft.Maui.Avalonia.Handlers;

internal static class AvaloniaViewHandlerMapper
{
	static bool _initialized;

	public static void EnsureInitialized()
	{
		if (_initialized)
			return;

		_initialized = true;

		ViewHandler.ViewMapper.AppendToMapping<IView, IViewHandler>(nameof(IView.Background), MapBackground);
		ViewHandler.ViewMapper.AppendToMapping<IView, IViewHandler>(nameof(IView.Opacity), MapOpacity);
		ViewHandler.ViewMapper.AppendToMapping<IView, IViewHandler>(nameof(IView.IsEnabled), MapIsEnabled);
		ViewHandler.ViewMapper.AppendToMapping<IView, IViewHandler>(nameof(IView.AutomationId), MapAutomationId);
		ViewHandler.ViewMapper.AppendToMapping<IView, IViewHandler>(nameof(IView.InputTransparent), MapInputTransparent);
		ViewHandler.ViewMapper.AppendToMapping<IView, IViewHandler>(nameof(IView.Semantics), MapSemantics);
		ViewHandler.ViewMapper.AppendToMapping<IView, IViewHandler>(nameof(IContextFlyoutElement.ContextFlyout), MapContextFlyout);
		ViewHandler.ViewMapper.AppendToMapping<IView, IViewHandler>(nameof(IToolbarElement.Toolbar), MapToolbarElement);
		ViewHandler.ViewCommandMapper.AppendToMapping<IView, IViewHandler>(nameof(IView.Focus), MapFocus);
		ViewHandler.ViewCommandMapper.AppendToMapping<IView, IViewHandler>(nameof(IView.Unfocus), MapUnfocus);
	}

	static void MapBackground(IViewHandler handler, IView view)
	{
		if (handler?.PlatformView is Control control)
			control.UpdateBackground(view);
	}

	static void MapOpacity(IViewHandler handler, IView view)
	{
		if (handler?.PlatformView is Control control)
			control.UpdateOpacity(view);
	}

	static void MapIsEnabled(IViewHandler handler, IView view)
	{
		if (handler?.PlatformView is Control control)
			control.UpdateIsEnabled(view);
	}

	static void MapAutomationId(IViewHandler handler, IView view)
	{
		if (handler?.PlatformView is Control control)
			control.UpdateAutomationId(view);
	}

	static void MapInputTransparent(IViewHandler handler, IView view)
	{
		if (handler?.PlatformView is Control control)
			control.UpdateInputTransparent(view);
	}

	static void MapSemantics(IViewHandler handler, IView view)
	{
		if (handler?.PlatformView is Control control)
			control.ApplySemantics(view);
	}

	static void MapContextFlyout(IViewHandler handler, IView view)
	{
		if (handler?.PlatformView is not Control control)
			return;

		if (handler.MauiContext is null)
		{
			control.ContextMenu = null;
			return;
		}

		if (view is not IContextFlyoutElement flyoutElement || flyoutElement.ContextFlyout is null)
		{
			control.ContextMenu = null;
			return;
		}

		if (flyoutElement.ContextFlyout is IMenuFlyout menuFlyout)
		{
			control.ContextMenu = AvaloniaMenuBuilder.BuildContextMenu(menuFlyout, handler.MauiContext);
		}
		else
		{
			control.ContextMenu = null;
		}
	}

	static void MapToolbarElement(IViewHandler handler, IView view)
	{
		if (handler?.MauiContext is null)
			return;

		if (view is not IToolbarElement toolbarElement)
			return;

		var navigationRoot = handler.MauiContext.Services.GetService(typeof(IAvaloniaNavigationRoot)) as IAvaloniaNavigationRoot;
		if (navigationRoot is null)
			return;

		if (toolbarElement.Toolbar is null)
		{
			navigationRoot.SetToolbar(null);
			return;
		}

		var toolbarHandler = toolbarElement.Toolbar.ToHandler(handler.MauiContext);
		if (toolbarHandler.PlatformView is Control toolbarControl)
		{
			navigationRoot.SetToolbar(toolbarControl);
		}
	}

	static void MapFocus(IViewHandler handler, IView view, object? args)
	{
		if (handler?.PlatformView is not Control control)
			return;

		if (args is not FocusRequest request)
			return;

		var result = control.Focus();
		request.TrySetResult(result);
	}

	static void MapUnfocus(IViewHandler handler, IView view, object? args)
	{
		if (handler?.PlatformView is not Control control)
			return;

		var focusManager = TopLevel.GetTopLevel(control)?.FocusManager;
		focusManager?.ClearFocus();
	}
}
