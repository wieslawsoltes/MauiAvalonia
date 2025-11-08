using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Avalonia.Handlers;

internal sealed class AvaloniaShellFlyoutPresenter : UserControl
{
	readonly ListBox _listBox;
	readonly List<ShellItemWrapper> _items = new();
	Shell? _shell;
	IShellController? _controller;
	bool _suppressSelectionChanged;

	public AvaloniaShellFlyoutPresenter()
	{
		_listBox = new ListBox
		{
			SelectionMode = global::Avalonia.Controls.SelectionMode.Single,
			ItemTemplate = new FuncDataTemplate<ShellItemWrapper>((item, _) =>
				new TextBlock
				{
					Margin = new global::Avalonia.Thickness(8, 4),
					Text = item?.Title ?? string.Empty
				}, true)
		};

		_listBox.SelectionChanged += OnSelectionChanged;
		Content = _listBox;
	}

	public void AttachShell(Shell? shell, IShellController? controller)
	{
		_shell = shell;
		_controller = controller;
		UpdateItems();
		UpdateSelection();
	}

	public void UpdateItems()
	{
		_items.Clear();

		if (_controller?.GetItems() is { } shellItems)
		{
			foreach (var item in shellItems)
				_items.Add(new ShellItemWrapper(item));
		}

		_listBox.ItemsSource = _items.ToArray();
		UpdateSelection();
	}

	public void UpdateSelection()
	{
		if (_shell?.CurrentItem is null)
		{
			SetSelectedWrapper(null);
			return;
		}

		var wrapper = _items.FirstOrDefault(w => ReferenceEquals(w.ShellItem, _shell.CurrentItem));
		SetSelectedWrapper(wrapper);
	}

	void SetSelectedWrapper(ShellItemWrapper? wrapper)
	{
		try
		{
			_suppressSelectionChanged = true;
			_listBox.SelectedItem = wrapper;
			if (wrapper is not null)
				_listBox.ScrollIntoView(wrapper);
		}
		finally
		{
			_suppressSelectionChanged = false;
		}
	}

	async void OnSelectionChanged(object? sender, global::Avalonia.Controls.SelectionChangedEventArgs e)
	{
		if (_suppressSelectionChanged || _controller is null)
			return;

		if (_listBox.SelectedItem is ShellItemWrapper wrapper)
			await NavigateToAsync(wrapper.ShellItem).ConfigureAwait(false);
	}

	Task NavigateToAsync(ShellItem shellItem) =>
		_controller?.OnFlyoutItemSelectedAsync(shellItem) ?? Task.CompletedTask;

	sealed class ShellItemWrapper
	{
		public ShellItemWrapper(ShellItem shellItem) => ShellItem = shellItem ?? throw new ArgumentNullException(nameof(shellItem));

		public ShellItem ShellItem { get; }

		public string Title =>
			!string.IsNullOrWhiteSpace(ShellItem.Title) ? ShellItem.Title :
			!string.IsNullOrWhiteSpace(ShellItem.Route) ? ShellItem.Route :
			ShellItem.ToString() ?? string.Empty;
	}
}
