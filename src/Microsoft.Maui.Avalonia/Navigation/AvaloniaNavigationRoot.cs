using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Microsoft.Maui.Avalonia.Platform;
using AvaloniaWindow = Avalonia.Controls.Window;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaBorder = Avalonia.Controls.Border;
using AvaloniaTextBlock = Avalonia.Controls.TextBlock;
using AvaloniaImage = Avalonia.Controls.Image;
using AvaloniaPoint = Avalonia.Point;
using MauiRect = Microsoft.Maui.Graphics.Rect;
using Thickness = Microsoft.Maui.Thickness;

namespace Microsoft.Maui.Avalonia.Navigation;

internal sealed class AvaloniaNavigationRoot : IAvaloniaNavigationRoot
{
	readonly AvaloniaNavigationRootView _rootView = new();
	AvaloniaWindow? _window;

	public Control RootView => _rootView;

	public void Attach(AvaloniaWindow window)
	{
		_window = window ?? throw new ArgumentNullException(nameof(window));
		_rootView.AttachWindow(window);
		window.Content = _rootView;
	}

	public void Detach()
	{
		_rootView.DetachWindow();
		_window = null;
	}

	public void SetPlaceholder(string message)
	{
		SetContent(new TextBlock
		{
			Text = message,
			TextWrapping = TextWrapping.Wrap,
			Margin = new AvaloniaThickness(32),
			HorizontalAlignment = AvaloniaHorizontalAlignment.Center,
			VerticalAlignment = AvaloniaVerticalAlignment.Center
		});
		SetContentPadding(new Thickness());
	}

	public void SetContent(Control? control) => _rootView.SetContent(control);

	public void SetContentPadding(Thickness padding) => _rootView.SetContentPadding(padding);

	public void SetToolbar(Control? control) => _rootView.SetToolbar(control);

	public void SetMenu(Control? control) => _rootView.SetMenu(control);

	public void SetTitle(string? title) => _rootView.SetTitle(title);

	public void SetTitleBar(Control? control, IReadOnlyList<Control>? passthroughElements) =>
		_rootView.SetTitleBar(control, passthroughElements);

	public void SetDragRectangles(IReadOnlyList<MauiRect> rectangles) =>
		_rootView.SetDragRectangles(rectangles);

	public event EventHandler? SafeAreaChanged
	{
		add => _rootView.SafeAreaChanged += value;
		remove => _rootView.SafeAreaChanged -= value;
	}

	public Thickness GetSafeAreaInsets() => _rootView.SafeAreaInsets;
}

internal sealed class AvaloniaNavigationRootView : AvaloniaGrid
{
	readonly AvaloniaBorder _titleBarBorder;
	readonly ContentControl _titleBarHost;
	readonly AvaloniaTextBlock _titleText;
	readonly StackPanel _systemButtonPanel;
	readonly AvaloniaButton _closeButton;
	readonly AvaloniaButton _maximizeButton;
	readonly AvaloniaButton _minimizeButton;
	readonly ContentControl _menuHost;
	readonly ContentControl _toolbarHost;
	readonly ContentControl _contentHost;
	readonly AvaloniaBorder _contentWrapper;
	readonly AvaloniaBorder _menuBorder;
	readonly AvaloniaBorder _toolbarBorder;
	readonly Canvas _dragVisualOverlay;

	AvaloniaWindow? _window;
	Control? _currentTitleBar;
	bool _customChromeActive;
	readonly List<MauiRect> _dragRectangles = new();
	readonly List<AvaloniaRect> _scaledDragRectangles = new();
	readonly HashSet<Control> _passthroughControls = new();
	double _renderScaling = 1d;
	string? _title;
	Thickness _safeAreaInsets = new();

	public event EventHandler? SafeAreaChanged;
	public Thickness SafeAreaInsets => _safeAreaInsets;

	public AvaloniaNavigationRootView()
	{
		RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto");

		Background = Brushes.Transparent;
		ClipToBounds = true;

		_titleText = new AvaloniaTextBlock
		{
			VerticalAlignment = AvaloniaVerticalAlignment.Center,
			FontSize = 14,
			TextTrimming = TextTrimming.CharacterEllipsis
		};

		_titleBarHost = new ContentControl
		{
			Content = CreateDefaultTitlePresenter(),
			HorizontalAlignment = AvaloniaHorizontalAlignment.Stretch,
			VerticalAlignment = AvaloniaVerticalAlignment.Center
		};

		_systemButtonPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = AvaloniaHorizontalAlignment.Right,
			VerticalAlignment = AvaloniaVerticalAlignment.Stretch,
			Spacing = 0
		};

