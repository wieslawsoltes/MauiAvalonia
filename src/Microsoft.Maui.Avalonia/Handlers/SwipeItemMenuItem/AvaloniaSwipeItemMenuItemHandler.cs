using System;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Fonts;
using Microsoft.Maui.Avalonia.Graphics;
using Microsoft.Maui.Avalonia.Platform;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using AvaloniaButtonControl = global::Avalonia.Controls.Button;

namespace Microsoft.Maui.Avalonia.Handlers;

public sealed class AvaloniaSwipeItemMenuItemHandler : ElementHandler<ISwipeItemMenuItem, AvaloniaButtonControl>, ISwipeItemMenuItemHandler
{
	public static readonly IPropertyMapper<ISwipeItemMenuItem, AvaloniaSwipeItemMenuItemHandler> Mapper =
		new PropertyMapper<ISwipeItemMenuItem, AvaloniaSwipeItemMenuItemHandler>(ElementMapper)
		{
			[nameof(IText.Text)] = MapText,
			[nameof(ITextStyle.Font)] = MapFont,
			[nameof(ITextStyle.CharacterSpacing)] = MapCharacterSpacing,
			[nameof(ITextStyle.TextColor)] = MapTextColor,
			[nameof(IMenuElement.IsEnabled)] = MapIsEnabled,
			[nameof(ISwipeItemMenuItem.Background)] = MapBackground,
			[nameof(ISwipeItemMenuItem.Visibility)] = MapVisibility
		};

	ImageSourcePartLoader? _sourceLoader;

	public event EventHandler? Invoked;

	public AvaloniaSwipeItemMenuItemHandler()
		: base(Mapper)
	{
	}

	protected override AvaloniaButtonControl CreatePlatformElement() =>
		new()
		{
			HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
			VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
			Margin = new global::Avalonia.Thickness(4),
			MinWidth = 48,
			MinHeight = 48
		};

	protected override void ConnectHandler(AvaloniaButtonControl platformView)
	{
		base.ConnectHandler(platformView);
		platformView.Click += OnClick;
	}

	protected override void DisconnectHandler(AvaloniaButtonControl platformView)
	{
		platformView.Click -= OnClick;
		base.DisconnectHandler(platformView);
	}

	static void MapText(AvaloniaSwipeItemMenuItemHandler handler, ISwipeItemMenuItem menuItem)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.Content = menuItem.Text ?? string.Empty;
	}

	static void MapFont(AvaloniaSwipeItemMenuItemHandler handler, ISwipeItemMenuItem menuItem)
	{
		if (handler.PlatformView is null)
			return;

		var fontManager = handler.MauiContext?.Services.GetService<IAvaloniaFontManager>();
		if (fontManager is null)
			return;

		handler.PlatformView.UpdateFont(menuItem, fontManager);
	}

	static void MapCharacterSpacing(AvaloniaSwipeItemMenuItemHandler handler, ISwipeItemMenuItem menuItem)
	{
		if (handler.PlatformView is null)
			return;

		global::Avalonia.Controls.TextBlock.SetLetterSpacing(handler.PlatformView, menuItem.CharacterSpacing.ToAvaloniaLetterSpacing());
	}

	static void MapTextColor(AvaloniaSwipeItemMenuItemHandler handler, ISwipeItemMenuItem menuItem)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.UpdateForegroundColor(menuItem);
	}

	static void MapIsEnabled(AvaloniaSwipeItemMenuItemHandler handler, ISwipeItemMenuItem menuItem)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.IsEnabled = menuItem.IsEnabled;
	}

	static void MapBackground(AvaloniaSwipeItemMenuItemHandler handler, ISwipeItemMenuItem menuItem)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.Background = menuItem.Background?.ToAvaloniaBrush();
	}

	static void MapVisibility(AvaloniaSwipeItemMenuItemHandler handler, ISwipeItemMenuItem menuItem)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.IsVisible = menuItem.Visibility == Visibility.Visible;
	}

	void OnClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
	{
		VirtualView?.OnInvoked();
		Invoked?.Invoke(this, EventArgs.Empty);
	}

	public ImageSourcePartLoader? SourceLoader =>
		_sourceLoader ??= new ImageSourcePartLoader(new SwipeItemImageSourceSetter(this));

	sealed class SwipeItemImageSourceSetter : IImageSourcePartSetter
	{
		readonly AvaloniaSwipeItemMenuItemHandler _handler;

		public SwipeItemImageSourceSetter(AvaloniaSwipeItemMenuItemHandler handler) =>
			_handler = handler;

		public IElementHandler? Handler => _handler;

		public IImageSourcePart? ImageSourcePart => _handler.VirtualView as IImageSourcePart;

		public void SetImageSource(object? platformImage)
		{
			// Swipe item icons are not yet supported on the Avalonia backend.
		}
	}
}
