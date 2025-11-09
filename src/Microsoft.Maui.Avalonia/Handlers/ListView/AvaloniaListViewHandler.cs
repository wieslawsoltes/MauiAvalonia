using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.Maui;
using AvaloniaGridLength = Avalonia.Controls.GridLength;
using AvaloniaRowDefinition = Avalonia.Controls.RowDefinition;
using AvaloniaSelectionMode = Avalonia.Controls.SelectionMode;

namespace Microsoft.Maui.Avalonia.Handlers;

public class AvaloniaListViewHandler : AvaloniaViewHandler<ListView, AvaloniaGrid>
{
	static readonly PropertyMapper<ListView, AvaloniaListViewHandler> Mapper = new(ViewMapper)
	{
		[nameof(ListView.ItemsSource)] = MapItems,
		[nameof(ListView.ItemTemplate)] = MapItems,
		[nameof(ListView.Header)] = MapItems,
		[nameof(ListView.Footer)] = MapItems,
		[nameof(ListView.GroupHeaderTemplate)] = MapItems,
		[nameof(ListView.SelectedItem)] = MapSelectedItem,
		[nameof(ListView.SelectionMode)] = MapSelectionMode
	};

	static readonly CommandMapper<ListView, AvaloniaListViewHandler> CommandMapper = new(ViewCommandMapper);

	readonly ObservableCollection<ListViewItemDescriptor> _items = new();
	readonly List<INotifyCollectionChanged> _groupSubscriptions = new();

	AvaloniaGrid? _root;
	ContentControl? _headerPresenter;
	ContentControl? _footerPresenter;
	ListBox? _listBox;
	INotifyCollectionChanged? _observable;
	MaterializedView? _headerContent;
	MaterializedView? _footerContent;
	bool _suppressNativeSelection;

	public AvaloniaListViewHandler()
		: base(Mapper, CommandMapper)
	{
	}

	protected override AvaloniaGrid CreatePlatformView()
	{
		_headerPresenter = new ContentControl();
		_footerPresenter = new ContentControl();

		_listBox = new ListBox
		{
			ItemTemplate = new FuncDataTemplate<ListViewItemDescriptor>((item, _) => new ListViewItemPresenter(this)),
			SelectionMode = AvaloniaSelectionMode.Single
		};
		_listBox.SelectionChanged += OnSelectionChanged;
		_listBox.ItemsSource = _items;

		_root = new AvaloniaGrid
		{
			RowDefinitions =
			{
				new AvaloniaRowDefinition { Height = AvaloniaGridLength.Auto },
				new AvaloniaRowDefinition { Height = AvaloniaGridLength.Star },
				new AvaloniaRowDefinition { Height = AvaloniaGridLength.Auto }
			}
		};

		AvaloniaGrid.SetRow(_headerPresenter, 0);
		AvaloniaGrid.SetRow(_listBox, 1);
		AvaloniaGrid.SetRow(_footerPresenter, 2);

		_root.Children.Add(_headerPresenter);
		_root.Children.Add(_listBox);
		_root.Children.Add(_footerPresenter);

		return _root;
	}

	protected override void ConnectHandler(AvaloniaGrid platformView)
	{
		base.ConnectHandler(platformView);
		RefreshItems();
	}

	protected override void DisconnectHandler(AvaloniaGrid platformView)
	{
		StopObserving();
		ReleaseMaterializedView(ref _headerContent);
		ReleaseMaterializedView(ref _footerContent);
		if (_listBox is not null)
			_listBox.SelectionChanged -= OnSelectionChanged;
		base.DisconnectHandler(platformView);
	}

	static void MapItems(AvaloniaListViewHandler handler, ListView view) => handler.RefreshItems();
	static void MapSelectedItem(AvaloniaListViewHandler handler, ListView view) => handler.SyncSelectionFromVirtualView();
	static void MapSelectionMode(AvaloniaListViewHandler handler, ListView view) => handler.ApplySelectionMode();

