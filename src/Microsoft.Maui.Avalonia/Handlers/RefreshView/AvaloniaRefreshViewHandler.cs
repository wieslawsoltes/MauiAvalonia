using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.PullToRefresh;
using Avalonia.VisualTree;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;

namespace Microsoft.Maui.Avalonia.Handlers;

public sealed class AvaloniaRefreshViewHandler : AvaloniaViewHandler<IRefreshView, RefreshContainer>
{
	public static readonly IPropertyMapper<IRefreshView, AvaloniaRefreshViewHandler> Mapper =
			new PropertyMapper<IRefreshView, AvaloniaRefreshViewHandler>(ViewHandler.ViewMapper)
			{
				[nameof(IRefreshView.Content)] = MapContent,
				[nameof(IRefreshView.IsRefreshing)] = MapIsRefreshing,
				[nameof(IRefreshView.RefreshColor)] = MapRefreshColor
			};

		RefreshCompletionDeferral? _refreshDeferral;

	public AvaloniaRefreshViewHandler()
		: base(Mapper)
	{
	}

	protected override RefreshContainer CreatePlatformView() =>
		new()
		{
				PullDirection = global::Avalonia.Input.PullDirection.TopToBottom,
				HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
				VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch
			};

	protected override void ConnectHandler(RefreshContainer platformView)
		{
			base.ConnectHandler(platformView);
		platformView.RefreshRequested += OnRefreshRequested;
		platformView.AttachedToVisualTree += OnAttachedToVisualTree;
		platformView.PropertyChanged += OnPlatformPropertyChanged;
		}

	protected override void DisconnectHandler(RefreshContainer platformView)
	{
		base.DisconnectHandler(platformView);
		platformView.RefreshRequested -= OnRefreshRequested;
		platformView.AttachedToVisualTree -= OnAttachedToVisualTree;
		platformView.PropertyChanged -= OnPlatformPropertyChanged;
		platformView.Content = null;
		CompleteRefresh();
		}

	static void MapContent(AvaloniaRefreshViewHandler handler, IRefreshView refreshView) =>
		handler.UpdateContent();

	static void MapIsRefreshing(AvaloniaRefreshViewHandler handler, IRefreshView refreshView) =>
		handler.UpdateIsRefreshing();

static void MapRefreshColor(AvaloniaRefreshViewHandler handler, IRefreshView refreshView) =>
		handler.UpdateRefreshColor();

	void UpdateContent()
	{
		if (PlatformView is null)
			return;

		if (MauiContext is null || VirtualView is null)
		{
			PlatformView.Content = null;
			return;
		}

		IView? content = (VirtualView as IContentView)?.PresentedContent ?? VirtualView.Content;
		PlatformView.Content = content?.ToAvaloniaControl(MauiContext);
	}

	void UpdateIsRefreshing()
	{
		if (PlatformView is null || VirtualView is null)
			return;

		if (!VirtualView.IsRefreshing)
		{
			CompleteRefresh();
			return;
		}

		if (_refreshDeferral is null && PlatformView.IsAttachedToVisualTree())
			PlatformView.RequestRefresh();
	}

	void UpdateRefreshColor()
	{
		if (PlatformView?.Visualizer is null)
			return;

		if (VirtualView?.RefreshColor is Paint paint)
		{
			PlatformView.Visualizer.Foreground = paint.ToAvaloniaBrush();
		}
		else
		{
			PlatformView.Visualizer.Foreground = null;
		}
	}

	void OnRefreshRequested(object? sender, RefreshRequestedEventArgs e)
	{
		CompleteRefresh();

		_refreshDeferral = e.GetDeferral();

		if (!VirtualView.IsRefreshing)
			VirtualView.IsRefreshing = true;
	}

	void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
	{
		UpdateIsRefreshing();
		UpdateRefreshColor();
	}

	void CompleteRefresh()
	{
		_refreshDeferral?.Complete();
		_refreshDeferral = null;
	}


	void OnPlatformPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property == RefreshContainer.VisualizerProperty)
			UpdateRefreshColor();
	}
}
