using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Graphics;
using Microsoft.Maui.Avalonia.Handlers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;

using AvaloniaImage = Avalonia.Controls.Image;
using AvaloniaSelectionChangedEventArgs = Avalonia.Controls.SelectionChangedEventArgs;

namespace Microsoft.Maui.Avalonia.Handlers;

public sealed class AvaloniaTabbedViewHandler : ViewHandler<ITabbedView, TabControl>, ITabbedViewHandler
{
	static readonly IPropertyMapper<ITabbedView, ITabbedViewHandler> _mapper =
		new PropertyMapper<ITabbedView, ITabbedViewHandler>(ViewHandler.ViewMapper)
		{
			[nameof(IView.Background)] = MapBackground
		};

	readonly Dictionary<Page, TabRegistration> _tabs = new();
	TabbedPage? _tabbedPage;

	public AvaloniaTabbedViewHandler()
		: base(_mapper)
	{
	}

	protected override TabControl CreatePlatformView() =>
		new()
		{
			TabStripPlacement = Dock.Top,
			HorizontalContentAlignment = AvaloniaHorizontalAlignment.Stretch,
			VerticalContentAlignment = AvaloniaVerticalAlignment.Stretch
		};

	protected override void ConnectHandler(TabControl platformView)
	{
		base.ConnectHandler(platformView);
		platformView.SelectionChanged += OnSelectionChanged;
		AttachTabbedPage();
	}

	protected override void DisconnectHandler(TabControl platformView)
	{
		platformView.SelectionChanged -= OnSelectionChanged;
		DetachTabbedPage();
		base.DisconnectHandler(platformView);
	}

	void AttachTabbedPage()
	{
		if (VirtualView is not TabbedPage tabbedPage || MauiContext is null)
			return;

		_tabbedPage = tabbedPage;
		_tabbedPage.PagesChanged += OnPagesChanged;
		_tabbedPage.CurrentPageChanged += OnCurrentPageChanged;
		_tabbedPage.PropertyChanged += OnTabbedPagePropertyChanged;
		RebuildTabs();
		SyncSelection();
		UpdateBarAppearance();
	}

	void DetachTabbedPage()
	{
		foreach (var tab in _tabs.Values)
			tab.Dispose();

		_tabs.Clear();
		if (_tabbedPage is not null)
		{
			_tabbedPage.PagesChanged -= OnPagesChanged;
			_tabbedPage.CurrentPageChanged -= OnCurrentPageChanged;
			_tabbedPage.PropertyChanged -= OnTabbedPagePropertyChanged;
			_tabbedPage = null;
		}
		PlatformView.Items?.Clear();
	}

	void OnPagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (_tabbedPage is null)
			return;

		if (e.Action == NotifyCollectionChangedAction.Reset)
		{
			RebuildTabs();
			return;
		}

		if (e.NewItems is not null)
		{
			foreach (var page in e.NewItems)
			{
				if (page is Page newPage)
					AddTab(newPage);
			}
		}

		if (e.OldItems is not null)
		{
			foreach (var page in e.OldItems)
			{
				if (page is Page oldPage)
					RemoveTab(oldPage);
			}
		}

