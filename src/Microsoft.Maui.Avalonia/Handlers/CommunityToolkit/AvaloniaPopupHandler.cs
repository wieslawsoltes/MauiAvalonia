using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Avalonia.Graphics;
using Microsoft.Maui.Avalonia.Internal;
using Microsoft.Maui.Avalonia.Navigation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Primitives;
using AvaloniaBorder = global::Avalonia.Controls.Border;
using AvaloniaPopupControl = global::Avalonia.Controls.Primitives.Popup;
using AvaloniaPopupAnchor = global::Avalonia.Controls.Primitives.PopupPositioning.PopupAnchor;

namespace Microsoft.Maui.Avalonia.Handlers;

public sealed class AvaloniaPopupHandler : ElementHandler<IPopup, AvaloniaPopupControl>
{
	public static readonly IPropertyMapper<IPopup, AvaloniaPopupHandler> Mapper =
		new PropertyMapper<IPopup, AvaloniaPopupHandler>(ElementMapper)
		{
			[nameof(IPopup.Content)] = MapContent,
			[nameof(IPopup.Color)] = MapColor,
			[nameof(IPopup.Size)] = MapSize,
			[nameof(IPopup.Anchor)] = MapPlacement,
			[nameof(IPopup.HorizontalOptions)] = MapPlacement,
			[nameof(IPopup.VerticalOptions)] = MapPlacement,
			[nameof(IPopup.CanBeDismissedByTappingOutsideOfPopup)] = MapDismissBehavior
		};

	public static readonly CommandMapper<IPopup, AvaloniaPopupHandler> CommandMapper =
		new CommandMapper<IPopup, AvaloniaPopupHandler>(ElementCommandMapper)
		{
			[nameof(IPopup.OnOpened)] = MapOpened,
			[nameof(IPopup.OnClosed)] = MapClosed,
			[nameof(IPopup.OnDismissedByTappingOutsideOfPopup)] = MapDismissedOutside
		};

	readonly AvaloniaBorder _contentHost = new()
	{
		Padding = new global::Avalonia.Thickness(0),
		HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
		VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch
	};

	bool _isClosingInternally;

	public AvaloniaPopupHandler()
		: base(Mapper, CommandMapper)
	{
	}

	protected override AvaloniaPopupControl CreatePlatformElement()
	{
		var popup = new AvaloniaPopupControl
		{
			Child = _contentHost,
			Topmost = true,
			IsHitTestVisible = true,
			Placement = PlacementMode.Center
		};

		popup.Opened += OnPlatformOpened;
		popup.Closed += OnPlatformClosed;
		return popup;
	}

	protected override void DisconnectHandler(AvaloniaPopupControl platformView)
	{
		platformView.Opened -= OnPlatformOpened;
		platformView.Closed -= OnPlatformClosed;
		base.DisconnectHandler(platformView);
	}

	static void MapContent(AvaloniaPopupHandler handler, IPopup popup) =>
		handler.UpdateContent();

	static void MapColor(AvaloniaPopupHandler handler, IPopup popup) =>
		handler.UpdateColor();

	static void MapSize(AvaloniaPopupHandler handler, IPopup popup) =>
		handler.UpdateSize();

	static void MapPlacement(AvaloniaPopupHandler handler, IPopup popup) =>
		handler.UpdatePlacement();

	static void MapDismissBehavior(AvaloniaPopupHandler handler, IPopup popup) =>
		handler.UpdateDismissBehavior();

	static void MapOpened(AvaloniaPopupHandler handler, IPopup popup, object? args) =>
		handler.OpenPopup();

	static void MapClosed(AvaloniaPopupHandler handler, IPopup popup, object? args) =>
		handler.ClosePopup(args);

	static void MapDismissedOutside(AvaloniaPopupHandler handler, IPopup popup, object? args) =>
		popup.OnDismissedByTappingOutsideOfPopup();

	void UpdateContent()
	{
		if (PlatformView is null || MauiContext is null)
		{
			_contentHost.Child = null;
			return;
		}

		var contentView = VirtualView?.Content;
		_contentHost.Child = contentView?.ToAvaloniaControl(MauiContext);
	}

	void UpdateColor()
	{
		if (VirtualView is null)
			return;

		_contentHost.Background = (VirtualView.Color ?? Microsoft.Maui.Graphics.Colors.Transparent).ToAvaloniaBrush();
	}

	void UpdateSize()
	{
		if (VirtualView is null)
			return;

		if (VirtualView.Size.IsZero)
		{
			_contentHost.Width = double.NaN;
			_contentHost.Height = double.NaN;
		}
		else
		{
			_contentHost.Width = VirtualView.Size.Width;
			_contentHost.Height = VirtualView.Size.Height;
		}
	}

	void UpdateDismissBehavior()
	{
		if (PlatformView is null || VirtualView is null)
			return;

		var allowLightDismiss = VirtualView.CanBeDismissedByTappingOutsideOfPopup;
		PlatformView.IsLightDismissEnabled = allowLightDismiss;
		PlatformView.OverlayDismissEventPassThrough = false;
	}

	void UpdatePlacement()
	{
		if (PlatformView is null)
			return;

		var target = ResolveAnchorControl();
		if (target is null)
		{
			target = ResolveRootControl();
			PlatformView.Placement = PlacementMode.Center;
			PlatformView.PlacementAnchor = AvaloniaPopupAnchor.None;
		}
		else
		{
			PlatformView.Placement = PlacementMode.Bottom;
			PlatformView.PlacementAnchor = AvaloniaPopupAnchor.Bottom;
		}

		PlatformView.PlacementTarget = target;
		PlatformView.HorizontalOffset = 0;
		PlatformView.VerticalOffset = 0;
	}

	void OpenPopup()
	{
		if (PlatformView is null)
			return;

		UpdatePlacement();
		PlatformView.IsOpen = true;
		VirtualView?.OnOpened();
	}

	void ClosePopup(object? result)
	{
		if (PlatformView is null || VirtualView is null)
			return;

		try
		{
			_isClosingInternally = true;
			PlatformView.IsOpen = false;
		}
		finally
		{
			_isClosingInternally = false;
		}

		VirtualView.OnClosed(result);
		VirtualView.HandlerCompleteTCS.TrySetResult();
	}

	void OnPlatformOpened(object? sender, EventArgs e) =>
		UpdatePlacement();

	void OnPlatformClosed(object? sender, EventArgs e)
	{
		if (VirtualView is null || _isClosingInternally)
			return;

		if (VirtualView.CanBeDismissedByTappingOutsideOfPopup)
		{
			VirtualView.Handler?.Invoke(nameof(IPopup.OnDismissedByTappingOutsideOfPopup));
		}
	}

	Control? ResolveAnchorControl()
	{
		if (VirtualView?.Anchor?.Handler?.PlatformView is Control control)
			return control;

		return null;
	}

	Control? ResolveRootControl()
	{
		if (MauiContext is null)
			return null;

		var navigationRoot = MauiContext.Services.GetService<IAvaloniaNavigationRoot>();
		return navigationRoot?.RootView;
	}
}
