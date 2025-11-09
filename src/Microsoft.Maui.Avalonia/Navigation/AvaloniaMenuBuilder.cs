using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Accessibility;
using Microsoft.Maui.Avalonia.Handlers;
using Microsoft.Maui.Controls;
using AvaloniaMenuItem = Avalonia.Controls.MenuItem;
using AvaloniaSeparator = Avalonia.Controls.Separator;
using MauiMenuItem = Microsoft.Maui.Controls.MenuItem;

namespace Microsoft.Maui.Avalonia.Navigation;

internal static class AvaloniaMenuBuilder
{
	public static Control? BuildMenu(IMenuBar? menuBar, IMauiContext context)
	{
		_ = context ?? throw new ArgumentNullException(nameof(context));
		if (menuBar is null || menuBar.Count == 0)
			return null;

		var menu = new Menu
		{
			HorizontalAlignment = AvaloniaHorizontalAlignment.Stretch,
			VerticalAlignment = AvaloniaVerticalAlignment.Stretch
		};

		foreach (var item in menuBar)
			menu.Items.Add(CreateMenuBarItem(item, context));

		return menu;
	}

	public static ContextMenu? BuildContextMenu(IFlyout? flyout, IMauiContext context)
	{
		_ = context ?? throw new ArgumentNullException(nameof(context));
		if (flyout is not IMenuFlyout menuFlyout || menuFlyout.Count == 0)
			return null;

		var contextMenu = new ContextMenu();

		foreach (var element in menuFlyout)
			contextMenu.Items.Add(CreateMenuElement(element, context));

		return contextMenu;
	}

	static AvaloniaMenuItem CreateMenuBarItem(IMenuBarItem item, IMauiContext context)
	{
		var menuItem = new AvaloniaMenuItem
		{
			Header = item.Text,
			IsEnabled = item.IsEnabled
		};

		ApplyMenuBarSemantics(menuItem, item);

		foreach (var child in item)
		{
			menuItem.Items.Add(CreateMenuElement(child, context));
		}

		return menuItem;
	}

	static Control CreateMenuElement(IMenuElement element, IMauiContext context)
	{
		if (element is IMenuFlyoutSeparator)
			return new AvaloniaSeparator();

		var menuItem = new AvaloniaMenuItem
		{
			Header = element.Text,
			IsEnabled = element.IsEnabled
		};

		if (element is IMenuFlyoutItem flyoutItem)
		{
			var hotKey = KeyboardAcceleratorMapper.FromAccelerators(flyoutItem.KeyboardAccelerators);
			if (hotKey is not null)
			{
				menuItem.HotKey = hotKey;
				menuItem.InputGesture = hotKey;
			}
		}

		if (element is IMenuFlyoutSubItem subItem)
		{
			foreach (var child in subItem)
				menuItem.Items.Add(CreateMenuElement(child, context));
		}
		else
		{
			menuItem.Click += (_, __) => element.Clicked();
		}

		if (element is MauiMenuItem controlsMenuItem && controlsMenuItem.IsDestructive)
			menuItem.Foreground = Brushes.OrangeRed;

		ApplyMenuSemantics(menuItem, element);
		TrySetIconAsync(menuItem, element, context);

		return menuItem;
	}

	static void TrySetIconAsync(AvaloniaMenuItem menuItem, IMenuElement element, IMauiContext context)
	{
		if (element is not IImageSourcePart imageSourcePart)
			return;

		var source = imageSourcePart.Source;
		if (source is null)
		{
			menuItem.Icon = null;
			return;
		}

		_ = SetIconAsync(menuItem, source, context, CancellationToken.None);
	}

	static async Task SetIconAsync(AvaloniaMenuItem menuItem, IImageSource source, IMauiContext context, CancellationToken token)
	{
		try
		{
			var bitmap = await AvaloniaImageSourceLoader.LoadAsync(source, context.Services, token).ConfigureAwait(false);
			if (bitmap is null)
				return;

			await AvaloniaUiDispatcher.UIThread.InvokeAsync(() =>
			{
				menuItem.Icon = new global::Avalonia.Controls.Image
				{
					Source = bitmap,
					Width = 16,
					Height = 16,
					Stretch = global::Avalonia.Media.Stretch.Uniform
				};
			});
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"[AvaloniaMenuBuilder] Failed to load menu icon: {ex}");
		}
	}

	static void ApplyMenuSemantics(Control target, IMenuElement source)
	{
		var bindable = source as BindableObject;
		AvaloniaSemanticNode.Apply(target, bindable, source.Text, source.Text);
	}

	static void ApplyMenuBarSemantics(Control target, IMenuBarItem source)
	{
		var bindable = source as BindableObject;
		AvaloniaSemanticNode.Apply(target, bindable, source.Text, source.Text);
	}

}
