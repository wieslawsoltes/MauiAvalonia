using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Accessibility;
using Microsoft.Maui.Avalonia.Graphics;
using Microsoft.Maui.Avalonia.Navigation;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using MauiControls = Microsoft.Maui.Controls;
using MauiToolbar = Microsoft.Maui.Controls.Toolbar;
using MauiToolbarItem = Microsoft.Maui.Controls.ToolbarItem;
using MauiToolbarItemOrder = Microsoft.Maui.Controls.ToolbarItemOrder;
using ImageSource = Microsoft.Maui.Controls.ImageSource;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBlock = Avalonia.Controls.TextBlock;
using AvaloniaImage = Avalonia.Controls.Image;
using AvaloniaBorder = Avalonia.Controls.Border;
using AvaloniaMenuItem = Avalonia.Controls.MenuItem;
using AvaloniaMenuFlyout = Avalonia.Controls.MenuFlyout;

namespace Microsoft.Maui.Avalonia.Handlers;

internal sealed class AvaloniaToolbar : AvaloniaGrid
{
	const string CheckedClass = "toolbar-button-checked";
	static readonly PropertyInfo? IsCheckedProperty = typeof(MauiToolbarItem).GetProperty("IsChecked");
	static readonly PropertyInfo? KeyboardAcceleratorsProperty = typeof(MauiToolbarItem).GetProperty("KeyboardAccelerators");

	readonly AvaloniaButton _backButton;
	readonly StackPanel _titleStack;
	readonly AvaloniaTextBlock _titleText;
	readonly AvaloniaImage _titleIcon;
	readonly ContentControl _titleViewHost;
	readonly StackPanel _itemsHost;
	readonly AvaloniaBorder _itemsBorder;
	readonly AvaloniaMenuFlyout _overflowFlyout;
	readonly AvaloniaButton _overflowButton;
	readonly Dictionary<MauiToolbarItem, AvaloniaButton> _itemButtons = new();
	readonly Dictionary<MauiToolbarItem, AvaloniaMenuItem> _overflowMenuItems = new();
	readonly Dictionary<MauiToolbarItem, ToolbarItemCommand> _itemCommands = new();
	readonly Dictionary<MauiToolbarItem, PropertyChangedEventHandler> _itemSubscriptions = new();
	readonly Dictionary<MauiToolbarItem, Bitmap?> _iconCache = new();
	readonly Dictionary<MauiToolbarItem, ToolbarItemIconLoader> _iconLoaders = new();
	readonly AvaloniaThickness _buttonMargin = new(4, 0, 0, 0);

	MauiToolbar? _currentToolbar;
	IMauiContext? _context;
	bool _isUpdatingItems;
	bool _rebuildPending;

	public AvaloniaToolbar()
	{
		RowDefinitions = new RowDefinitions("Auto");
		ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
		Height = 44;
		Background = new AvaloniaSolidColorBrush(AvaloniaColor.FromRgb(245, 245, 245));
		VerticalAlignment = AvaloniaVerticalAlignment.Stretch;
		HorizontalAlignment = AvaloniaHorizontalAlignment.Stretch;
		Margin = new AvaloniaThickness(8, 0, 8, 0);

		_backButton = new AvaloniaButton
		{
			Content = "←",
			VerticalAlignment = AvaloniaVerticalAlignment.Center,
			HorizontalAlignment = AvaloniaHorizontalAlignment.Left,
			Margin = new AvaloniaThickness(0, 0, 8, 0),
			IsVisible = false
		};
		_backButton.Click += (_, __) => BackRequested?.Invoke(this, EventArgs.Empty);
		_backButton.SetValue(AvaloniaGrid.ColumnProperty, 0);

		_titleIcon = new AvaloniaImage
		{
			Width = 16,
			Height = 16,
			Margin = new AvaloniaThickness(0, 0, 6, 0),
			IsVisible = false
		};

		_titleText = new AvaloniaTextBlock
		{
			VerticalAlignment = AvaloniaVerticalAlignment.Center,
			FontSize = 16,
			TextTrimming = TextTrimming.CharacterEllipsis
		};

		_titleStack = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = AvaloniaVerticalAlignment.Center,
			Spacing = 2
		};
		_titleStack.Children.Add(_titleIcon);
		_titleStack.Children.Add(_titleText);

