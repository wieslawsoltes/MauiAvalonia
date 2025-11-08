using System;
using Avalonia.Controls;
using CommunityToolkit.Maui.Core;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Internal;
using Microsoft.Maui.Handlers;
using ToolkitExpandDirection = CommunityToolkit.Maui.Core.ExpandDirection;

namespace Microsoft.Maui.Avalonia.Handlers;

public sealed class AvaloniaExpanderHandler : AvaloniaViewHandler<IExpander, Expander>
{
	public static readonly IPropertyMapper<IExpander, AvaloniaExpanderHandler> Mapper =
		new PropertyMapper<IExpander, AvaloniaExpanderHandler>(AvaloniaContentViewHandler.Mapper)
		{
			[nameof(IExpander.Content)] = MapContent,
			[nameof(IExpander.Header)] = MapHeader,
			[nameof(IExpander.IsExpanded)] = MapIsExpanded,
			[nameof(IExpander.Direction)] = MapDirection
		};

	bool _isUpdating;

	public AvaloniaExpanderHandler()
		: base(Mapper)
	{
	}

	protected override Expander CreatePlatformView() =>
		new()
		{
			HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch
		};

	protected override void ConnectHandler(Expander platformView)
	{
		base.ConnectHandler(platformView);
		platformView.Expanded += OnExpandedChanged;
		platformView.Collapsed += OnExpandedChanged;
	}

	protected override void DisconnectHandler(Expander platformView)
	{
		platformView.Expanded -= OnExpandedChanged;
		platformView.Collapsed -= OnExpandedChanged;
		base.DisconnectHandler(platformView);
	}

	static void MapContent(AvaloniaExpanderHandler handler, IExpander expander)
	{
		if (handler.MauiContext is null)
		{
			handler.PlatformView.Content = null;
			return;
		}

		var content = (expander as IContentView)?.PresentedContent ?? expander.Content;
		handler.PlatformView.Content = (content as IView)?.ToAvaloniaControl(handler.MauiContext);
	}

	static void MapHeader(AvaloniaExpanderHandler handler, IExpander expander)
	{
		if (handler.MauiContext is null)
		{
			handler.PlatformView.Header = null;
			return;
		}

		handler.PlatformView.Header = expander.Header?.ToAvaloniaControl(handler.MauiContext);
	}

	static void MapIsExpanded(AvaloniaExpanderHandler handler, IExpander expander)
	{
		if (handler.PlatformView is null)
			return;

		try
		{
			handler._isUpdating = true;
			handler.PlatformView.IsExpanded = expander.IsExpanded;
		}
		finally
		{
			handler._isUpdating = false;
		}
	}

	static void MapDirection(AvaloniaExpanderHandler handler, IExpander expander)
	{
		if (handler.PlatformView is null)
			return;

			handler.PlatformView.ExpandDirection = expander.Direction switch
			{
				ToolkitExpandDirection.Up => global::Avalonia.Controls.ExpandDirection.Up,
				_ => global::Avalonia.Controls.ExpandDirection.Down
			};
	}

	void OnExpandedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
	{
		if (_isUpdating || PlatformView is null || VirtualView is null)
			return;

		try
		{
			_isUpdating = true;
			VirtualView.IsExpanded = PlatformView.IsExpanded;
		}
		finally
		{
			_isUpdating = false;
		}
	}
}
