using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Avalonia.Handlers;

public sealed class AvaloniaFlyoutViewHandler : ViewHandler<IFlyoutView, SplitView>, IFlyoutViewHandler
{
	readonly AvaloniaShellFlyoutPresenter _defaultShellFlyoutPresenter = new();
	Shell? _trackedShell;
	IShellController? _shellController;

	static readonly IPropertyMapper<IFlyoutView, IFlyoutViewHandler> _mapper = new PropertyMapper<IFlyoutView, IFlyoutViewHandler>(ViewHandler.ViewMapper)
	{
		[nameof(IFlyoutView.Flyout)] = MapFlyout,
		[nameof(IFlyoutView.Detail)] = MapDetail,
		[nameof(IFlyoutView.IsPresented)] = MapIsPresented,
		[nameof(IFlyoutView.FlyoutWidth)] = MapFlyoutWidth,
		[nameof(IFlyoutView.FlyoutBehavior)] = MapFlyoutBehavior,
		[nameof(IFlyoutView.IsGestureEnabled)] = MapIsGestureEnabled
	};

	public AvaloniaFlyoutViewHandler()
		: base(_mapper)
	{
	}

	protected override SplitView CreatePlatformView() => new SplitView
	{
		DisplayMode = SplitViewDisplayMode.Inline,
		IsPaneOpen = false,
		OpenPaneLength = 320
	};

	static void MapFlyout(IFlyoutViewHandler handler, IFlyoutView view)
	{
		if (handler is not AvaloniaFlyoutViewHandler platformHandler || handler.MauiContext is null)
			return;

		platformHandler.EnsureShellSubscriptions(view);
		platformHandler.PlatformView.Pane = platformHandler.ResolveFlyoutPane(view);
	}

	static void MapDetail(IFlyoutViewHandler handler, IFlyoutView view)
	{
		if (handler is not AvaloniaFlyoutViewHandler platformHandler || handler.MauiContext is null)
			return;

		platformHandler.EnsureShellSubscriptions(view);

		var detailView = view.Detail ?? TryResolveShellDetail(view);
		platformHandler.PlatformView.Content = detailView?.ToAvaloniaControl(handler.MauiContext);
	}

	static void MapIsPresented(IFlyoutViewHandler handler, IFlyoutView view) =>
		(handler as AvaloniaFlyoutViewHandler)?.PlatformView.IsPaneOpen = view.IsPresented;

	static void MapFlyoutWidth(IFlyoutViewHandler handler, IFlyoutView view)
	{
		if (handler is not AvaloniaFlyoutViewHandler platformHandler)
			return;

		if (view.FlyoutWidth > 0)
			platformHandler.PlatformView.OpenPaneLength = view.FlyoutWidth;
	}

	static void MapFlyoutBehavior(IFlyoutViewHandler handler, IFlyoutView view)
	{
		if (handler is not AvaloniaFlyoutViewHandler platformHandler)
			return;

		platformHandler.PlatformView.DisplayMode = view.FlyoutBehavior switch
		{
			FlyoutBehavior.Locked => SplitViewDisplayMode.Inline,
			FlyoutBehavior.Disabled => SplitViewDisplayMode.Overlay,
			_ => SplitViewDisplayMode.Overlay
		};
	}

	static void MapIsGestureEnabled(IFlyoutViewHandler handler, IFlyoutView view)
	{
		// gestures are handled by Avalonia SplitView internally; nothing to map yet
	}

	static IView? TryResolveShellDetail(IFlyoutView view)
	{
		if (view is not Shell shell)
			return null;

		if (shell.CurrentPage is IView pageView)
			return pageView;

		var shellContent = shell.CurrentItem?.CurrentItem?.CurrentItem
			?? shell.Items?.FirstOrDefault()?.Items?.FirstOrDefault()?.Items?.FirstOrDefault();

		if (shellContent is null)
			return null;

		if (shellContent is IShellContentController controller)
			return controller.GetOrCreateContent() as IView;

		return shellContent.Content as IView;
	}

	Control? ResolveFlyoutPane(IFlyoutView view)
	{
		if (MauiContext is null)
			return null;

		if (view.Flyout is IView flyoutView)
			return flyoutView.ToAvaloniaControl(MauiContext);

		if (_trackedShell is not null)
		{
			_defaultShellFlyoutPresenter.AttachShell(_trackedShell, _shellController);
			return _defaultShellFlyoutPresenter;
		}

		return null;
	}

	void EnsureShellSubscriptions(IFlyoutView view)
	{
		if (_trackedShell is not null && ReferenceEquals(_trackedShell, view))
			return;

		ResetShellSubscriptions();

		if (view is not Shell shell)
			return;

		_trackedShell = shell;
		shell.PropertyChanged += OnShellPropertyChanged;
		
		if (shell is IShellController controller)
		{
			_shellController = controller;
			controller.StructureChanged += OnShellStructureChanged;
			controller.FlyoutItemsChanged += OnShellStructureChanged;
		}

		_defaultShellFlyoutPresenter.AttachShell(shell, _shellController);
	}

	void ResetShellSubscriptions()
	{
		if (_trackedShell is not null)
		{
			_trackedShell.PropertyChanged -= OnShellPropertyChanged;
			_trackedShell = null;
		}

		if (_shellController is not null)
		{
			_shellController.StructureChanged -= OnShellStructureChanged;
			_shellController.FlyoutItemsChanged -= OnShellStructureChanged;
			_shellController = null;
		}

		_defaultShellFlyoutPresenter.AttachShell(null, null);
	}

	void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_trackedShell is null)
			return;

		if (e.PropertyName == nameof(Shell.CurrentItem) ||
			e.PropertyName == nameof(Shell.CurrentState) ||
			e.PropertyName == "CurrentPage")
		{
			MapDetail(this, _trackedShell);
			_defaultShellFlyoutPresenter.UpdateSelection();
		}
	}

	void OnShellStructureChanged(object? sender, EventArgs e)
	{
		if (_trackedShell is null)
			return;

		MapDetail(this, _trackedShell);
		_defaultShellFlyoutPresenter.UpdateItems();
	}

	protected override void DisconnectHandler(SplitView platformView)
	{
		ResetShellSubscriptions();
		base.DisconnectHandler(platformView);
	}

	IFlyoutView IFlyoutViewHandler.VirtualView => VirtualView;

	object IFlyoutViewHandler.PlatformView => PlatformView!;
}
