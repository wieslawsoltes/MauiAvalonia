using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Internal;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using AvaloniaSwipeContentPresenter = global::Avalonia.Controls.Presenters.ContentPresenter;
using AvaloniaSwipeBorder = global::Avalonia.Controls.Border;
using AvaloniaSwipePanel = global::Avalonia.Controls.Panel;
using AvaloniaSwipeStackPanel = global::Avalonia.Controls.StackPanel;
using AvaloniaSwipeOrientation = global::Avalonia.Layout.Orientation;
using AvaloniaPointerEventArgs = global::Avalonia.Input.PointerEventArgs;
using AvaloniaPointerPressedEventArgs = global::Avalonia.Input.PointerPressedEventArgs;
using AvaloniaPointerReleasedEventArgs = global::Avalonia.Input.PointerReleasedEventArgs;
using AvaloniaPointerCaptureLostEventArgs = global::Avalonia.Input.PointerCaptureLostEventArgs;
using AvaloniaPoint = global::Avalonia.Point;

namespace Microsoft.Maui.Avalonia.Handlers;

public sealed class AvaloniaSwipeViewHandler : AvaloniaViewHandler<ISwipeView, AvaloniaSwipeViewControl>, ISwipeViewHandler
{
	public static readonly IPropertyMapper<ISwipeView, AvaloniaSwipeViewHandler> Mapper =
		new PropertyMapper<ISwipeView, AvaloniaSwipeViewHandler>(AvaloniaContentViewHandler.Mapper)
		{
			[nameof(IContentView.Content)] = MapContent,
			[nameof(ISwipeView.LeftItems)] = MapLeftItems,
			[nameof(ISwipeView.RightItems)] = MapRightItems,
			[nameof(ISwipeView.TopItems)] = MapTopItems,
			[nameof(ISwipeView.BottomItems)] = MapBottomItems,
			[nameof(ISwipeView.IsOpen)] = MapIsOpen
		};

	public static readonly CommandMapper<ISwipeView, AvaloniaSwipeViewHandler> CommandMapper =
		new CommandMapper<ISwipeView, AvaloniaSwipeViewHandler>(ViewCommandMapper)
		{
			[nameof(ISwipeView.RequestOpen)] = MapRequestOpen,
			[nameof(ISwipeView.RequestClose)] = MapRequestClose
		};

	readonly Dictionary<OpenSwipeItem, SwipeItemsHostState> _hosts = new();
	readonly Dictionary<OpenSwipeItem, List<AvaloniaSwipeItemMenuItemHandler>> _menuHandlers = new();
	readonly Dictionary<AvaloniaSwipeItemMenuItemHandler, OpenSwipeItem> _handlerLookup = new();

	AvaloniaPoint? _gestureStart;
	SwipeDirection? _gestureDirection;
	OpenSwipeItem? _openSlot;
	bool _updatingIsOpen;

	public AvaloniaSwipeViewHandler()
		: base(Mapper, CommandMapper)
	{
	}

	protected override AvaloniaSwipeViewControl CreatePlatformView()
	{
		var control = new AvaloniaSwipeViewControl();
		_hosts[OpenSwipeItem.LeftItems] = new SwipeItemsHostState(OpenSwipeItem.LeftItems, control.LeftItemsHost);
		_hosts[OpenSwipeItem.RightItems] = new SwipeItemsHostState(OpenSwipeItem.RightItems, control.RightItemsHost);
		_hosts[OpenSwipeItem.TopItems] = new SwipeItemsHostState(OpenSwipeItem.TopItems, control.TopItemsHost);
		_hosts[OpenSwipeItem.BottomItems] = new SwipeItemsHostState(OpenSwipeItem.BottomItems, control.BottomItemsHost);

		foreach (var slot in Enum.GetValues<OpenSwipeItem>())
		{
			_menuHandlers[slot] = new List<AvaloniaSwipeItemMenuItemHandler>();
		}

		return control;
	}

	protected override void ConnectHandler(AvaloniaSwipeViewControl platformView)
	{
		base.ConnectHandler(platformView);
		platformView.PointerPressed += OnPointerPressed;
		platformView.PointerMoved += OnPointerMoved;
		platformView.PointerReleased += OnPointerReleased;
		platformView.PointerCaptureLost += OnPointerCaptureLost;
	}