		_closeButton = CreateChromeButton("✕");
		_maximizeButton = CreateChromeButton("⬜");
		_minimizeButton = CreateChromeButton("–");

		_systemButtonPanel.Children.Add(_minimizeButton);
		_systemButtonPanel.Children.Add(_maximizeButton);
		_systemButtonPanel.Children.Add(_closeButton);

		var titleGrid = new AvaloniaGrid
		{
			ColumnDefinitions = new ColumnDefinitions("*,Auto"),
			HorizontalAlignment = AvaloniaHorizontalAlignment.Stretch,
			VerticalAlignment = AvaloniaVerticalAlignment.Stretch
		};

		_titleBarHost.SetValue(AvaloniaGrid.ColumnProperty, 0);
		_systemButtonPanel.SetValue(AvaloniaGrid.ColumnProperty, 1);
		titleGrid.Children.Add(_titleBarHost);
		titleGrid.Children.Add(_systemButtonPanel);

		_titleBarBorder = new AvaloniaBorder
		{
			Background = new AvaloniaSolidColorBrush(AvaloniaColor.FromRgb(32, 32, 32)),
			Height = 36,
			HorizontalAlignment = AvaloniaHorizontalAlignment.Stretch,
			Child = titleGrid,
			BorderBrush = new AvaloniaSolidColorBrush(AvaloniaColor.FromRgb(64, 64, 64)),
			BorderThickness = new AvaloniaThickness(0, 0, 0, 1)
		};
		_titleBarBorder.SetValue(AvaloniaGrid.RowProperty, 0);

		_menuHost = new ContentControl
		{
			HorizontalAlignment = AvaloniaHorizontalAlignment.Stretch,
			VerticalAlignment = AvaloniaVerticalAlignment.Stretch
		};

		_menuBorder = new AvaloniaBorder
		{
			Child = _menuHost,
			BorderBrush = new AvaloniaSolidColorBrush(AvaloniaColor.FromRgb(64, 64, 64)),
			BorderThickness = new AvaloniaThickness(0, 0, 0, 1)
		};
		_menuBorder.SetValue(AvaloniaGrid.RowProperty, 1);

		_toolbarHost = new ContentControl
		{
			HorizontalAlignment = AvaloniaHorizontalAlignment.Stretch,
			VerticalAlignment = AvaloniaVerticalAlignment.Stretch
		};

		_toolbarBorder = new AvaloniaBorder
		{
			Child = _toolbarHost,
			BorderBrush = new AvaloniaSolidColorBrush(AvaloniaColor.FromRgb(64, 64, 64)),
			BorderThickness = new AvaloniaThickness(0, 0, 0, 1)
		};
		_toolbarBorder.SetValue(AvaloniaGrid.RowProperty, 2);

		_contentHost = new ContentControl
		{
			HorizontalAlignment = AvaloniaHorizontalAlignment.Stretch,
			VerticalAlignment = AvaloniaVerticalAlignment.Stretch
		};

		_contentWrapper = new AvaloniaBorder
		{
			Child = _contentHost,
			Background = Brushes.Transparent
		};
		_contentWrapper.SetValue(AvaloniaGrid.RowProperty, 0);
		_contentWrapper.SetValue(AvaloniaGrid.RowSpanProperty, 5);
		_contentWrapper.ZIndex = 0;

		_dragVisualOverlay = new Canvas
		{
			IsHitTestVisible = false,
			Opacity = 0
		};
		_dragVisualOverlay.SetValue(AvaloniaGrid.RowProperty, 0);
		_dragVisualOverlay.SetValue(AvaloniaGrid.RowSpanProperty, 5);
		_dragVisualOverlay.ZIndex = 20;

		_titleBarBorder.ZIndex = 10;
		_menuBorder.ZIndex = 10;
		_toolbarBorder.ZIndex = 10;

		Children.Add(_titleBarBorder);
		Children.Add(_menuBorder);
		Children.Add(_toolbarBorder);
		Children.Add(_contentWrapper);
		Children.Add(_dragVisualOverlay);

		UpdateToolbarVisibility();
		UpdateMenuVisibility();
		_titleBarBorder.IsVisible = false;

