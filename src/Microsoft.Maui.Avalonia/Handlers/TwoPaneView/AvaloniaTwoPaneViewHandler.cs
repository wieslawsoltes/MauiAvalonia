using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Microsoft.Maui;
using Microsoft.Maui.Controls.Foldable;
using Microsoft.Maui.Platform;
using AvaloniaColumnDefinition = Avalonia.Controls.ColumnDefinition;
using AvaloniaRowDefinition = Avalonia.Controls.RowDefinition;
using AvaloniaGridLength = Avalonia.Controls.GridLength;
using AvaloniaGridUnitType = Avalonia.Controls.GridUnitType;

namespace Microsoft.Maui.Avalonia.Handlers;

public class AvaloniaTwoPaneViewHandler : AvaloniaViewHandler<TwoPaneView, AvaloniaGrid>
{
	static readonly PropertyMapper<TwoPaneView, AvaloniaTwoPaneViewHandler> Mapper = new(ViewMapper)
	{
		[nameof(TwoPaneView.Pane1)] = MapPanes,
		[nameof(TwoPaneView.Pane2)] = MapPanes,
		[nameof(TwoPaneView.PanePriority)] = MapLayout,
		[nameof(TwoPaneView.MinWideModeWidth)] = MapLayout,
		[nameof(TwoPaneView.MinTallModeHeight)] = MapLayout,
		[nameof(TwoPaneView.Pane1Length)] = MapLayout,
		[nameof(TwoPaneView.Pane2Length)] = MapLayout
	};

	static readonly CommandMapper<TwoPaneView, AvaloniaTwoPaneViewHandler> CommandMapper = new(ViewCommandMapper);

	readonly Dictionary<IView, IElementHandler> _handlers = new();

	public AvaloniaTwoPaneViewHandler()
		: base(Mapper, CommandMapper)
	{
	}

	protected override AvaloniaGrid CreatePlatformView()
	{
		var grid = new AvaloniaGrid();
		grid.SizeChanged += OnSizeChanged;
		return grid;
	}

	protected override void DisconnectHandler(AvaloniaGrid platformView)
	{
		platformView.SizeChanged -= OnSizeChanged;
		ClearHandlers();
		base.DisconnectHandler(platformView);
	}

	static void MapPanes(AvaloniaTwoPaneViewHandler handler, TwoPaneView view)
	{
		handler.UpdatePaneContents();
		handler.UpdateLayout();
	}

	static void MapLayout(AvaloniaTwoPaneViewHandler handler, TwoPaneView view) => handler.UpdateLayout();

	void OnSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateLayout();

	void UpdatePaneContents()
	{
		if (PlatformView is null)
			return;

		PlatformView.Children.Clear();
		ClearHandlers();

		if (VirtualView is null)
			return;

		var pane1 = CreatePresenter(VirtualView.Pane1);
		if (pane1 is not null)
			PlatformView.Children.Add(pane1);

		var pane2 = CreatePresenter(VirtualView.Pane2);
		if (pane2 is not null)
			PlatformView.Children.Add(pane2);
	}

	ContentControl? CreatePresenter(IView? pane)
	{
		if (pane is null || MauiContext is null)
			return null;

		var handler = pane.ToHandler(MauiContext);
		if (!_handlers.ContainsKey(pane))
			_handlers[pane] = handler;

		return new ContentControl
		{
			Content = handler.PlatformView,
			HorizontalContentAlignment = AvaloniaHorizontalAlignment.Stretch,
			VerticalContentAlignment = AvaloniaVerticalAlignment.Stretch
		};
	}

	void ClearHandlers()
	{
		foreach (var pair in _handlers)
			pair.Value.DisconnectHandler();

		_handlers.Clear();
	}

	void UpdateLayout()
	{
		if (PlatformView is null || VirtualView is null)
			return;

		var bounds = PlatformView.Bounds;
		var canShowSideBySide = bounds.Width >= VirtualView.MinWideModeWidth && VirtualView.Pane2 is not null;
		var canStackTall = !canShowSideBySide && bounds.Height >= VirtualView.MinTallModeHeight && VirtualView.Pane2 is not null;

		PlatformView.RowDefinitions.Clear();
		PlatformView.ColumnDefinitions.Clear();

		foreach (var child in PlatformView.Children)
		{
			AvaloniaGrid.SetRow(child, 0);
			AvaloniaGrid.SetColumn(child, 0);
			AvaloniaGrid.SetColumnSpan(child, 1);
			AvaloniaGrid.SetRowSpan(child, 1);
			child.IsVisible = false;
		}

		if (canShowSideBySide)
		{
			PlatformView.RowDefinitions.Add(ToRowDefinition(GridLength.Star));
			PlatformView.ColumnDefinitions.Add(ToColumnDefinition(VirtualView.Pane1Length));
			PlatformView.ColumnDefinitions.Add(ToColumnDefinition(VirtualView.Pane2Length));

			if (PlatformView.Children.Count > 0)
			{
				PlatformView.Children[0].IsVisible = true;
				AvaloniaGrid.SetColumn(PlatformView.Children[0], 0);
			}
			if (PlatformView.Children.Count > 1)
			{
				PlatformView.Children[1].IsVisible = true;
				AvaloniaGrid.SetColumn(PlatformView.Children[1], 1);
			}
		}
		else if (canStackTall)
		{
			PlatformView.RowDefinitions.Add(ToRowDefinition(VirtualView.Pane1Length));
			PlatformView.RowDefinitions.Add(ToRowDefinition(VirtualView.Pane2Length));
			PlatformView.ColumnDefinitions.Add(ToColumnDefinition(GridLength.Star));

			if (PlatformView.Children.Count > 0)
			{
				PlatformView.Children[0].IsVisible = true;
				AvaloniaGrid.SetRow(PlatformView.Children[0], 0);
			}

			if (PlatformView.Children.Count > 1)
			{
				PlatformView.Children[1].IsVisible = true;
				AvaloniaGrid.SetRow(PlatformView.Children[1], 1);
			}
		}
		else
		{
			PlatformView.RowDefinitions.Add(ToRowDefinition(GridLength.Star));

			var target = VirtualView.PanePriority == TwoPaneViewPriority.Pane1 ? 0 : 1;
			if (PlatformView.Children.Count > target && PlatformView.Children[target] is Control pane)
			{
				pane.IsVisible = true;
				AvaloniaGrid.SetRow(pane, 0);
				AvaloniaGrid.SetColumn(pane, 0);
				AvaloniaGrid.SetColumnSpan(pane, 1);
			}
		}
	}

	static AvaloniaColumnDefinition ToColumnDefinition(GridLength gridLength) =>
		new()
		{
			Width = ToAvaloniaGridLength(gridLength)
		};

	static AvaloniaRowDefinition ToRowDefinition(GridLength gridLength) =>
		new()
		{
			Height = ToAvaloniaGridLength(gridLength)
		};

	static AvaloniaGridLength ToAvaloniaGridLength(GridLength gridLength)
	{
		if (gridLength.IsAuto)
			return AvaloniaGridLength.Auto;

		if (gridLength.IsStar)
			return new AvaloniaGridLength(gridLength.Value, AvaloniaGridUnitType.Star);

		return new AvaloniaGridLength(gridLength.Value, AvaloniaGridUnitType.Pixel);
	}
}