	protected override void DisconnectHandler(AvaloniaSwipeViewControl platformView)
	{
		platformView.PointerPressed -= OnPointerPressed;
		platformView.PointerMoved -= OnPointerMoved;
		platformView.PointerReleased -= OnPointerReleased;
		platformView.PointerCaptureLost -= OnPointerCaptureLost;

		foreach (var slot in Enum.GetValues<OpenSwipeItem>())
			ClearSwipeItems(slot);

		base.DisconnectHandler(platformView);
	}

static void MapContent(AvaloniaSwipeViewHandler handler, ISwipeView swipeView) =>
	handler.UpdateContent();

static void MapLeftItems(AvaloniaSwipeViewHandler handler, ISwipeView swipeView) =>
	handler.UpdateSwipeItems(OpenSwipeItem.LeftItems);

static void MapRightItems(AvaloniaSwipeViewHandler handler, ISwipeView swipeView) =>
	handler.UpdateSwipeItems(OpenSwipeItem.RightItems);

static void MapTopItems(AvaloniaSwipeViewHandler handler, ISwipeView swipeView) =>
	handler.UpdateSwipeItems(OpenSwipeItem.TopItems);

static void MapBottomItems(AvaloniaSwipeViewHandler handler, ISwipeView swipeView) =>
	handler.UpdateSwipeItems(OpenSwipeItem.BottomItems);

	static void MapIsOpen(AvaloniaSwipeViewHandler handler, ISwipeView swipeView)
	{
		if (!swipeView.IsOpen)
			handler.CloseSwipe(false);
	}

	static void MapRequestOpen(AvaloniaSwipeViewHandler handler, ISwipeView swipeView, object? args)
	{
		if (args is SwipeViewOpenRequest request)
			handler.OpenSlot(request.OpenSwipeItem);
	}

	static void MapRequestClose(AvaloniaSwipeViewHandler handler, ISwipeView swipeView, object? args)
	{
		handler.CloseSwipe(args is SwipeViewCloseRequest close && close.Animated);
	}

	void UpdateContent()
	{
		if (PlatformView is null)
			return;

		if (MauiContext is null || VirtualView is null)
		{
			PlatformView.SetContent(null);
			return;
		}

		var content = ((VirtualView as IContentView)?.PresentedContent ?? VirtualView.Content) as IView;
		PlatformView.SetContent(content?.ToAvaloniaControl(MauiContext));
	}

	void UpdateSwipeItems(OpenSwipeItem slot)
	{
		if (!_hosts.TryGetValue(slot, out var host))
			return;

		ClearSwipeItems(slot);

		if (MauiContext is null || VirtualView is null)
			return;

		var items = GetItems(slot);
		host.Items = items;

		if (items is null || items.Count == 0)
		{
			if (_openSlot == slot)
				CloseSwipe(false);
			return;
		}

		foreach (var swipeItem in items)
		{
			if (swipeItem is null)
				continue;

			Control? control = null;

			if (swipeItem is ISwipeItemMenuItem menuItem)
			{
				if (menuItem.ToHandler(MauiContext) is AvaloniaSwipeItemMenuItemHandler handler)
				{
					control = handler.PlatformView;

					handler.Invoked += OnSwipeItemInvoked;
					_menuHandlers[slot].Add(handler);
					_handlerLookup[handler] = slot;
				}
			}
			else if (swipeItem is IView view)
			{
				control = new MauiItemsViewItem(view, MauiContext);
			}

			if (control is null)
				continue;

			host.Panel.Children.Add(control);
		}

		if (host.Panel.Children.Count == 0 && _openSlot == slot)
			CloseSwipe(false);
	}

	void ClearSwipeItems(OpenSwipeItem slot)
	{
		if (!_hosts.TryGetValue(slot, out var host))
			return;

		host.Panel.Children.Clear();
		host.Items = null;

		if (_menuHandlers.TryGetValue(slot, out var handlers))
		{
			foreach (var handler in handlers)
			{
				handler.Invoked -= OnSwipeItemInvoked;
				((IElementHandler)handler).DisconnectHandler();
				_handlerLookup.Remove(handler);
			}

			handlers.Clear();
		}
	}

