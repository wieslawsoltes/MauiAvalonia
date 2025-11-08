using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Graphics;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using MauiRectangleShape = Microsoft.Maui.Controls.Shapes.Rectangle;
using MauiEllipseShape = Microsoft.Maui.Controls.Shapes.Ellipse;
using MauiColors = Microsoft.Maui.Graphics.Colors;

namespace Microsoft.Maui.Avalonia.Handlers;

public sealed class AvaloniaIndicatorViewHandler : AvaloniaViewHandler<IIndicatorView, StackPanel>, IIndicatorViewHandler
{
	public static readonly IPropertyMapper<IIndicatorView, AvaloniaIndicatorViewHandler> Mapper =
		new PropertyMapper<IIndicatorView, AvaloniaIndicatorViewHandler>(ViewHandler.ViewMapper)
		{
			[nameof(IIndicatorView.Count)] = MapIndicators,
			[nameof(IIndicatorView.Position)] = MapIndicators,
			[nameof(IIndicatorView.IndicatorSize)] = MapIndicators,
			[nameof(IIndicatorView.IndicatorColor)] = MapIndicators,
			[nameof(IIndicatorView.SelectedIndicatorColor)] = MapIndicators,
			[nameof(IIndicatorView.MaximumVisible)] = MapIndicators,
			[nameof(IIndicatorView.HideSingle)] = MapIndicators,
			[nameof(IIndicatorView.IndicatorsShape)] = MapIndicators
		};

	const double DefaultIndicatorSize = 8d;

	public AvaloniaIndicatorViewHandler()
		: base(Mapper)
	{
	}

	protected override StackPanel CreatePlatformView()
	{
		var panel = new StackPanel
		{
			Spacing = 6,
			HorizontalAlignment = AvaloniaHorizontalAlignment.Center,
			VerticalAlignment = AvaloniaVerticalAlignment.Center
		};

		panel.SetValue(global::Avalonia.Controls.StackPanel.OrientationProperty, global::Avalonia.Layout.Orientation.Horizontal);
		return panel;
	}

	static void MapIndicators(AvaloniaIndicatorViewHandler handler, IIndicatorView indicatorView) =>
		handler.UpdateIndicators();

	void UpdateIndicators()
	{
		if (PlatformView is null)
			return;

		var view = VirtualView;
		if (view is null)
		{
			PlatformView.Children.Clear();
			return;
		}

		var count = Math.Max(0, view.Count);
		if (view.HideSingle && count <= 1)
		{
			PlatformView.Children.Clear();
			PlatformView.IsVisible = false;
			return;
		}

		PlatformView.IsVisible = true;

		var maxVisible = view.MaximumVisible > 0 ? Math.Min(view.MaximumVisible, count) : count;
		if (maxVisible <= 0)
			maxVisible = count;

		var position = Math.Clamp(view.Position, 0, Math.Max(0, count - 1));
		var startIndex = CalculateWindowStart(count, maxVisible, position);
		var size = view.IndicatorSize > 0 ? view.IndicatorSize : DefaultIndicatorSize;

		var defaultBrush = view.IndicatorColor?.ToAvaloniaBrush() ?? new global::Avalonia.Media.SolidColorBrush(AvaloniaColor.FromRgb(211, 211, 211));
		var selectedBrush = view.SelectedIndicatorColor?.ToAvaloniaBrush() ?? new global::Avalonia.Media.SolidColorBrush(AvaloniaColor.FromRgb(0, 0, 0));

		PlatformView.Children.Clear();

		for (var i = 0; i < maxVisible && startIndex + i < count; i++)
		{
			var index = startIndex + i;
			var isSelected = index == position;
			PlatformView.Children.Add(CreateIndicator(size, isSelected ? selectedBrush : defaultBrush, view));
		}
	}

	static int CalculateWindowStart(int count, int maxVisible, int position)
	{
		if (count <= maxVisible)
			return 0;

		var half = maxVisible / 2;
		var start = Math.Max(0, position - half);
		if (start + maxVisible > count)
			start = count - maxVisible;
		return Math.Max(0, start);
	}

	Control CreateIndicator(double size, IBrush brush, IIndicatorView view)
	{
		var margin = new global::Avalonia.Thickness(2, 0, 2, 0);
		Control indicator;

		if (view.IndicatorsShape is MauiRectangleShape)
		{
			indicator = new Rectangle
			{
				Width = size,
				Height = size / 2,
				Fill = brush
			};
		}
		else if (view.IndicatorsShape is MauiEllipseShape)
		{
			indicator = new Ellipse
			{
				Width = size,
				Height = size,
				Fill = brush
			};
		}
		else
		{
			indicator = new Ellipse
			{
				Width = size,
				Height = size,
				Fill = brush
			};
		}

		indicator.Margin = margin;
		return indicator;
	}
}