		SyncSelection();
	}

	void OnCurrentPageChanged(object? sender, EventArgs e) => SyncSelection();

	void OnTabbedPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_tabbedPage is null)
			return;

		if (e.PropertyName == nameof(TabbedPage.BarBackground) ||
			e.PropertyName == nameof(TabbedPage.BarBackgroundColor) ||
			e.PropertyName == nameof(TabbedPage.BarTextColor))
		{
			UpdateBarAppearance();
		}
		else if (e.PropertyName == nameof(TabbedPage.SelectedTabColor) ||
			e.PropertyName == nameof(TabbedPage.UnselectedTabColor))
		{
			UpdateHeaderColors();
		}
	}

	void RebuildTabs()
	{
		if (_tabbedPage is null || MauiContext is null)
			return;

		foreach (var tab in _tabs.Values)
			tab.Dispose();

		_tabs.Clear();
		PlatformView.Items?.Clear();

		foreach (var page in _tabbedPage.Children)
			AddTab(page);

		UpdateHeaderColors();
	}

	void AddTab(Page page)
	{
		if (MauiContext is null)
			return;

		var content = page.ToAvaloniaControl(MauiContext);
		if (content is null)
			return;

		var header = BuildHeader(page, out var titleBlock);
		var tabItem = new TabItem
		{
			Tag = page,
			Content = content,
			Header = header,
		};

		PlatformView.Items?.Add(tabItem);

		var registration = new TabRegistration(page, tabItem, header, titleBlock);
		page.PropertyChanged += registration.OnPagePropertyChanged;
		_tabs[page] = registration;
		registration.UpdateHeaderTitle(page.Title);
		registration.UpdateIcon(page.IconImageSource, MauiContext.Services);
	}

	StackPanel BuildHeader(Page page, out TextBlock titleBlock)
	{
		titleBlock = new TextBlock
		{
			Text = string.IsNullOrEmpty(page.Title) ? page.GetType().Name : page.Title,
			VerticalAlignment = AvaloniaVerticalAlignment.Center
		};

		return new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 4,
			VerticalAlignment = AvaloniaVerticalAlignment.Center,
			Children = { titleBlock }
		};
	}

	void RemoveTab(Page page)
	{
		if (!_tabs.TryGetValue(page, out var registration))
			return;

		page.PropertyChanged -= registration.OnPagePropertyChanged;
		_tabs.Remove(page);
		registration.Dispose();
		PlatformView.Items?.Remove(registration.TabItem);
	}

	void SyncSelection()
	{
		if (_tabbedPage is null)
			return;

		if (_tabbedPage.CurrentPage is null)
		{
			PlatformView.SelectedItem = null;
			UpdateHeaderColors();
			return;
		}

		if (_tabs.TryGetValue(_tabbedPage.CurrentPage, out var registration))
			PlatformView.SelectedItem = registration.TabItem;
		else if (PlatformView.Items?.Count > 0)
			PlatformView.SelectedIndex = 0;

		UpdateHeaderColors();
	}

	void OnSelectionChanged(object? sender, AvaloniaSelectionChangedEventArgs e)
	{
		if (_tabbedPage is null)
			return;

		if (PlatformView.SelectedItem is TabItem tab &&
			tab.Tag is Page page &&
			_tabbedPage.CurrentPage != page)
		{
			_tabbedPage.CurrentPage = page;
		}

		UpdateHeaderColors();
	}

	void UpdateBarAppearance()
	{
		if (_tabbedPage is null || PlatformView is null)
			return;

		if (VirtualView?.Background is not null)
		{
			PlatformView.Background = VirtualView.Background?.ToAvaloniaBrush();
		}
		else
		{
			var background = ConvertBrush(_tabbedPage.BarBackground)
				?? _tabbedPage.BarBackgroundColor.ToAvaloniaBrush();

			if (background is not null)
				PlatformView.Background = background;
			else
				PlatformView.ClearValue(TemplatedControl.BackgroundProperty);
		}

		UpdateHeaderColors();
	}

	void UpdateHeaderColors()
	{
		if (_tabbedPage is null)
			return;

		var selectedBrush = _tabbedPage.SelectedTabColor?.ToAvaloniaBrush()
			?? _tabbedPage.BarTextColor.ToAvaloniaBrush()
			?? Brushes.Black;
		var unselectedBrush = _tabbedPage.UnselectedTabColor?.ToAvaloniaBrush()
			?? Brushes.Gray;

		foreach (var tab in _tabs.Values)
		{
			var isSelected = _tabbedPage.CurrentPage == tab.Page;
			tab.SetHeaderForeground(isSelected ? selectedBrush : unselectedBrush);
		}
	}

	ITabbedView ITabbedViewHandler.VirtualView => VirtualView;

	static IBrush? ConvertBrush(Microsoft.Maui.Controls.Brush? brush) =>
		brush switch
		{
			Microsoft.Maui.Controls.SolidColorBrush solid => new AvaloniaSolidColorBrush(solid.Color.ToAvaloniaColor()),
			_ => null
		};

	sealed class TabRegistration : IDisposable
	{
		readonly TextBlock _titleBlock;
		readonly StackPanel _headerPanel;
		readonly AvaloniaImage _iconImage;
		CancellationTokenSource? _iconCts;

		public TabRegistration(Page page, TabItem tabItem, StackPanel header, TextBlock titleBlock)
		{
			Page = page;
			TabItem = tabItem;
			_headerPanel = header;
			_titleBlock = titleBlock;
			_iconImage = new AvaloniaImage
			{
				Width = 16,
				Height = 16,
				Margin = new AvaloniaThickness(0, 0, 6, 0),
				IsVisible = false
			};
			_headerPanel.Children.Insert(0, _iconImage);
		}

		public Page Page { get; }
		public TabItem TabItem { get; }

		public void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is not Page page)
				return;

			if (e.PropertyName == Page.TitleProperty.PropertyName)
				UpdateHeaderTitle(page.Title);
			else if (e.PropertyName == Page.IconImageSourceProperty.PropertyName)
				UpdateIcon(page.IconImageSource, page.Handler?.MauiContext?.Services);
		}

		public void UpdateHeaderTitle(string? title) =>
			_titleBlock.Text = string.IsNullOrEmpty(title) ? Page.GetType().Name : title;

		public void UpdateIcon(ImageSource? source, IServiceProvider? services)
		{
			_iconCts?.Cancel();
			_iconCts = null;

			if (source is null || services is null)
			{
				_iconImage.Source = null;
				_iconImage.IsVisible = false;
				return;
			}

			var cts = new CancellationTokenSource();
			_iconCts = cts;

			_ = AvaloniaImageSourceLoader.LoadAsync(source, services, cts.Token)
				.ContinueWith(task =>
				{
					if (cts.IsCancellationRequested)
						return;

					var bitmap = task.IsCompletedSuccessfully ? task.Result : null;
					AvaloniaUiDispatcher.UIThread.Post(() =>
					{
						if (cts.IsCancellationRequested)
							return;

						_iconImage.Source = bitmap;
						_iconImage.IsVisible = bitmap is not null;
					});
				});
		}

		public void SetHeaderForeground(IBrush brush)
		{
			_titleBlock.Foreground = brush;
			if (_iconImage.Source is not null)
				_iconImage.Opacity = brush switch
				{
					AvaloniaSolidColorBrush scb => scb.Color.A / 255d,
					_ => 1d
				};
		}

		public void Dispose()
		{
			_iconCts?.Cancel();
			_iconCts?.Dispose();
			_iconImage.Source = null;
			_iconImage.IsVisible = false;
			_titleBlock.Text = string.Empty;
		}
	}

	static void MapBackground(AvaloniaTabbedViewHandler handler, ITabbedView view) =>
		handler.UpdateViewBackground();

	void UpdateViewBackground()
	{
		if (PlatformView is null)
			return;

		if (VirtualView?.Background is not null)
		{
			PlatformView.Background = VirtualView.Background?.ToAvaloniaBrush();
		}
		else
		{
			UpdateBarAppearance();
		}
	}
}