	ISwipeItems? GetItems(OpenSwipeItem slot) =>
		slot switch
		{
			OpenSwipeItem.LeftItems => VirtualView?.LeftItems,
			OpenSwipeItem.RightItems => VirtualView?.RightItems,
			OpenSwipeItem.TopItems => VirtualView?.TopItems,
			OpenSwipeItem.BottomItems => VirtualView?.BottomItems,
			_ => null
		};

	void OnPointerPressed(object? sender, AvaloniaPointerPressedEventArgs e)
	{
		if (PlatformView is null || VirtualView is null || !VirtualView.IsEnabled)
			return;

		_gestureStart = e.GetPosition(PlatformView);
		_gestureDirection = null;
	}

	void OnPointerMoved(object? sender, AvaloniaPointerEventArgs e)
	{
		if (PlatformView is null || VirtualView is null || _gestureStart is null)
			return;

		var current = e.GetPosition(PlatformView);
		var delta = current - _gestureStart.Value;

		if (_gestureDirection is null)
		{
			if (!TryResolveDirection(delta, out var direction))
				return;

			if (!HasItems(direction))
				return;

			_gestureDirection = direction;
			VirtualView.SwipeStarted(new SwipeViewSwipeStarted(direction));
			ShowOverlay(direction);
		}

		if (_gestureDirection is SwipeDirection gestureDirection)
		{
			var offset = GetAxisOffset(gestureDirection, delta);
			VirtualView.SwipeChanging(new SwipeViewSwipeChanging(gestureDirection, offset));
		}
	}

	void OnPointerReleased(object? sender, AvaloniaPointerReleasedEventArgs e)
	{
		if (PlatformView is null || _gestureStart is null || _gestureDirection is null)
		{
			ResetGesture();
			return;
		}

		var current = e.GetPosition(PlatformView);
		var delta = current - _gestureStart.Value;
		var direction = _gestureDirection.Value;
		var offset = GetAxisOffset(direction, delta);
		var shouldRemainOpen = ShouldKeepOpen(direction, offset);

		if (shouldRemainOpen)
		{
			ShowOverlay(direction);
			SetIsOpen(true);
		}
		else
		{
			CloseSwipe(true);
		}

		VirtualView?.SwipeEnded(new SwipeViewSwipeEnded(direction, shouldRemainOpen));

		ResetGesture();
	}

	void OnPointerCaptureLost(object? sender, AvaloniaPointerCaptureLostEventArgs e) =>
		ResetGesture();

	void ResetGesture()
	{
		_gestureStart = null;
		_gestureDirection = null;
	}

	void ShowOverlay(SwipeDirection direction)
	{
		if (PlatformView is null)
			return;

		var slot = SlotForDirection(direction);
		if (PlatformView.ShowOverlay(slot))
			_openSlot = slot;
	}

	void CloseSwipe(bool animated)
	{
		if (PlatformView is null)
			return;

		_ = animated;

		PlatformView.HideOverlay();
		_openSlot = null;
		SetIsOpen(false);
	}

	void OpenSlot(OpenSwipeItem slot)
	{
		if (PlatformView is null)
			return;

		if (!PlatformView.ShowOverlay(slot))
			return;

		_openSlot = slot;
		var direction = DirectionFromSlot(slot);
		_gestureDirection = direction;
		SetIsOpen(true);

		VirtualView?.SwipeStarted(new SwipeViewSwipeStarted(direction));
		VirtualView?.SwipeChanging(new SwipeViewSwipeChanging(direction, 0));
		VirtualView?.SwipeEnded(new SwipeViewSwipeEnded(direction, true));
	}

	void SetIsOpen(bool isOpen)
	{
		if (VirtualView is null || _updatingIsOpen || VirtualView.IsOpen == isOpen)
			return;

		try
		{
			_updatingIsOpen = true;
			VirtualView.IsOpen = isOpen;
		}
		finally
		{
			_updatingIsOpen = false;
		}
	}

