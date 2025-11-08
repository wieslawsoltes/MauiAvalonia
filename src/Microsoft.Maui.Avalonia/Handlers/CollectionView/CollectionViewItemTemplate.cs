using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Internal;
using MauiControls = global::Microsoft.Maui.Controls;

namespace Microsoft.Maui.Avalonia.Handlers;

enum CollectionViewItemKind
{
	Header,
	Footer,
	Empty,
	Item
}

sealed class CollectionViewItemViewModel
{
	CollectionViewItemViewModel(CollectionViewItemKind kind, object? data, int dataIndex)
	{
		Kind = kind;
		Data = data;
		DataIndex = dataIndex;
	}

	public CollectionViewItemKind Kind { get; }
	public object? Data { get; }
	public int DataIndex { get; }

	public static CollectionViewItemViewModel CreateHeader(object? data) =>
		new(CollectionViewItemKind.Header, data, -1);

	public static CollectionViewItemViewModel CreateFooter(object? data) =>
		new(CollectionViewItemKind.Footer, data, -1);

	public static CollectionViewItemViewModel CreateEmpty(object? data) =>
		new(CollectionViewItemKind.Empty, data, -1);

	public static CollectionViewItemViewModel CreateItem(object? data, int index) =>
		new(CollectionViewItemKind.Item, data, index);
}

sealed class CollectionViewItemTemplate : IDataTemplate
{
	readonly MauiControls.CollectionView _itemsView;
	readonly IMauiContext _context;

	public CollectionViewItemTemplate(MauiControls.CollectionView itemsView, IMauiContext context)
	{
		_itemsView = itemsView;
		_context = context;
	}

	public bool Match(object? data) => data is CollectionViewItemViewModel;

	public Control Build(object? param)
	{
		if (param is not CollectionViewItemViewModel item)
			return new ContentControl();

		var view = CreateView(item);
		if (view is null)
			return new ContentControl();

		return new MauiItemsViewItem(view, _context);
	}

	MauiControls.View? CreateView(CollectionViewItemViewModel item) => item.Kind switch
	{
		CollectionViewItemKind.Header => CreateStructuredView(_itemsView.Header, _itemsView.HeaderTemplate),
		CollectionViewItemKind.Footer => CreateStructuredView(_itemsView.Footer, _itemsView.FooterTemplate),
		CollectionViewItemKind.Empty => CreateStructuredView(_itemsView.EmptyView, _itemsView.EmptyViewTemplate)
			?? CreateDefaultView(_itemsView.EmptyView),
		CollectionViewItemKind.Item => CreateItemView(item.Data),
		_ => null
	};

	MauiControls.View? CreateStructuredView(object? content, MauiControls.DataTemplate? template)
	{
		if (content is MauiControls.View view)
			return view;

		if (template is not null)
			return Inflate(template, content);

		return content is null ? null : CreateDefaultView(content);
	}

	MauiControls.View? CreateItemView(object? data)
	{
		if (_itemsView.ItemTemplate is MauiControls.DataTemplate template)
		{
			var templateView = Inflate(template, data);
			if (templateView is not null)
				return templateView;
		}

		if (data is MauiControls.View view)
			return view;

		return CreateDefaultView(data);
	}

	static MauiControls.View? Inflate(MauiControls.DataTemplate template, object? data)
	{
		var content = template.CreateContent();

		if (content is MauiControls.View view)
		{
			view.BindingContext = data;
			return view;
		}

		if (content is MauiControls.ViewCell cell)
		{
			cell.View.BindingContext = data;
			return cell.View;
		}

		return null;
	}

	static MauiControls.View CreateDefaultView(object? data) =>
		new MauiControls.Label { Text = data?.ToString() ?? string.Empty };
}
