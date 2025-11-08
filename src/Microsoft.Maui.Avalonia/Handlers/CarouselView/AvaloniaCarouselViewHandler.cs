using System;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Internal;
using Microsoft.Maui.Handlers;
using MauiControls = Microsoft.Maui.Controls;
using AvaloniaSelectionChangedEventArgs = global::Avalonia.Controls.SelectionChangedEventArgs;

namespace Microsoft.Maui.Avalonia.Handlers;

public sealed class AvaloniaCarouselViewHandler : ViewHandler<MauiControls.CarouselView, Carousel>
{
	public static readonly IPropertyMapper<MauiControls.CarouselView, AvaloniaCarouselViewHandler> Mapper =
		new PropertyMapper<MauiControls.CarouselView, AvaloniaCarouselViewHandler>(ViewHandler.ViewMapper)
		{
			[nameof(MauiControls.ItemsView.ItemsSource)] = MapItemsSource,
			[nameof(MauiControls.ItemsView.ItemTemplate)] = MapItemTemplate,
			[nameof(MauiControls.CarouselView.Position)] = MapPosition,
			[nameof(MauiControls.CarouselView.CurrentItem)] = MapCurrentItem,
			[nameof(MauiControls.CarouselView.PeekAreaInsets)] = MapPeekAreaInsets,
			[nameof(MauiControls.CarouselView.IsSwipeEnabled)] = MapIsSwipeEnabled
		};

	bool _suppressSelectionUpdates;

	public AvaloniaCarouselViewHandler()
		: base(Mapper)
	{
	}

	protected override Carousel CreatePlatformView() =>
		new();

	protected override void ConnectHandler(Carousel platformView)
	{
		base.ConnectHandler(platformView);
		platformView.SelectionChanged += OnSelectionChanged;
	}

	protected override void DisconnectHandler(Carousel platformView)
	{
		base.DisconnectHandler(platformView);
		platformView.SelectionChanged -= OnSelectionChanged;
		platformView.ItemsSource = null;
	}

	static void MapItemsSource(AvaloniaCarouselViewHandler handler, MauiControls.CarouselView carouselView) =>
		handler.UpdateItemsSource();

	static void MapItemTemplate(AvaloniaCarouselViewHandler handler, MauiControls.CarouselView carouselView) =>
		handler.UpdateItemTemplate();

	static void MapPosition(AvaloniaCarouselViewHandler handler, MauiControls.CarouselView carouselView) =>
		handler.UpdatePosition();

	static void MapCurrentItem(AvaloniaCarouselViewHandler handler, MauiControls.CarouselView carouselView) =>
		handler.UpdateCurrentItem();

	static void MapPeekAreaInsets(AvaloniaCarouselViewHandler handler, MauiControls.CarouselView carouselView)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.Padding = carouselView.PeekAreaInsets.ToAvalonia();
	}

	static void MapIsSwipeEnabled(AvaloniaCarouselViewHandler handler, MauiControls.CarouselView carouselView)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.IsHitTestVisible = carouselView.IsSwipeEnabled;
	}

	void UpdateItemsSource()
	{
		if (PlatformView is null || VirtualView is null)
			return;

		PlatformView.ItemsSource = VirtualView.ItemsSource ?? Array.Empty<object>();

		if (MauiContext is not null)
		{
			UpdateItemTemplate();
		}

		UpdatePosition();
		UpdateCurrentItem();
	}

	void UpdateItemTemplate()
	{
		if (PlatformView is null || VirtualView is null || MauiContext is null)
			return;

		PlatformView.DataTemplates.Clear();
		PlatformView.DataTemplates.Add(new MauiItemsViewTemplate(VirtualView, MauiContext));
	}

	void UpdatePosition()
	{
		if (PlatformView is null || VirtualView is null)
			return;

		var hasItems = PlatformView.ItemCount > 0;
		var targetIndex = hasItems ? Math.Max(0, Math.Min(VirtualView.Position, PlatformView.ItemCount - 1)) : -1;

		try
		{
			_suppressSelectionUpdates = true;
			PlatformView.SelectedIndex = targetIndex;
		}
		finally
		{
			_suppressSelectionUpdates = false;
		}
	}

	void UpdateCurrentItem()
	{
		if (PlatformView is null || VirtualView is null)
			return;

		if (VirtualView.CurrentItem is null && VirtualView.Position >= 0)
			return;

		try
		{
			_suppressSelectionUpdates = true;
			PlatformView.SelectedItem = VirtualView.CurrentItem;
		}
		finally
		{
			_suppressSelectionUpdates = false;
		}
	}

	void OnSelectionChanged(object? sender, AvaloniaSelectionChangedEventArgs e)
	{
		if (VirtualView is null || PlatformView is null || _suppressSelectionUpdates)
			return;

		try
		{
			_suppressSelectionUpdates = true;

			var index = PlatformView.SelectedIndex;
			if (index >= 0)
				VirtualView.Position = index;

			VirtualView.CurrentItem = PlatformView.SelectedItem;
		}
		finally
		{
			_suppressSelectionUpdates = false;
		}
	}
}
