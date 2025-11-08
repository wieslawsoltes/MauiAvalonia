using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Microsoft.Maui;
using Microsoft.Maui.Handlers;
using MauiControls = global::Microsoft.Maui.Controls;

namespace Microsoft.Maui.Avalonia.Handlers;

using AvaloniaSelectionChangedEventArgs = global::Avalonia.Controls.SelectionChangedEventArgs;
using AvaloniaSelectionMode = global::Avalonia.Controls.SelectionMode;
using MauiSelectionMode = global::Microsoft.Maui.Controls.SelectionMode;

public class AvaloniaCollectionViewHandler : ViewHandler<MauiControls.CollectionView, ListBox>
{
	public static readonly IPropertyMapper<MauiControls.CollectionView, AvaloniaCollectionViewHandler> Mapper =
		new PropertyMapper<MauiControls.CollectionView, AvaloniaCollectionViewHandler>(ViewHandler.ViewMapper)
		{
			[nameof(MauiControls.ItemsView.ItemsSource)] = MapItemsSource,
			[nameof(MauiControls.ItemsView.ItemTemplate)] = MapItemTemplate,
			[nameof(MauiControls.ItemsView.EmptyView)] = MapSupplementalContent,
			[nameof(MauiControls.ItemsView.EmptyViewTemplate)] = MapSupplementalContent,
			[nameof(MauiControls.StructuredItemsView.Header)] = MapSupplementalContent,
			[nameof(MauiControls.StructuredItemsView.HeaderTemplate)] = MapSupplementalContent,
			[nameof(MauiControls.StructuredItemsView.Footer)] = MapSupplementalContent,
			[nameof(MauiControls.StructuredItemsView.FooterTemplate)] = MapSupplementalContent,
			[nameof(MauiControls.ItemsView.RemainingItemsThreshold)] = MapRemainingItemsThreshold,
			[nameof(MauiControls.SelectableItemsView.SelectionMode)] = MapSelectionMode,
			[nameof(MauiControls.SelectableItemsView.SelectedItem)] = MapSelectedItem,
			[nameof(MauiControls.SelectableItemsView.SelectedItems)] = MapSelectedItems
		};

	public AvaloniaCollectionViewHandler()
		: base(Mapper)
	{
	}

	readonly AvaloniaList<CollectionViewItemViewModel> _visualItems = new();
	readonly List<object?> _dataItems = new();
	INotifyCollectionChanged? _observedItemsSource;
	VirtualizingStackPanel? _itemsPanel;
	bool _suppressSelectionUpdates;

	protected override ListBox CreatePlatformView() =>
		new()
		{
			ItemsPanel = new FuncTemplate<Panel>(() => new VirtualizingStackPanel()),
			ItemsSource = _visualItems
		};