	void RefreshItems()
	{
		if (VirtualView is null || _listBox is null)
			return;

		StopObserving();
		StartObserving();

		UpdateHeader();
		UpdateFooter();

		_items.Clear();

		if (VirtualView.IsGroupingEnabled)
			PopulateGroupedItems();
		else
			PopulateFlatItems();

		SyncSelectionFromVirtualView();
	}

	void PopulateFlatItems()
	{
		if (VirtualView is null)
			return;

		foreach (var item in EnumerateItems(VirtualView.ItemsSource))
			_items.Add(ListViewItemDescriptor.Item(item));
	}

	void PopulateGroupedItems()
	{
		if (VirtualView?.ItemsSource is null)
			return;

		foreach (var group in EnumerateItems(VirtualView.ItemsSource))
		{
			_items.Add(ListViewItemDescriptor.GroupHeader(group));

			foreach (var child in EnumerateItems(group))
				_items.Add(ListViewItemDescriptor.Item(child));
		}
	}

	void UpdateHeader()
	{
		if (_headerPresenter is null)
			return;

		ReleaseMaterializedView(ref _headerContent);

		if (VirtualView?.Header is null)
		{
			_headerPresenter.Content = null;
			return;
		}

		_headerContent = CreateSupplementaryContent(VirtualView.Header, VirtualView.HeaderTemplate);
		_headerPresenter.Content = _headerContent.HasValue ? _headerContent.Value.Control : null;
	}

	void UpdateFooter()
	{
		if (_footerPresenter is null)
			return;

		ReleaseMaterializedView(ref _footerContent);

		if (VirtualView?.Footer is null)
		{
			_footerPresenter.Content = null;
			return;
		}

		_footerContent = CreateSupplementaryContent(VirtualView.Footer, VirtualView.FooterTemplate);
		_footerPresenter.Content = _footerContent.HasValue ? _footerContent.Value.Control : null;
	}

	void OnSelectionChanged(object? sender, global::Avalonia.Controls.SelectionChangedEventArgs e)
	{
		if (_listBox is null || VirtualView is null || _suppressNativeSelection)
			return;

		if (VirtualView.SelectionMode == ListViewSelectionMode.None)
		{
			SafelyClearSelection();
			return;
		}

		if (_listBox.SelectedItem is not ListViewItemDescriptor descriptor || descriptor.Kind != ListViewItemKind.Item)
			return;

		_suppressNativeSelection = true;
		VirtualView.SelectedItem = descriptor.Content;
		_suppressNativeSelection = false;
	}

	void SyncSelectionFromVirtualView()
	{
		if (_listBox is null || VirtualView is null)
			return;

		if (VirtualView.SelectedItem is null)
		{
			SafelyClearSelection();
			return;
		}

		ListViewItemDescriptor? match = null;
		foreach (var descriptor in _items)
		{
			if (descriptor.Kind == ListViewItemKind.Item && Equals(descriptor.Content, VirtualView.SelectedItem))
			{
				match = descriptor;
				break;
			}
		}

		_suppressNativeSelection = true;
		_listBox.SelectedItem = match.HasValue ? match.Value : null;
		_suppressNativeSelection = false;
	}

	void SafelyClearSelection()
	{
		if (_listBox is null)
			return;

		_suppressNativeSelection = true;
		_listBox.SelectedItem = null;
		_suppressNativeSelection = false;
	}

	void ApplySelectionMode()
	{
		if (_listBox is null || VirtualView is null)
			return;

		_listBox.SelectionMode = AvaloniaSelectionMode.Single;

		if (VirtualView.SelectionMode == ListViewSelectionMode.None)
			SafelyClearSelection();
	}

	void StartObserving()
	{
		if (VirtualView?.ItemsSource is INotifyCollectionChanged observable)
		{
			_observable = observable;
			_observable.CollectionChanged += OnItemsChanged;
		}

		if (VirtualView?.IsGroupingEnabled == true && VirtualView.ItemsSource is IEnumerable groups)
		{
			foreach (var group in groups)
			{
				if (group is INotifyCollectionChanged groupObservable)
				{
					groupObservable.CollectionChanged += OnItemsChanged;
					_groupSubscriptions.Add(groupObservable);
				}
			}
		}
	}