	void OnSwipeItemInvoked(object? sender, EventArgs e)
	{
		if (sender is not AvaloniaSwipeItemMenuItemHandler handler)
			return;

		if (!_handlerLookup.TryGetValue(handler, out var slot))
			return;

		if (!_hosts.TryGetValue(slot, out var host) || host.Items is null)
			return;

		var behavior = host.Items.SwipeBehaviorOnInvoked;
		var mode = host.Items.Mode;
		var shouldClose = behavior switch
		{
			SwipeBehaviorOnInvoked.Close => true,
			SwipeBehaviorOnInvoked.RemainOpen => false,
			SwipeBehaviorOnInvoked.Auto => mode != SwipeMode.Execute,
			_ => true
		};

		if (shouldClose)
			CloseSwipe(true);
	}

	bool HasItems(SwipeDirection direction)
	{
		var slot = SlotForDirection(direction);
		return _hosts.TryGetValue(slot, out var host) && host.Panel.Children.Count > 0;
	}

	bool TryResolveDirection(Vector delta, out SwipeDirection direction)
	{
		var threshold = Math.Max(12, VirtualView?.Threshold ?? 0);
		if (Math.Abs(delta.X) < threshold && Math.Abs(delta.Y) < threshold)
		{
			direction = default;
			return false;
		}

		if (Math.Abs(delta.X) >= Math.Abs(delta.Y))
			direction = delta.X >= 0 ? SwipeDirection.Right : SwipeDirection.Left;
		else
			direction = delta.Y >= 0 ? SwipeDirection.Down : SwipeDirection.Up;

		return true;
	}

	static double GetAxisOffset(SwipeDirection direction, Vector delta) =>
		direction switch
		{
			SwipeDirection.Left => delta.X,
			SwipeDirection.Right => delta.X,
			SwipeDirection.Up => delta.Y,
			SwipeDirection.Down => delta.Y,
			_ => 0
		};

	bool ShouldKeepOpen(SwipeDirection direction, double offset)
	{
		var threshold = Math.Max(24, VirtualView?.Threshold ?? 0);
		var magnitude = Math.Abs(offset);

		if (direction is SwipeDirection.Left or SwipeDirection.Up)
		{
			if (offset >= 0)
				return false;
		}
		else
		{
			if (offset <= 0)
				return false;
		}

		return magnitude >= threshold && HasItems(direction);
	}

	static OpenSwipeItem SlotForDirection(SwipeDirection direction) =>
		direction switch
		{
			SwipeDirection.Right => OpenSwipeItem.LeftItems,
			SwipeDirection.Left => OpenSwipeItem.RightItems,
			SwipeDirection.Down => OpenSwipeItem.TopItems,
			SwipeDirection.Up => OpenSwipeItem.BottomItems,
			_ => OpenSwipeItem.LeftItems
		};

	static SwipeDirection DirectionFromSlot(OpenSwipeItem slot) =>
		slot switch
		{
			OpenSwipeItem.LeftItems => SwipeDirection.Right,
			OpenSwipeItem.RightItems => SwipeDirection.Left,
			OpenSwipeItem.TopItems => SwipeDirection.Down,
			OpenSwipeItem.BottomItems => SwipeDirection.Up,
			_ => SwipeDirection.Right
		};
}

sealed class SwipeItemsHostState
{
	public SwipeItemsHostState(OpenSwipeItem slot, AvaloniaSwipePanel panel)
	{
		Slot = slot;
		Panel = panel;
	}

	public OpenSwipeItem Slot { get; }
	public AvaloniaSwipePanel Panel { get; }
	public ISwipeItems? Items { get; set; }
}

public sealed class AvaloniaSwipeViewControl : AvaloniaGrid
{
	readonly AvaloniaSwipeContentPresenter _contentPresenter;
	readonly AvaloniaSwipeBorder _leftOverlay;
	readonly AvaloniaSwipeBorder _rightOverlay;
	readonly AvaloniaSwipeBorder _topOverlay;
	readonly AvaloniaSwipeBorder _bottomOverlay;
	OpenSwipeItem? _activeSlot;

