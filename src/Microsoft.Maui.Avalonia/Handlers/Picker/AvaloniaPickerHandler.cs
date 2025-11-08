using System;
using System.Collections.Specialized;
using Avalonia.Controls;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Fonts;
using Microsoft.Maui.Avalonia.Graphics;
using Microsoft.Maui.Avalonia.Platform;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using AvaloniaSelectionChangedEventArgs = global::Avalonia.Controls.SelectionChangedEventArgs;

namespace Microsoft.Maui.Avalonia.Handlers;

public sealed class AvaloniaPickerHandler : AvaloniaViewHandler<IPicker, ComboBox>, IPickerHandler
{
	public static readonly IPropertyMapper<IPicker, AvaloniaPickerHandler> Mapper =
		new PropertyMapper<IPicker, AvaloniaPickerHandler>(ViewHandler.ViewMapper)
		{
			[nameof(IPicker.Items)] = MapItems,
			[nameof(IPicker.SelectedIndex)] = MapSelectedIndex,
			[nameof(IPicker.Title)] = MapTitle,
			[nameof(IPicker.TitleColor)] = MapTitleColor,
			[nameof(ITextStyle.TextColor)] = MapTextColor,
			[nameof(ITextStyle.Font)] = MapFont,
			[nameof(ITextStyle.CharacterSpacing)] = MapCharacterSpacing,
			[nameof(ITextAlignment.HorizontalTextAlignment)] = MapHorizontalTextAlignment,
			[nameof(ITextAlignment.VerticalTextAlignment)] = MapVerticalTextAlignment,
			[nameof(IView.Background)] = MapBackground
		};

	bool _updatingItems;
	bool _updatingSelection;
	INotifyCollectionChanged? _itemsObservable;

	public AvaloniaPickerHandler()
		: base(Mapper)
	{
	}

	protected override ComboBox CreatePlatformView() =>
		new()
		{
			HorizontalAlignment = AvaloniaHorizontalAlignment.Stretch,
			VerticalAlignment = AvaloniaVerticalAlignment.Center,
			PlaceholderText = string.Empty
		};

	protected override void ConnectHandler(ComboBox platformView)
	{
		base.ConnectHandler(platformView);
		platformView.SelectionChanged += OnSelectionChanged;
		SubscribeItems();
	}

	protected override void DisconnectHandler(ComboBox platformView)
	{
		platformView.SelectionChanged -= OnSelectionChanged;
		UnsubscribeItems();
		base.DisconnectHandler(platformView);
	}

	static void MapItems(AvaloniaPickerHandler handler, IPicker picker) =>
		handler.UpdateItems();

	static void MapSelectedIndex(AvaloniaPickerHandler handler, IPicker picker)
	{
		if (handler.PlatformView is null)
			return;

		try
		{
			handler._updatingSelection = true;
			handler.PlatformView.SelectedIndex = picker.SelectedIndex;
		}
		finally
		{
			handler._updatingSelection = false;
		}
	}

	static void MapTitle(AvaloniaPickerHandler handler, IPicker picker)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.PlaceholderText = string.IsNullOrWhiteSpace(picker.Title) ? string.Empty : picker.Title;
	}

	static void MapTitleColor(AvaloniaPickerHandler handler, IPicker picker)
	{
		if (handler.PlatformView is null)
			return;

		if (picker.TitleColor.IsDefault())
			handler.PlatformView.ClearValue(ComboBox.PlaceholderForegroundProperty);
		else
			handler.PlatformView.PlaceholderForeground = picker.TitleColor.ToAvaloniaBrush();
	}

	static void MapTextColor(AvaloniaPickerHandler handler, IPicker picker)
	{
		if (handler.PlatformView is null)
			return;

		if (picker.TextColor.IsDefault())
			handler.PlatformView.ClearValue(ComboBox.ForegroundProperty);
		else
			handler.PlatformView.Foreground = picker.TextColor.ToAvaloniaBrush();
	}

	static void MapFont(AvaloniaPickerHandler handler, IPicker picker)
	{
		if (handler.PlatformView is null)
			return;

		var fontManager = handler.GetRequiredService<IAvaloniaFontManager>();
		handler.PlatformView.UpdateFont(picker, fontManager);
	}

	static void MapCharacterSpacing(AvaloniaPickerHandler handler, IPicker picker)
	{
		if (handler.PlatformView is null)
			return;

		TextBlock.SetLetterSpacing(handler.PlatformView, picker.CharacterSpacing.ToAvaloniaLetterSpacing());
	}

	static void MapHorizontalTextAlignment(AvaloniaPickerHandler handler, IPicker picker)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.HorizontalContentAlignment = picker.HorizontalTextAlignment switch
		{
			TextAlignment.Center => AvaloniaHorizontalAlignment.Center,
			TextAlignment.End => AvaloniaHorizontalAlignment.Right,
			_ => AvaloniaHorizontalAlignment.Left
		};
	}

	static void MapVerticalTextAlignment(AvaloniaPickerHandler handler, IPicker picker)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.VerticalContentAlignment = picker.VerticalTextAlignment switch
		{
			TextAlignment.Center => AvaloniaVerticalAlignment.Center,
			TextAlignment.End => AvaloniaVerticalAlignment.Bottom,
			_ => AvaloniaVerticalAlignment.Top
		};
	}

	static void MapBackground(AvaloniaPickerHandler handler, IPicker picker)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.Background = picker.Background?.ToAvaloniaBrush();
	}

	void SubscribeItems()
	{
		if (VirtualView?.Items is not INotifyCollectionChanged observable)
			return;

		if (ReferenceEquals(_itemsObservable, observable))
			return;

		UnsubscribeItems();
		_itemsObservable = observable;
		_itemsObservable.CollectionChanged += OnItemsCollectionChanged;
	}

	void UnsubscribeItems()
	{
		if (_itemsObservable is not null)
			_itemsObservable.CollectionChanged -= OnItemsCollectionChanged;

		_itemsObservable = null;
	}

	void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateItems();

	void UpdateItems()
	{
		if (PlatformView is null || VirtualView is null)
			return;

		SubscribeItems();

		try
		{
			_updatingItems = true;
			PlatformView.ItemsSource = new ItemDelegateList<string>(VirtualView);
		}
		finally
		{
			_updatingItems = false;
		}

		MapSelectedIndex(this, VirtualView);
	}

	void OnSelectionChanged(object? sender, AvaloniaSelectionChangedEventArgs e)
	{
		if (VirtualView is null || PlatformView is null || _updatingItems || _updatingSelection)
			return;

		VirtualView.SelectedIndex = PlatformView.SelectedIndex;
	}

}