		_titleViewHost = new ContentControl
		{
			Content = _titleStack,
			HorizontalAlignment = AvaloniaHorizontalAlignment.Stretch,
			VerticalAlignment = AvaloniaVerticalAlignment.Center
		};
		_titleViewHost.SetValue(AvaloniaGrid.ColumnProperty, 1);

		_itemsHost = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = AvaloniaHorizontalAlignment.Right,
			VerticalAlignment = AvaloniaVerticalAlignment.Center,
			Spacing = 4
		};

		_overflowFlyout = new AvaloniaMenuFlyout();

		_overflowButton = new AvaloniaButton
		{
			Content = "⋮",
			HorizontalAlignment = AvaloniaHorizontalAlignment.Right,
			VerticalAlignment = AvaloniaVerticalAlignment.Center,
			Margin = _buttonMargin,
			MinHeight = 30,
			IsVisible = false
		};
		_overflowButton.Click += (_, __) => global::Avalonia.Controls.Primitives.FlyoutBase.ShowAttachedFlyout(_overflowButton);
		global::Avalonia.Controls.Primitives.FlyoutBase.SetAttachedFlyout(_overflowButton, _overflowFlyout);

		_itemsBorder = new AvaloniaBorder
		{
			Child = _itemsHost,
			HorizontalAlignment = AvaloniaHorizontalAlignment.Right,
			VerticalAlignment = AvaloniaVerticalAlignment.Center
		};
		_itemsBorder.SetValue(AvaloniaGrid.ColumnProperty, 2);

		Children.Add(_backButton);
		Children.Add(_titleViewHost);
		Children.Add(_itemsBorder);
	}

	public event EventHandler? BackRequested;

	public void SetContext(IMauiContext? context) => _context = context;

	public void UpdateTitle(MauiToolbar toolbar)
	{
		_titleText.Text = toolbar.Title ?? string.Empty;
	}

	public void UpdateTitleView(MauiToolbar toolbar)
	{
		if (toolbar.TitleView is IView titleView && _context != null)
		{
			_titleViewHost.Content = titleView.ToAvaloniaControl(_context);
		}
		else
		{
			_titleViewHost.Content = _titleStack;
		}
	}

	public void UpdateTitleIcon(MauiToolbar toolbar)
	{
		if (toolbar.TitleIcon != null)
		{
			LoadImage(toolbar.TitleIcon, image =>
			{
				_titleIcon.Source = image;
				_titleIcon.IsVisible = image != null;
			});
		}
		else
		{
			_titleIcon.Source = null;
			_titleIcon.IsVisible = false;
		}
	}

	public void UpdateBarBackground(MauiToolbar toolbar)
	{
		if (toolbar.BarBackground is MauiControls.SolidColorBrush solidBrush)
		{
			Background = new AvaloniaSolidColorBrush(solidBrush.Color.ToAvaloniaColor());
		}
		else
		{
			Background = new AvaloniaSolidColorBrush(AvaloniaColor.FromRgb(245, 245, 245));
		}
	}

	public void UpdateBarTextColor(MauiToolbar toolbar)
	{
		if (toolbar.BarTextColor != Microsoft.Maui.Graphics.Colors.Transparent)
			_titleText.Foreground = new AvaloniaSolidColorBrush(toolbar.BarTextColor.ToAvaloniaColor());
	}

	public void UpdateIconColor(MauiToolbar toolbar)
	{
		if (toolbar.IconColor != Microsoft.Maui.Graphics.Colors.Transparent)
			_backButton.Foreground = new AvaloniaSolidColorBrush(toolbar.IconColor.ToAvaloniaColor());
	}

	public void UpdateBackButton(MauiToolbar toolbar)
	{
		_backButton.IsVisible = toolbar.BackButtonVisible;
		_backButton.IsEnabled = toolbar.BackButtonEnabled;
	}

	public void UpdateToolbarItems(MauiToolbar toolbar)
	{
		if (_isUpdatingItems && !ReferenceEquals(toolbar, _currentToolbar))
			return;

		_currentToolbar = toolbar;

		if (_isUpdatingItems)
			return;

		_isUpdatingItems = true;
		try
		{
			ResetTrackedItems();

			var items = toolbar.ToolbarItems?.OfType<MauiToolbarItem>().ToList() ?? new List<MauiToolbarItem>();
			if (items.Count == 0)
			{
				_overflowButton.IsVisible = false;
				return;
			}

			var indexedItems = items
				.Select((item, index) => new ToolbarEntry(item, index))
				.ToList();

			var allowOverflow = toolbar.DynamicOverflowEnabled;
			var primary = indexedItems
				.Where(entry => allowOverflow ? entry.Item.Order != MauiToolbarItemOrder.Secondary : true)
				.OrderBy(entry => entry.Item.Order == MauiToolbarItemOrder.Primary ? 0 : 1)
				.ThenBy(entry => entry.Item.Priority)
				.ThenBy(entry => entry.Index)
				.Select(entry => entry.Item)
				.ToList();

			var secondary = allowOverflow
				? indexedItems.Where(entry => entry.Item.Order == MauiToolbarItemOrder.Secondary)
					.OrderBy(entry => entry.Item.Priority)
					.ThenBy(entry => entry.Index)
					.Select(entry => entry.Item)
					.ToList()
				: new List<MauiToolbarItem>();

			foreach (var item in primary)
			{
				SubscribeToolbarItem(item);

				var button = CreateToolbarButton(item);
				_itemButtons[item] = button;
				_itemsHost.Children.Add(button);
			}

			ConfigureOverflow(secondary);
		}
		finally
		{
			_isUpdatingItems = false;
		}
	}

	void ConfigureOverflow(IReadOnlyList<MauiToolbarItem> secondary)
	{
		_overflowFlyout.Items.Clear();
		_overflowMenuItems.Clear();

		if (secondary.Count == 0)
		{
			_overflowButton.IsVisible = false;
			return;
		}

		foreach (var item in secondary)
		{
			SubscribeToolbarItem(item);

			var menuItem = new AvaloniaMenuItem
			{
				Header = item.Text,
				IsEnabled = item.IsEnabled,
				Command = GetOrCreateCommand(item)
			};

			UpdateMenuItemAccelerators(menuItem, item);
			UpdateMenuCheckedState(menuItem, item);
			UpdateMenuIcon(menuItem, item, GetCachedIcon(item));
			AvaloniaSemanticNode.Apply(menuItem, item, item.Text, item.Text);

			if (!_overflowFlyout.Items.Contains(menuItem))
				_overflowFlyout.Items.Add(menuItem);

			_overflowMenuItems[item] = menuItem;
			RefreshItemIcon(item);
		}

		_overflowButton.IsVisible = true;
		_itemsHost.Children.Add(_overflowButton);
	}

	AvaloniaButton CreateToolbarButton(MauiToolbarItem item)
	{
		var button = new AvaloniaButton
		{
			Margin = _buttonMargin,
			HorizontalAlignment = AvaloniaHorizontalAlignment.Right,
			VerticalAlignment = AvaloniaVerticalAlignment.Center,
			MinHeight = 30,
			Command = GetOrCreateCommand(item),
			IsEnabled = item.IsEnabled
		};

		UpdateButtonContent(button, item, GetCachedIcon(item));
		UpdateButtonCheckedState(button, item);
		AvaloniaSemanticNode.Apply(button, item, item.Text, item.Text);
		RefreshItemIcon(item);

		return button;
	}

	void SubscribeToolbarItem(MauiToolbarItem item)
	{
		if (_itemSubscriptions.ContainsKey(item))
			return;

		void Handler(object? sender, PropertyChangedEventArgs args) =>
			OnToolbarItemPropertyChanged(item, args.PropertyName);

		item.PropertyChanged += Handler;
		_itemSubscriptions[item] = Handler;
	}

	void ResetTrackedItems()
	{
		foreach (var subscription in _itemSubscriptions)
			subscription.Key.PropertyChanged -= subscription.Value;
		_itemSubscriptions.Clear();

		foreach (var command in _itemCommands.Values)
			command.Dispose();
		_itemCommands.Clear();

		foreach (var loader in _iconLoaders.Values)
			loader.Dispose();
		_iconLoaders.Clear();

		foreach (var bitmap in _iconCache.Values)
			bitmap?.Dispose();
		_iconCache.Clear();

		_itemButtons.Clear();
		_overflowMenuItems.Clear();
		_itemsHost.Children.Clear();
		_overflowFlyout.Items.Clear();
	}

	ToolbarItemCommand GetOrCreateCommand(MauiToolbarItem item)
	{
		if (_itemCommands.TryGetValue(item, out var command))
			return command;

		command = new ToolbarItemCommand(item);
		_itemCommands[item] = command;
		return command;
	}

	void OnToolbarItemPropertyChanged(MauiToolbarItem item, string? propertyName)
	{
		if (_isUpdatingItems)
			return;

		switch (propertyName)
		{
			case nameof(MauiToolbarItem.Text):
				RunOnUi(() => UpdateTextualState(item));
				break;

			case nameof(MauiControls.MenuItem.IconImageSource):
				RunOnUi(() => RefreshItemIcon(item));
				break;

			case nameof(MauiControls.MenuItem.IsEnabled):
				RunOnUi(() => UpdateEnabledState(item));
				break;

			case nameof(MauiToolbarItem.Order):
			case nameof(MauiToolbarItem.Priority):
				RequestToolbarRebuild();
				break;

			case "KeyboardAccelerators":
				RunOnUi(() => UpdateAccelerators(item));
				break;

			case "IsChecked":
				RunOnUi(() => UpdateCheckedVisuals(item));
				break;
		}
	}

	void UpdateTextualState(MauiToolbarItem item)
	{
		if (_itemButtons.TryGetValue(item, out var button))
		{
			UpdateButtonContent(button, item, GetCachedIcon(item));
			AvaloniaSemanticNode.Apply(button, item, item.Text, item.Text);
		}

		if (_overflowMenuItems.TryGetValue(item, out var menuItem))
		{
			menuItem.Header = item.Text;
			AvaloniaSemanticNode.Apply(menuItem, item, item.Text, item.Text);
		}
	}

	void UpdateEnabledState(MauiToolbarItem item)
	{
		if (_itemButtons.TryGetValue(item, out var button))
			button.IsEnabled = item.IsEnabled;

		if (_overflowMenuItems.TryGetValue(item, out var menuItem))
			menuItem.IsEnabled = item.IsEnabled;
	}

	void UpdateAccelerators(MauiToolbarItem item)
	{
		if (_overflowMenuItems.TryGetValue(item, out var menuItem))
			UpdateMenuItemAccelerators(menuItem, item);
	}

	void UpdateCheckedVisuals(MauiToolbarItem item)
	{
		if (_itemButtons.TryGetValue(item, out var button))
			UpdateButtonCheckedState(button, item);

		if (_overflowMenuItems.TryGetValue(item, out var menuItem))
			UpdateMenuCheckedState(menuItem, item);
	}

	void RequestToolbarRebuild()
	{
		if (_currentToolbar is null || _rebuildPending)
			return;

		_rebuildPending = true;
		RunOnUi(() =>
		{
			_rebuildPending = false;
			if (_currentToolbar is not null)
				UpdateToolbarItems(_currentToolbar);
		});
	}

	void RunOnUi(Action callback)
	{
		if (AvaloniaUiDispatcher.UIThread.CheckAccess())
		{
			callback();
		}
		else
		{
			AvaloniaUiDispatcher.UIThread.Post(callback);
		}
	}

	void UpdateButtonContent(AvaloniaButton button, MauiToolbarItem item, Bitmap? icon)
	{
		Control? iconControl = null;
		if (icon is not null)
		{
			iconControl = new AvaloniaImage
			{
				Source = icon,
				Width = 18,
				Height = 18,
				Stretch = global::Avalonia.Media.Stretch.Uniform
			};
		}

		if (string.IsNullOrWhiteSpace(item.Text))
		{
			button.Content = iconControl ?? (object)(item.Text ?? string.Empty);
			return;
		}

		var textBlock = new AvaloniaTextBlock
		{
			Text = item.Text,
			VerticalAlignment = AvaloniaVerticalAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis
		};

		if (iconControl is null)
		{
			button.Content = textBlock;
			return;
		}

		var stack = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = AvaloniaVerticalAlignment.Center,
			Spacing = 4
		};
		stack.Children.Add(iconControl);
		stack.Children.Add(textBlock);
		button.Content = stack;
	}

	void UpdateMenuItemAccelerators(AvaloniaMenuItem menuItem, MauiToolbarItem item)
	{
		var accelerators = GetKeyboardAccelerators(item);
		var gesture = KeyboardAcceleratorMapper.FromAccelerators(accelerators);
		menuItem.HotKey = gesture;
		menuItem.InputGesture = gesture;
	}

	void UpdateMenuCheckedState(AvaloniaMenuItem menuItem, MauiToolbarItem item)
	{
		var isChecked = TryGetIsChecked(item);
		if (isChecked.HasValue)
		{
			menuItem.ToggleType = MenuItemToggleType.CheckBox;
			menuItem.IsChecked = isChecked.Value;
		}
		else
		{
			menuItem.ToggleType = MenuItemToggleType.None;
			menuItem.IsChecked = false;
		}
	}

	void UpdateButtonCheckedState(AvaloniaButton button, MauiToolbarItem item)
	{
		var isChecked = TryGetIsChecked(item);
		if (isChecked == true)
			button.Classes.Add(CheckedClass);
		else
			button.Classes.Remove(CheckedClass);
	}

	void UpdateMenuIcon(AvaloniaMenuItem menuItem, MauiToolbarItem item, Bitmap? icon)
	{
		if (icon is null)
		{
			menuItem.Icon = null;
			return;
		}

		menuItem.Icon = new AvaloniaImage
		{
			Source = icon,
			Width = 16,
			Height = 16,
			Stretch = global::Avalonia.Media.Stretch.Uniform
		};
	}

	void RefreshItemIcon(MauiToolbarItem item)
	{
		if (_context is null)
			return;

		if (item.IconImageSource is null)
		{
			if (_iconLoaders.TryGetValue(item, out var loader))
				loader.Cancel();

			UpdateIconCache(item, null);
			ApplyIconToVisuals(item, null);
			return;
		}

		var iconLoader = GetOrCreateIconLoader(item);
		iconLoader.Load(_context.Services, item.IconImageSource);
	}

	void ApplyIconToVisuals(MauiToolbarItem item, Bitmap? bitmap)
	{
		if (_itemButtons.TryGetValue(item, out var button))
			UpdateButtonContent(button, item, bitmap);

		if (_overflowMenuItems.TryGetValue(item, out var menuItem))
			UpdateMenuIcon(menuItem, item, bitmap);
	}

	void UpdateIconCache(MauiToolbarItem item, Bitmap? bitmap)
	{
		if (_iconCache.TryGetValue(item, out var existing) && !ReferenceEquals(existing, bitmap))
			existing?.Dispose();

		if (bitmap is null)
			_iconCache.Remove(item);
		else
			_iconCache[item] = bitmap;
	}

	Bitmap? GetCachedIcon(MauiToolbarItem item) =>
		_iconCache.TryGetValue(item, out var bitmap) ? bitmap : null;

	ToolbarItemIconLoader GetOrCreateIconLoader(MauiToolbarItem item)
	{
		if (_iconLoaders.TryGetValue(item, out var loader))
			return loader;

		loader = new ToolbarItemIconLoader(this, item);
		_iconLoaders[item] = loader;
		return loader;
	}

	void OnToolbarIconLoaded(MauiToolbarItem item, Bitmap? bitmap)
	{
		void Apply()
		{
			if (!_itemButtons.ContainsKey(item) && !_overflowMenuItems.ContainsKey(item))
			{
				bitmap?.Dispose();
				return;
			}

			UpdateIconCache(item, bitmap);
			ApplyIconToVisuals(item, bitmap);
		}

		if (AvaloniaUiDispatcher.UIThread.CheckAccess())
			Apply();
		else
			AvaloniaUiDispatcher.UIThread.Post(Apply);
	}

	static IReadOnlyList<IKeyboardAccelerator>? GetKeyboardAccelerators(MauiToolbarItem item)
	{
		if (item is IMenuFlyoutItem menuFlyoutItem)
			return menuFlyoutItem.KeyboardAccelerators;

		if (KeyboardAcceleratorsProperty?.GetValue(item) is IReadOnlyList<IKeyboardAccelerator> list)
			return list;

		return null;
	}

	static bool? TryGetIsChecked(MauiToolbarItem item)
	{
		if (IsCheckedProperty is null)
			return null;

		var value = IsCheckedProperty.GetValue(item);
		if (value is bool boolean)
			return boolean;

		if (value is bool?)
			return (bool?)value;

		return null;
	}

	void LoadImage(ImageSource? source, Action<Bitmap?> onLoaded)
	{
		if (source == null || _context == null)
		{
			onLoaded(null);
			return;
		}

		_ = AvaloniaImageSourceLoader.LoadAsync(source, _context.Services, default)
			.ContinueWith(task =>
			{
				var image = task.IsCompletedSuccessfully ? task.Result : null;
				AvaloniaUiDispatcher.UIThread.Post(() => onLoaded(image));
			});
	}

	struct ToolbarEntry
	{
		public ToolbarEntry(MauiToolbarItem item, int index)
		{
			Item = item;
			Index = index;
		}

		public MauiToolbarItem Item { get; }
		public int Index { get; }
	}

	sealed class ToolbarItemCommand : ICommand, IDisposable
	{
		readonly MauiToolbarItem _item;

		public ToolbarItemCommand(MauiToolbarItem item)
		{
			_item = item;
			_item.PropertyChanged += OnPropertyChanged;
		}

		public bool CanExecute(object? parameter) => _item.IsEnabled;

		public event EventHandler? CanExecuteChanged;

		public void Execute(object? parameter)
		{
			if (_item is IMenuItemController controller)
				controller.Activate();
			else
				_item.Command?.Execute(_item.CommandParameter);
		}

		public void Dispose() => _item.PropertyChanged -= OnPropertyChanged;

		void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(MauiControls.MenuItem.IsEnabled))
				CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	sealed class ToolbarItemIconLoader : IImageSourcePartSetter, IDisposable
	{
		readonly AvaloniaToolbar _owner;
		readonly MauiToolbarItem _item;
		CancellationTokenSource? _cts;

		public ToolbarItemIconLoader(AvaloniaToolbar owner, MauiToolbarItem item)
		{
			_owner = owner;
			_item = item;
		}

		public IElementHandler? Handler => null;

		public IImageSourcePart? ImageSourcePart => _item;

		public void Load(IServiceProvider services, ImageSource? source)
		{
			Cancel();

			if (services is null || source is null)
			{
				SetImageSource(null);
				return;
			}

			_cts = new CancellationTokenSource();
			var token = _cts.Token;

			_ = LoadCoreAsync(services, source, token);
		}

		async Task LoadCoreAsync(IServiceProvider services, ImageSource source, CancellationToken token)
		{
			try
			{
				var bitmap = await AvaloniaImageSourceLoader.LoadAsync(source, services, token).ConfigureAwait(false);
				if (token.IsCancellationRequested)
				{
					bitmap?.Dispose();
					return;
				}

				SetImageSource(bitmap);
			}
			catch
			{
				if (!token.IsCancellationRequested)
					SetImageSource(null);
			}
		}

		public void Cancel()
		{
			if (_cts is null)
				return;

			try
			{
				_cts.Cancel();
			}
			catch (ObjectDisposedException)
			{
			}
			finally
			{
				_cts.Dispose();
				_cts = null;
			}
		}

		public void Dispose() => Cancel();

		public void SetImageSource(object? platformImage)
		{
			var bitmap = platformImage switch
			{
				Bitmap avaloniaBitmap => avaloniaBitmap,
				IImageSourceServiceResult<Bitmap> result => result.Value,
				_ => platformImage as Bitmap
			};

			_owner.OnToolbarIconLoaded(_item, bitmap);
		}
	}
}