	public AvaloniaSwipeViewControl()
	{
		ClipToBounds = true;
		Background = Brushes.Transparent;

		_contentPresenter = new AvaloniaSwipeContentPresenter
		{
			HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
			VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch
		};

		_leftOverlay = CreateOverlay(global::Avalonia.Layout.HorizontalAlignment.Left, global::Avalonia.Layout.VerticalAlignment.Stretch, AvaloniaSwipeOrientation.Vertical);
		_rightOverlay = CreateOverlay(global::Avalonia.Layout.HorizontalAlignment.Right, global::Avalonia.Layout.VerticalAlignment.Stretch, AvaloniaSwipeOrientation.Vertical);
		_topOverlay = CreateOverlay(global::Avalonia.Layout.HorizontalAlignment.Stretch, global::Avalonia.Layout.VerticalAlignment.Top, AvaloniaSwipeOrientation.Horizontal);
		_bottomOverlay = CreateOverlay(global::Avalonia.Layout.HorizontalAlignment.Stretch, global::Avalonia.Layout.VerticalAlignment.Bottom, AvaloniaSwipeOrientation.Horizontal);

		Children.Add(_leftOverlay);
		Children.Add(_rightOverlay);
		Children.Add(_topOverlay);
		Children.Add(_bottomOverlay);
		Children.Add(_contentPresenter);

		_contentPresenter.ZIndex = 1;
	}

	public AvaloniaSwipePanel LeftItemsHost => (AvaloniaSwipePanel)_leftOverlay.Child!;
	public AvaloniaSwipePanel RightItemsHost => (AvaloniaSwipePanel)_rightOverlay.Child!;
	public AvaloniaSwipePanel TopItemsHost => (AvaloniaSwipePanel)_topOverlay.Child!;
	public AvaloniaSwipePanel BottomItemsHost => (AvaloniaSwipePanel)_bottomOverlay.Child!;

	public void SetContent(global::Avalonia.Controls.Control? control) =>
		_contentPresenter.Content = control;

	public bool ShowOverlay(OpenSwipeItem slot)
	{
		if (!HasItems(slot))
		{
			HideOverlay();
			return false;
		}

		_activeSlot = slot;
		UpdateOverlayVisibility();
		return true;
	}

	public void HideOverlay()
	{
		_activeSlot = null;
		UpdateOverlayVisibility();
	}

	bool HasItems(OpenSwipeItem slot) =>
		slot switch
		{
			OpenSwipeItem.LeftItems => LeftItemsHost.Children.Count > 0,
			OpenSwipeItem.RightItems => RightItemsHost.Children.Count > 0,
			OpenSwipeItem.TopItems => TopItemsHost.Children.Count > 0,
			OpenSwipeItem.BottomItems => BottomItemsHost.Children.Count > 0,
			_ => false
		};

	void UpdateOverlayVisibility()
	{
		_leftOverlay.IsVisible = _activeSlot == OpenSwipeItem.LeftItems && LeftItemsHost.Children.Count > 0;
		_rightOverlay.IsVisible = _activeSlot == OpenSwipeItem.RightItems && RightItemsHost.Children.Count > 0;
		_topOverlay.IsVisible = _activeSlot == OpenSwipeItem.TopItems && TopItemsHost.Children.Count > 0;
		_bottomOverlay.IsVisible = _activeSlot == OpenSwipeItem.BottomItems && BottomItemsHost.Children.Count > 0;
	}

	static AvaloniaSwipeBorder CreateOverlay(global::Avalonia.Layout.HorizontalAlignment horizontalAlignment, global::Avalonia.Layout.VerticalAlignment verticalAlignment, AvaloniaSwipeOrientation orientation) =>
		new()
		{
			Background = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromArgb(0xB0, 0x2C, 0x2C, 0x2C)),
			Padding = new AvaloniaThickness(8),
			HorizontalAlignment = horizontalAlignment,
			VerticalAlignment = verticalAlignment,
			IsVisible = false,
			Child = new AvaloniaSwipeStackPanel
			{
				Orientation = orientation,
				Spacing = 8
			}
		};
}