	void StopObserving()
	{
		if (_observable is not null)
			_observable.CollectionChanged -= OnItemsChanged;
		_observable = null;

		foreach (var subscription in _groupSubscriptions)
			subscription.CollectionChanged -= OnItemsChanged;
		_groupSubscriptions.Clear();
	}

	void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshItems();

	MaterializedView? CreateSupplementaryContent(object? context, DataTemplate? template)
	{
		if (context is null)
			return null;

		if (context is View view)
			return MaterializeView(view);

		if (template?.CreateContent() is Element element)
		{
			element.BindingContext = context;

			if (element is ViewCell cell)
				return MaterializeView(cell.View);

			if (element is View templateView)
				return MaterializeView(templateView);
		}

			var textBlock = new TextBlock
			{
				Text = context.ToString() ?? string.Empty,
				Margin = new global::Avalonia.Thickness(8, 12, 8, 4),
				FontWeight = global::Avalonia.Media.FontWeight.SemiBold
			};

		return new MaterializedView(textBlock, null);
	}

	MaterializedView MaterializeDescriptor(ListViewItemDescriptor descriptor)
	{
		if (descriptor.Kind == ListViewItemKind.GroupHeader)
			return CreateSupplementaryContent(descriptor.Content, VirtualView?.GroupHeaderTemplate) ?? new MaterializedView(new TextBlock(), null);

		if (VirtualView?.ItemTemplate?.CreateContent() is Element element)
		{
			element.BindingContext = descriptor.Content;
			if (element is ViewCell cell)
				return MaterializeView(cell.View);

			if (element is View view)
				return MaterializeView(view);
		}

		if (descriptor.Content is View directView)
			return MaterializeView(directView);

		return new MaterializedView(new TextBlock
		{
			Text = descriptor.Content?.ToString() ?? string.Empty,
			Margin = new global::Avalonia.Thickness(4)
		}, null);
	}

	MaterializedView MaterializeView(View? view)
	{
		if (view is null)
			return new MaterializedView(new TextBlock(), null);

		var handler = view.ToHandler(MauiContext);
		var control = handler.PlatformView as Control ?? new TextBlock();
		return new MaterializedView(control, handler);
	}

	void ReleaseMaterializedView(ref MaterializedView? materialized)
	{
		if (materialized is { Handler: { } handler })
			handler.DisconnectHandler();

		materialized = null;
	}

	static IEnumerable EnumerateItems(object? source)
	{
		if (source is IEnumerable enumerable)
			return enumerable;

		return Array.Empty<object>();
	}

	sealed class ListViewItemPresenter : ContentControl
	{
		readonly AvaloniaListViewHandler _owner;
		MaterializedView? _materialized;

		public ListViewItemPresenter(AvaloniaListViewHandler owner)
		{
			_owner = owner;
			DataContextChanged += OnDataContextChanged;
		}

		void OnDataContextChanged(object? sender, EventArgs e)
		{
			if (DataContext is ListViewItemDescriptor descriptor)
			{
				Release();
				var materialized = _owner.MaterializeDescriptor(descriptor);
				_materialized = materialized;
				Content = materialized.Control;
			}
			else
			{
				Release();
			}
		}

		void Release()
		{
			if (_materialized is { Handler: { } handler })
				handler.DisconnectHandler();

			_materialized = null;
			Content = null;
		}

	}

	enum ListViewItemKind
	{
		Item,
		GroupHeader
	}

	readonly record struct MaterializedView(Control Control, IElementHandler? Handler);

	readonly record struct ListViewItemDescriptor(ListViewItemKind Kind, object? Content)
	{
		public static ListViewItemDescriptor Item(object? content) => new(ListViewItemKind.Item, content);
		public static ListViewItemDescriptor GroupHeader(object? content) => new(ListViewItemKind.GroupHeader, content);
	}
}
