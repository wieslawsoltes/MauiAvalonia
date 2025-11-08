using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Avalonia.Handlers;

namespace Microsoft.Maui.Avalonia.Internal;

sealed class MauiItemsViewTemplate : IDataTemplate
{
	readonly ItemsView _itemsView;
	readonly IMauiContext _context;

	public MauiItemsViewTemplate(ItemsView itemsView, IMauiContext context)
	{
		_itemsView = itemsView;
		_context = context;
	}

	public bool Match(object? data) => true;

	public Control Build(object? param)
	{
		var view = CreateView(param);
		if (view is null)
			return new ContentControl();

		view.BindingContext = param;
		return new MauiItemsViewItem(view, _context);
	}

	Microsoft.Maui.Controls.View? CreateView(object? item)
	{
		if (_itemsView.ItemTemplate is DataTemplate template)
		{
			var content = template.CreateContent();
			if (content is Microsoft.Maui.Controls.View view)
				return view;

			if (content is ViewCell cell)
				return cell.View;
		}

		return new Microsoft.Maui.Controls.Label
		{
			Text = item?.ToString() ?? string.Empty
		};
	}
}

sealed class MauiItemsViewItem : ContentControl
{
	readonly IView _view;

	public MauiItemsViewItem(IView view, IMauiContext context)
	{
		_view = view;
		Content = view.ToAvaloniaControl(context);
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnDetachedFromVisualTree(e);
		_view.Handler?.DisconnectHandler();
	}
}
