using Avalonia.Controls;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Platform;
using Microsoft.Maui.Handlers;
using AvaloniaRowDefinition = Avalonia.Controls.RowDefinition;
using AvaloniaColumnDefinition = Avalonia.Controls.ColumnDefinition;

namespace Microsoft.Maui.Avalonia.Handlers;

public class AvaloniaGridLayoutHandler : AvaloniaPanelLayoutHandler<IGridLayout, AvaloniaGridPanel>
{
	public static PropertyMapper<IGridLayout, AvaloniaGridLayoutHandler> Mapper =
		new PropertyMapper<IGridLayout, AvaloniaGridLayoutHandler>(ViewHandler.ViewMapper)
		{
			[nameof(IGridLayout.RowDefinitions)] = MapRowDefinitions,
			[nameof(IGridLayout.ColumnDefinitions)] = MapColumnDefinitions,
			[nameof(IGridLayout.RowSpacing)] = MapSpacing,
			[nameof(IGridLayout.ColumnSpacing)] = MapSpacing
		};

	public AvaloniaGridLayoutHandler()
		: base(Mapper)
	{
	}

	protected override void AddChildControl(IView view, Control control)
	{
		if (PlatformView is null || VirtualView is null)
			return;

		var row = VirtualView.GetRow(view);
		var column = VirtualView.GetColumn(view);
		var rowSpan = VirtualView.GetRowSpan(view);
		var columnSpan = VirtualView.GetColumnSpan(view);

		AvaloniaGrid.SetRow(control, row);
		AvaloniaGrid.SetColumn(control, column);
		AvaloniaGrid.SetRowSpan(control, System.Math.Max(1, rowSpan));
		AvaloniaGrid.SetColumnSpan(control, System.Math.Max(1, columnSpan));

		PlatformView.Children.Add(control);
	}

	protected override void OnChildrenUpdated()
	{
		base.OnChildrenUpdated();
		if (PlatformView is null || VirtualView is null)
			return;

		PlatformView.UpdateDefinitions(VirtualView);
		PlatformView.UpdateSpacing(VirtualView);
	}

	static void MapRowDefinitions(AvaloniaGridLayoutHandler handler, IGridLayout layout) =>
		handler.PlatformView?.UpdateDefinitions(layout);

	static void MapColumnDefinitions(AvaloniaGridLayoutHandler handler, IGridLayout layout) =>
		handler.PlatformView?.UpdateDefinitions(layout);

	static void MapSpacing(AvaloniaGridLayoutHandler handler, IGridLayout layout) =>
		handler.PlatformView?.UpdateSpacing(layout);
}

public sealed class AvaloniaGridPanel : AvaloniaGrid
{
	public void UpdateDefinitions(IGridLayout layout)
	{
		if (layout is null)
			return;

		RowDefinitions.Clear();
		foreach (var row in layout.RowDefinitions)
		{
			RowDefinitions.Add(new AvaloniaRowDefinition
			{
				Height = row.Height.ToAvalonia()
			});
		}

		ColumnDefinitions.Clear();
		foreach (var column in layout.ColumnDefinitions)
		{
			ColumnDefinitions.Add(new AvaloniaColumnDefinition
			{
				Width = column.Width.ToAvalonia()
			});
		}
	}

	public void UpdateSpacing(IGridLayout layout)
	{
		// Avalonia 11.1 does not expose row/column spacing on Grid.
		// TODO: emulate spacing by injecting padding rows once supported.
	}
}
