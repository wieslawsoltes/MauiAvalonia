using System;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Internal;
using Microsoft.Maui.Avalonia.Platform;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using MauiControls = Microsoft.Maui.Controls;
using AvaloniaBorder = Avalonia.Controls.Border;
using AvaloniaScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility;

namespace Microsoft.Maui.Avalonia.Handlers;

public class AvaloniaScrollViewHandler : AvaloniaViewHandler<IScrollView, ScrollViewer>, IScrollViewHandler
{
	static readonly TimeSpan ScrollFinishedDelay = TimeSpan.FromMilliseconds(120);
	static readonly TimeSpan ScrollAnimationDuration = TimeSpan.FromMilliseconds(250);

	AvaloniaBorder? _contentHost;
	IDisposable? _scrollFinishedDisposable;
	DispatcherTimer? _scrollAnimationTimer;
	CancellationTokenSource? _scrollAnimationCts;

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
		CancelScrollAnimation();
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
		if (handler is not AvaloniaScrollViewHandler avaloniaHandler)
			return;

		if (!avaloniaHandler.TryResolveScrollRequest(scrollView, args, out var offset, out var instant))
			return;

		avaloniaHandler.RequestScroll(offset, instant);
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

	void RequestScroll(Vector offset, bool instant)
	{
		if (PlatformView is null)
			return;

		var target = ClampOffset(offset);

		if (instant)
		{
			CancelScrollAnimation();
			PlatformView.Offset = target;
			return;
		}

		_ = AnimateScrollAsync(target);
	}

	Vector ClampOffset(Vector offset)
	{
		if (PlatformView is null)
			return offset;

		var extent = PlatformView.Extent;
		var viewport = PlatformView.Viewport;

		var maxX = Math.Max(0, extent.Width - viewport.Width);
		var maxY = Math.Max(0, extent.Height - viewport.Height);

		var x = double.IsFinite(maxX) ? Math.Clamp(offset.X, 0, maxX) : offset.X;
		var y = double.IsFinite(maxY) ? Math.Clamp(offset.Y, 0, maxY) : offset.Y;

		return new Vector(x, y);
	}

	async Task AnimateScrollAsync(Vector target)
	{
		if (PlatformView is null)
			return;

		CancelScrollAnimation();

		var start = PlatformView.Offset;
		if (start == target)
			return;

		var cts = new CancellationTokenSource();
		_scrollAnimationCts = cts;
		var token = cts.Token;
		var tcs = new TaskCompletionSource();

		await AvaloniaUiDispatcher.UIThread.InvokeAsync(() =>
		{
			if (PlatformView is null)
			{
				tcs.TrySetResult();
				return;
			}

			var stopwatch = Stopwatch.StartNew();
			_scrollAnimationTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(16)
			};

			_scrollAnimationTimer.Tick += (_, _) =>
			{
				if (token.IsCancellationRequested || PlatformView is null)
				{
					_scrollAnimationTimer?.Stop();
					tcs.TrySetCanceled(token);
					return;
				}

				var progress = Math.Min(1, stopwatch.Elapsed.TotalMilliseconds / ScrollAnimationDuration.TotalMilliseconds);
				var eased = EaseOutCubic(progress);
				var current = new Vector(
					Lerp(start.X, target.X, eased),
					Lerp(start.Y, target.Y, eased));
				PlatformView.Offset = current;

				if (progress >= 1)
				{
					_scrollAnimationTimer?.Stop();
					tcs.TrySetResult();
				}
			};

			_scrollAnimationTimer.Start();
		});

		try
		{
			await tcs.Task.ConfigureAwait(false);
		}
		catch (TaskCanceledException)
		{
			// ignore
		}
		finally
		{
			CancelScrollAnimation();
		}
	}

	bool TryResolveScrollRequest(IScrollView scrollView, object? args, out Vector offset, out bool instant)
	{
		switch (args)
		{
			case ScrollToRequest request:
				offset = new Vector(request.HorizontalOffset, request.VerticalOffset);
				instant = request.Instant;
				return true;
			case MauiControls.ScrollToRequestedEventArgs requested:
				offset = ResolveOffsets(scrollView, requested);
				instant = !requested.ShouldAnimate;
				return true;
			default:
				offset = default;
				instant = true;
				return false;
		}
	}

	Vector ResolveOffsets(IScrollView scrollView, MauiControls.ScrollToRequestedEventArgs request)
	{
		double horizontal = request.ScrollX;
		double vertical = request.ScrollY;

		if (request.Mode == MauiControls.ScrollToMode.Element &&
			request.Element is MauiControls.VisualElement element &&
			scrollView is MauiControls.ScrollView controlsScrollView)
		{
			var position = controlsScrollView.GetScrollPositionForElement(element, request.Position);
			horizontal = position.X;
			vertical = position.Y;
		}

		return new Vector(horizontal, vertical);
	}

	void CancelScrollAnimation()
	{
		if (_scrollAnimationTimer is not null)
		{
			_scrollAnimationTimer.Stop();
			_scrollAnimationTimer = null;
		}

		if (_scrollAnimationCts is not null)
		{
			try
			{
				_scrollAnimationCts.Cancel();
			}
			catch (ObjectDisposedException)
			{
			}
			finally
			{
				_scrollAnimationCts.Dispose();
				_scrollAnimationCts = null;
			}
		}
	}

	static double Lerp(double start, double end, double progress) =>
		start + ((end - start) * progress);

	static double EaseOutCubic(double progress)
	{
		var t = Math.Max(0, Math.Min(1, progress));
		var inv = 1 - t;
		return 1 - (inv * inv * inv);
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