		PointerPressed += OnPointerPressed;
		DoubleTapped += OnPointerDoubleTapped;
		LayoutUpdated += (_, __) => UpdateSafeAreaInsets();
	}

	public void AttachWindow(AvaloniaWindow window)
	{
		_window = window ?? throw new ArgumentNullException(nameof(window));
		_renderScaling = _window.RenderScaling;
		UpdateScaledDragRectangles();
		_window.PropertyChanged += OnWindowPropertyChanged;
		_window.ScalingChanged += OnWindowScalingChanged;
		_minimizeButton.Click += (_, _) => _window.WindowState = WindowState.Minimized;
		_maximizeButton.Click += (_, _) => ToggleWindowState();
		_closeButton.Click += (_, _) => _window.Close();
		UpdateChromeState();
		UpdateMaximizeGlyph();
		UpdateSafeAreaInsets();
	}

	public void DetachWindow()
	{
		if (_window != null)
		{
			_window.PropertyChanged -= OnWindowPropertyChanged;
			_window.ScalingChanged -= OnWindowScalingChanged;
		}
		_window = null;
	}

	public void SetContent(Control? control)
	{
		_contentHost.Content = control;
	}

	public void SetContentPadding(Thickness padding)
	{
		_contentWrapper.Padding = padding.ToAvalonia();
	}

	public void SetToolbar(Control? control)
	{
		_toolbarHost.Content = control;
		UpdateToolbarVisibility();
		UpdateSafeAreaInsets();
	}

	public void SetMenu(Control? control)
	{
		_menuHost.Content = control;
		UpdateMenuVisibility();
		UpdateSafeAreaInsets();
	}

	public void SetTitle(string? title)
	{
		_title = title;
		_titleText.Text = title ?? string.Empty;
	}

	public void SetTitleBar(Control? control, IReadOnlyList<Control>? passthroughElements)
	{
		if (_currentTitleBar == control)
			return;

		_currentTitleBar = control;
		_passthroughControls.Clear();

		if (passthroughElements != null)
		{
			foreach (var element in passthroughElements)
			{
				if (element != null)
					_passthroughControls.Add(element);
			}
		}

		_titleBarHost.Content = control ?? CreateDefaultTitlePresenter();
		UpdateChromeState();
		UpdateSafeAreaInsets();
	}

	public void SetDragRectangles(IReadOnlyList<MauiRect> rectangles)
	{
		_dragRectangles.Clear();

		if (rectangles != null)
		{
			_dragRectangles.AddRange(rectangles);
		}

		UpdateScaledDragRectangles();
		UpdateChromeState();
	}

	Control CreateDefaultTitlePresenter()
	{
		return new AvaloniaGrid
		{
			ColumnDefinitions = new ColumnDefinitions("Auto,*"),
			Children =
			{
				new AvaloniaTextBlock
				{
					Text = "\u25A0",
					VerticalAlignment = AvaloniaVerticalAlignment.Center,
					Margin = new AvaloniaThickness(8, 0, 4, 0),
					Opacity = 0.6
				},
				_titleText
			}
		};
	}

	AvaloniaButton CreateChromeButton(string glyph)
	{
		return new AvaloniaButton
		{
			Content = glyph,
			HorizontalAlignment = AvaloniaHorizontalAlignment.Stretch,
			VerticalAlignment = AvaloniaVerticalAlignment.Stretch,
			Padding = new AvaloniaThickness(12, 0, 12, 0),
			Background = Brushes.Transparent,
			BorderThickness = new AvaloniaThickness(0),
			Classes = { "ChromeButton" }
		};
	}

	void UpdateMenuVisibility()
	{
		_menuBorder.IsVisible = _menuHost.Content != null;
		UpdateSafeAreaInsets();
	}

	void UpdateToolbarVisibility()
	{
		_toolbarBorder.IsVisible = _toolbarHost.Content != null;
		UpdateSafeAreaInsets();
	}

	void ToggleWindowState()
	{
		if (_window is null)
			return;

		_window.WindowState = _window.WindowState == WindowState.Maximized
			? WindowState.Normal
			: WindowState.Maximized;
	}

	void UpdateChromeState()
	{
		bool shouldUseCustom = _currentTitleBar != null || _dragRectangles.Count > 0;

		if (!_customChromeActive && shouldUseCustom)
		{
			EnableCustomChrome();
		}
		else if (_customChromeActive && !shouldUseCustom)
		{
			DisableCustomChrome();
		}
	}

	void EnableCustomChrome()
	{
		if (_window is null)
			return;

		_window.SystemDecorations = SystemDecorations.None;
		_window.ExtendClientAreaToDecorationsHint = true;
		_window.ExtendClientAreaTitleBarHeightHint = _titleBarBorder.Bounds.Height > 0
			? _titleBarBorder.Bounds.Height
			: 36;
		_titleBarBorder.IsVisible = true;
		_customChromeActive = true;
		UpdateSafeAreaInsets();
	}

	void DisableCustomChrome()
	{
		if (_window is null)
			return;

		_window.ExtendClientAreaToDecorationsHint = false;
		_window.SystemDecorations = SystemDecorations.Full;
		_titleBarBorder.IsVisible = false;
		_customChromeActive = false;
		UpdateSafeAreaInsets();
	}

	void OnPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (!_customChromeActive || _window is null)
			return;

		var point = e.GetPosition(this);
		if (IsPointInDragRegion(point))
		{
			try
			{
				_window.BeginMoveDrag(e);
			}
			catch
			{
				// ignored when drag is invoked twice quickly
			}
		}
	}

	void OnPointerDoubleTapped(object? sender, global::Avalonia.Input.TappedEventArgs e)
	{
		if (!_customChromeActive || _window is null)
			return;

		var point = e.GetPosition(this);
		if (IsPointInDefaultTitleRegion(ScalePoint(point)))
		{
			ToggleWindowState();
		}
	}

	bool IsPointInDragRegion(AvaloniaPoint point)
	{
		var scaledPoint = ScalePoint(point);

		if (IsPointInDefaultTitleRegion(scaledPoint))
			return true;

		if (_scaledDragRectangles.Count == 0)
			return false;

		foreach (var rect in _scaledDragRectangles)
		{
			if (rect.Contains(scaledPoint))
				return true;
		}

		return false;
	}

	bool IsPointInDefaultTitleRegion(AvaloniaPoint scaledPoint)
	{
		var origin = _titleBarBorder.TranslatePoint(default, this);
		if (origin == null)
			return false;

		var rect = ScaleRect(new AvaloniaRect(origin.Value, _titleBarBorder.Bounds.Size));
		if (!rect.Contains(scaledPoint))
			return false;

		if (IsPointWithinControl(scaledPoint, _systemButtonPanel))
			return false;

		if (_passthroughControls.Any(control => IsPointWithinControl(scaledPoint, control)))
			return false;

		return true;
	}

	bool IsPointWithinControl(AvaloniaPoint scaledPoint, Control control)
	{
		var origin = control.TranslatePoint(default, this);
		if (origin == null)
			return false;

		var rect = ScaleRect(new AvaloniaRect(origin.Value, control.Bounds.Size));
		return rect.Contains(scaledPoint);
	}

	void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property == AvaloniaWindow.WindowStateProperty)
		{
			UpdateMaximizeGlyph();
		}
	}

	void UpdateMaximizeGlyph()
	{
		if (_window is null)
			return;

		_maximizeButton.Content = _window.WindowState == global::Avalonia.Controls.WindowState.Maximized ? "🗗" : "⬜";
	}

	void UpdateSafeAreaInsets()
	{
		double topInset = 0;

		if (_titleBarBorder.IsVisible)
			topInset += _titleBarBorder.Bounds.Height;

		if (_menuBorder.IsVisible)
			topInset += _menuBorder.Bounds.Height;

		if (_toolbarBorder.IsVisible)
			topInset += _toolbarBorder.Bounds.Height;

		var newInsets = new Thickness(0, topInset, 0, 0);
		if (!_safeAreaInsets.Equals(newInsets))
		{
			_safeAreaInsets = newInsets;
			SafeAreaChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	void OnWindowScalingChanged(object? sender, EventArgs e) => UpdateRenderScaling();

	void UpdateRenderScaling()
	{
		var newScaling = _window?.RenderScaling ?? 1d;
		if (Math.Abs(newScaling - _renderScaling) < 0.001)
			return;

		_renderScaling = newScaling;
		UpdateScaledDragRectangles();
		UpdateSafeAreaInsets();
	}

	void UpdateScaledDragRectangles()
	{
		_scaledDragRectangles.Clear();

		if (_dragRectangles.Count == 0)
			return;

		foreach (var rect in _dragRectangles)
			_scaledDragRectangles.Add(ScaleRect(rect));
	}

	AvaloniaPoint ScalePoint(AvaloniaPoint point) =>
		_renderScaling == 1d ? point : new AvaloniaPoint(point.X * _renderScaling, point.Y * _renderScaling);

	AvaloniaRect ScaleRect(AvaloniaRect rect)
	{
		if (_renderScaling == 1d)
			return rect;

		return new AvaloniaRect(
			rect.X * _renderScaling,
			rect.Y * _renderScaling,
			rect.Width * _renderScaling,
			rect.Height * _renderScaling);
	}

	AvaloniaRect ScaleRect(MauiRect rect) =>
		ScaleRect(new AvaloniaRect(rect.X, rect.Y, rect.Width, rect.Height));
}