	protected override void ConnectHandler(ListBox platformView)
	{
		base.ConnectHandler(platformView);
		platformView.SelectionChanged += OnSelectionChanged;
		platformView.TemplateApplied += OnTemplateApplied;
		platformView.AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollViewerChanged, RoutingStrategies.Bubble);
	}

	protected override void DisconnectHandler(ListBox platformView)
	{
		platformView.SelectionChanged -= OnSelectionChanged;
		platformView.TemplateApplied -= OnTemplateApplied;
		platformView.RemoveHandler(ScrollViewer.ScrollChangedEvent, OnScrollViewerChanged);
		DetachItemsSourceObserver();
		_visualItems.Clear();
		_dataItems.Clear();
		_itemsPanel = null;
		base.DisconnectHandler(platformView);
	}

	static void MapItemsSource(AvaloniaCollectionViewHandler handler, MauiControls.CollectionView view) =>
		handler.UpdateItemsSource();

	static void MapItemTemplate(AvaloniaCollectionViewHandler handler, MauiControls.CollectionView view)
	{
		handler.UpdateItemTemplate();
		handler.RefreshVisualItems();
	}

	static void MapSupplementalContent(AvaloniaCollectionViewHandler handler, MauiControls.CollectionView view) =>
		handler.RefreshVisualItems();

	static void MapSelectionMode(AvaloniaCollectionViewHandler handler, MauiControls.CollectionView view) =>
		handler.UpdateSelectionMode();

	static void MapSelectedItem(AvaloniaCollectionViewHandler handler, MauiControls.CollectionView view) =>
		handler.UpdateSelectedItem();

	static void MapSelectedItems(AvaloniaCollectionViewHandler handler, MauiControls.CollectionView view) =>
		handler.UpdateSelectedItem();

	static void MapRemainingItemsThreshold(AvaloniaCollectionViewHandler handler, MauiControls.CollectionView view) =>
		handler.TrySendRemainingItemsThreshold();

	void UpdateItemsSource()
	{
		if (VirtualView is null)
			return;

		ObserveItemsSource(VirtualView.ItemsSource);

		if (MauiContext is not null)
			UpdateItemTemplate();

		RefreshVisualItems();
		UpdateSelectionMode();
		UpdateSelectedItem();
	}

	void UpdateItemTemplate()
	{
		if (PlatformView is null || VirtualView is null || MauiContext is null)
			return;

		PlatformView.DataTemplates.Clear();
		PlatformView.DataTemplates.Add(new CollectionViewItemTemplate(VirtualView, MauiContext));
	}

	void UpdateSelectionMode()
	{
		if (PlatformView is null || VirtualView is null)
			return;

		PlatformView.SelectionMode = VirtualView.SelectionMode switch
		{
			MauiSelectionMode.Multiple => AvaloniaSelectionMode.Multiple,
			MauiSelectionMode.Single => AvaloniaSelectionMode.Single,
			_ => AvaloniaSelectionMode.Single
		};
	}

	void UpdateSelectedItem()
	{
		if (PlatformView is null || VirtualView is null)
			return;

		try
		{
			_suppressSelectionUpdates = true;

			if (VirtualView.SelectionMode == MauiSelectionMode.Multiple)
			{
				var selectedItems = PlatformView.SelectedItems;
				selectedItems?.Clear();

				if (VirtualView.SelectedItems is { Count: > 0 } multiSelection && selectedItems is not null)
				{
					foreach (var item in multiSelection)
					{
						var match = FindVisualItem(item);
						if (match is not null)
							selectedItems.Add(match);
					}
				}
			}
			else
			{
				PlatformView.SelectedItem = FindVisualItem(VirtualView.SelectedItem);
			}
		}
		finally
		{
			_suppressSelectionUpdates = false;
		}
	}

	void RefreshVisualItems()
	{
		if (VirtualView is null)
		{
			_visualItems.Clear();
			_dataItems.Clear();
			return;
		}

		BuildDataItems(VirtualView.ItemsSource);
		_visualItems.Clear();

		if (VirtualView.Header is not null || VirtualView.HeaderTemplate is not null)
			_visualItems.Add(CollectionViewItemViewModel.CreateHeader(VirtualView.Header));

		if (_dataItems.Count > 0)
		{
			for (var i = 0; i < _dataItems.Count; i++)
			{
				_visualItems.Add(CollectionViewItemViewModel.CreateItem(_dataItems[i], i));
			}
		}
		else if (VirtualView.EmptyView is not null || VirtualView.EmptyViewTemplate is not null)
		{
			_visualItems.Add(CollectionViewItemViewModel.CreateEmpty(VirtualView.EmptyView));
		}

		if (VirtualView.Footer is not null || VirtualView.FooterTemplate is not null)
			_visualItems.Add(CollectionViewItemViewModel.CreateFooter(VirtualView.Footer));

		_itemsPanel ??= PlatformView?.ItemsPanelRoot as VirtualizingStackPanel;
		TrySendRemainingItemsThreshold();
	}

	void BuildDataItems(IEnumerable? itemsSource)
	{
		_dataItems.Clear();
		if (itemsSource is null)
			return;

		foreach (var item in itemsSource)
			_dataItems.Add(item);
	}

	void ObserveItemsSource(IEnumerable? itemsSource)
	{
		DetachItemsSourceObserver();

		if (itemsSource is INotifyCollectionChanged collectionChanged)
		{
			_observedItemsSource = collectionChanged;
			_observedItemsSource.CollectionChanged += OnItemsSourceCollectionChanged;
		}
	}

	void DetachItemsSourceObserver()
	{
		if (_observedItemsSource is not null)
		{
			_observedItemsSource.CollectionChanged -= OnItemsSourceCollectionChanged;
			_observedItemsSource = null;
		}
	}

	void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (PlatformView is null)
			return;

		if (AvaloniaUiDispatcher.UIThread.CheckAccess())
		{
			RefreshVisualItems();
			UpdateSelectedItem();
		}
		else
		{
			AvaloniaUiDispatcher.UIThread.Post(() =>
			{
				RefreshVisualItems();
				UpdateSelectedItem();
			});
		}
	}

	CollectionViewItemViewModel? FindVisualItem(object? item)
	{
		if (item is null)
			return null;

		return _visualItems.FirstOrDefault(x => x.Kind == CollectionViewItemKind.Item && ReferenceEquals(x.Data, item));
	}

	void OnSelectionChanged(object? sender, AvaloniaSelectionChangedEventArgs e)
	{
		if (VirtualView is null || PlatformView is null || _suppressSelectionUpdates)
			return;

		try
		{
			_suppressSelectionUpdates = true;

			if (VirtualView.SelectionMode == MauiSelectionMode.None)
			{
				PlatformView.SelectedItem = null;
				return;
			}

			if (VirtualView.SelectionMode == MauiSelectionMode.Multiple)
			{
				var selection = PlatformView.SelectedItems?
					.OfType<CollectionViewItemViewModel>()
					.Where(vm => vm.Kind == CollectionViewItemKind.Item)
					.Select(vm => vm.Data)
					.Where(item => item is not null)
					.Cast<object>()
					.ToList() ?? new List<object>();

				VirtualView.UpdateSelectedItems(selection);
			}
			else
			{
				var selected = PlatformView.SelectedItem as CollectionViewItemViewModel;
				if (selected?.Kind == CollectionViewItemKind.Item)
				{
					VirtualView.SelectedItem = selected.Data;
				}
				else
				{
					VirtualView.SelectedItem = null;
					PlatformView.SelectedItem = null;
				}
			}
		}
		finally
		{
			_suppressSelectionUpdates = false;
		}
	}

	void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
	{
		_itemsPanel = PlatformView?.ItemsPanelRoot as VirtualizingStackPanel;
		TrySendRemainingItemsThreshold();
	}

	void OnScrollViewerChanged(object? sender, ScrollChangedEventArgs e)
	{
		if (VirtualView is null)
			return;

		var scrollViewer = sender as ScrollViewer;
		var first = GetFirstVisibleDataIndex();
		var last = GetLastVisibleDataIndex();

		var args = new MauiControls.ItemsViewScrolledEventArgs
		{
			HorizontalDelta = e.OffsetDelta.X,
			VerticalDelta = e.OffsetDelta.Y,
			HorizontalOffset = scrollViewer?.Offset.X ?? 0,
			VerticalOffset = scrollViewer?.Offset.Y ?? 0,
			FirstVisibleItemIndex = first,
			LastVisibleItemIndex = last,
			CenterItemIndex = CalculateCenterIndex(first, last)
		};

		VirtualView.SendScrolled(args);
		TrySendRemainingItemsThreshold();
	}

	static int CalculateCenterIndex(int first, int last)
	{
		if (first == -1 && last == -1)
			return -1;
		if (first == -1)
			return last;
		if (last == -1)
			return first;
		return first + ((last - first) / 2);
	}

	int GetFirstVisibleDataIndex()
	{
		if (_itemsPanel is null)
			return _dataItems.Count > 0 ? 0 : -1;

		return GetDataIndexFromAdapter(_itemsPanel.FirstRealizedIndex, true);
	}

	int GetLastVisibleDataIndex()
	{
		if (_itemsPanel is null)
			return _dataItems.Count > 0 ? _dataItems.Count - 1 : -1;

		return GetDataIndexFromAdapter(_itemsPanel.LastRealizedIndex, false);
	}

	int GetDataIndexFromAdapter(int adapterIndex, bool forward)
	{
		if (adapterIndex < 0 || _visualItems.Count == 0)
			return -1;

		if (forward)
		{
			for (var i = Math.Max(0, adapterIndex); i < _visualItems.Count; i++)
			{
				var candidate = _visualItems[i];
				if (candidate.Kind == CollectionViewItemKind.Item)
					return candidate.DataIndex;
			}
		}
		else
		{
			for (var i = Math.Min(adapterIndex, _visualItems.Count - 1); i >= 0; i--)
			{
				var candidate = _visualItems[i];
				if (candidate.Kind == CollectionViewItemKind.Item)
					return candidate.DataIndex;
			}
		}

		return -1;
	}

	void TrySendRemainingItemsThreshold()
	{
		if (VirtualView is null)
			return;

		var threshold = VirtualView.RemainingItemsThreshold;
		if (threshold < 0)
			return;

		var itemCount = _dataItems.Count;
		if (itemCount == 0)
		{
			VirtualView.SendRemainingItemsThresholdReached();
			return;
		}

		var lastVisible = GetLastVisibleDataIndex();
		if (lastVisible == -1)
			return;

		if (itemCount - 1 - lastVisible <= threshold)
			VirtualView.SendRemainingItemsThresholdReached();
	}
}
