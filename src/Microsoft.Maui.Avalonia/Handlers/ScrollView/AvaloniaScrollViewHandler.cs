using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Internal;
using Microsoft.Maui.Avalonia.Platform;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using AvaloniaBorder = Avalonia.Controls.Border;
using AvaloniaScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility;

namespace Microsoft.Maui.Avalonia.Handlers;

public class AvaloniaScrollViewHandler : AvaloniaViewHandler<IScrollView, ScrollViewer>, IScrollViewHandler
{
	static readonly TimeSpan ScrollFinishedDelay = TimeSpan.FromMilliseconds(120);

	AvaloniaBorder? _contentHost;
	IDisposable? _scrollFinishedDisposable;

	public static IPropertyMapper<IScrollView, IScrollViewHandler> Mapper =
		new PropertyMapper<IScrollView, IScrollViewHandler>(ViewHandler.ViewMapper)
		{
			[nameof(IScrollView.Content)] = MapContent,
			[nameof(IScrollView.HorizontalScrollBarVisibility)] = MapHorizontalScrollBarVisibility,
			[nameof(IScrollView.VerticalScrollBarVisibility)] = MapVerticalScrollBarVisibility,
			[nameof(IScrollView.Orientation)] = MapOrientation,
			[nameof(IPaddingElement.Padding)] = MapPadding
		};

	public static CommandMapper<IScrollView, IScrollViewHandler> CommandMapper =
		new(ViewCommandMapper)
		{
			[nameof(IScrollView.RequestScrollTo)] = MapRequestScrollTo
		};

	public AvaloniaScrollViewHandler()
		: base(Mapper, CommandMapper)
	{
	}

	protected override ScrollViewer CreatePlatformView() =>
		new()
		{
			HorizontalScrollBarVisibility = AvaloniaScrollBarVisibility.Auto,
			VerticalScrollBarVisibility = AvaloniaScrollBarVisibility.Auto
		};

	protected override void ConnectHandler(ScrollViewer platformView)
	{
		base.ConnectHandler(platformView);
		platformView.ScrollChanged += OnScrollChanged;
	}

	protected override void DisconnectHandler(ScrollViewer platformView)
	{
		platformView.ScrollChanged -= OnScrollChanged;
		_scrollFinishedDisposable?.Dispose();
		_scrollFinishedDisposable = null;
		base.DisconnectHandler(platformView);
	}

	static void MapContent(IScrollViewHandler handler, IScrollView scrollView) =>
		(handler as AvaloniaScrollViewHandler)?.UpdateContent();

	static void MapHorizontalScrollBarVisibility(IScrollViewHandler handler, IScrollView scrollView)
	{
		if (handler.PlatformView is not ScrollViewer scrollViewer)
			return;

		scrollViewer.HorizontalScrollBarVisibility = scrollView.HorizontalScrollBarVisibility.ToAvalonia();
	}

	static void MapVerticalScrollBarVisibility(IScrollViewHandler handler, IScrollView scrollView)
	{
		if (handler.PlatformView is not ScrollViewer scrollViewer)
			return;

		scrollViewer.VerticalScrollBarVisibility = scrollView.VerticalScrollBarVisibility.ToAvalonia();
	}

	static void MapOrientation(IScrollViewHandler handler, IScrollView scrollView)
	{
		if (handler.PlatformView is not ScrollViewer platformView)
			return;

		switch (scrollView.Orientation)
		{
			case ScrollOrientation.Horizontal:
				platformView.HorizontalScrollBarVisibility = scrollView.HorizontalScrollBarVisibility.ToAvalonia();
				platformView.VerticalScrollBarVisibility = AvaloniaScrollBarVisibility.Disabled;
				break;
			case ScrollOrientation.Vertical:
				platformView.HorizontalScrollBarVisibility = AvaloniaScrollBarVisibility.Disabled;
				platformView.VerticalScrollBarVisibility = scrollView.VerticalScrollBarVisibility.ToAvalonia();
				break;
			default:
				platformView.HorizontalScrollBarVisibility = scrollView.HorizontalScrollBarVisibility.ToAvalonia();
				platformView.VerticalScrollBarVisibility = scrollView.VerticalScrollBarVisibility.ToAvalonia();
				break;
		}
	}

	static void MapPadding(IScrollViewHandler handler, IScrollView scrollView) =>
		(handler as AvaloniaScrollViewHandler)?.UpdatePadding();

	static void MapRequestScrollTo(IScrollViewHandler handler, IScrollView scrollView, object? args)
	{
		if (handler is not AvaloniaScrollViewHandler avaloniaHandler || avaloniaHandler.PlatformView is null)
			return;

		if (args is not ScrollToRequest request)
			return;

		var offset = new Vector(request.HorizontalOffset, request.VerticalOffset);
		if (request.Instant)
		{
			avaloniaHandler.PlatformView.Offset = offset;
		}
		else
		{
			avaloniaHandler.PlatformView.Offset = offset;
		}
	}

	void UpdateContent()
	{
		if (PlatformView is null)
			return;

		if (MauiContext is null || VirtualView?.PresentedContent is not IView content)
		{
			PlatformView.Content = null;
			return;
		}

		var control = content.ToAvaloniaControl(MauiContext);
		if (control is null)
		{
			PlatformView.Content = null;
			return;
		}

		_contentHost ??= new AvaloniaBorder
		{
			HorizontalAlignment = AvaloniaHorizontalAlignment.Stretch,
			VerticalAlignment = AvaloniaVerticalAlignment.Stretch
		};

		_contentHost.Padding = Microsoft.Maui.Avalonia.Platform.ThicknessExtensions.ToAvalonia(VirtualView.Padding);
		_contentHost.Child = control;

		PlatformView.Content = _contentHost;
	}

	void UpdatePadding()
	{
		if (_contentHost is null || VirtualView is null)
			return;

		_contentHost.Padding = Microsoft.Maui.Avalonia.Platform.ThicknessExtensions.ToAvalonia(VirtualView.Padding);
	}

	void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
	{
		if (VirtualView is null || PlatformView is null)
			return;

		VirtualView.HorizontalOffset = PlatformView.Offset.X;
		VirtualView.VerticalOffset = PlatformView.Offset.Y;

		_scrollFinishedDisposable?.Dispose();
		_scrollFinishedDisposable = DispatcherTimer.RunOnce(() =>
		{
			VirtualView?.ScrollFinished();
			_scrollFinishedDisposable = null;
		}, ScrollFinishedDelay);
	}
}

static class ScrollBarVisibilityExtensions
{
	public static AvaloniaScrollBarVisibility ToAvalonia(this Microsoft.Maui.ScrollBarVisibility visibility) =>
		visibility switch
		{
			Microsoft.Maui.ScrollBarVisibility.Always => AvaloniaScrollBarVisibility.Visible,
			Microsoft.Maui.ScrollBarVisibility.Never => AvaloniaScrollBarVisibility.Disabled,
			Microsoft.Maui.ScrollBarVisibility.Default => AvaloniaScrollBarVisibility.Auto,
			_ => AvaloniaScrollBarVisibility.Auto
		};
}
