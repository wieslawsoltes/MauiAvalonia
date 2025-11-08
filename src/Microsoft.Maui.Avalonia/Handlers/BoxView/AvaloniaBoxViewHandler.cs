using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Graphics;
using Microsoft.Maui.Avalonia.Internal;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using AvaloniaBorder = global::Avalonia.Controls.Border;

namespace Microsoft.Maui.Avalonia.Handlers;

public sealed class AvaloniaBoxViewHandler : AvaloniaViewHandler<IShapeView, AvaloniaBorder>
{
	public static readonly IPropertyMapper<IShapeView, AvaloniaBoxViewHandler> Mapper =
		new PropertyMapper<IShapeView, AvaloniaBoxViewHandler>(ViewHandler.ViewMapper)
		{
			[nameof(IShapeView.Shape)] = MapShape,
			[nameof(IShapeView.Fill)] = MapFill,
			[nameof(IShapeView.Aspect)] = MapAspect
		};

	public AvaloniaBoxViewHandler()
		: base(Mapper)
	{
	}

	protected override AvaloniaBorder CreatePlatformView() =>
		new()
		{
			HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
			VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
			Background = Colors.Transparent.ToAvaloniaBrush()
		};

	static void MapFill(AvaloniaBoxViewHandler handler, IShapeView shapeView)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.Background = shapeView.Fill?.ToAvaloniaBrush() ?? Colors.Transparent.ToAvaloniaBrush();
	}

	static void MapShape(AvaloniaBoxViewHandler handler, IShapeView shapeView)
	{
		if (handler.PlatformView is null)
			return;

		MapFill(handler, shapeView);

		var cornerRadius = TryGetCornerRadius(shapeView);
		handler.PlatformView.CornerRadius = cornerRadius.ToAvalonia();

		handler.PlatformView.BorderThickness = shapeView.StrokeThickness > 0
			? new global::Avalonia.Thickness(shapeView.StrokeThickness)
			: new global::Avalonia.Thickness(0);

		handler.PlatformView.BorderBrush = shapeView.Stroke?.ToAvaloniaBrush();
	}

	static void MapAspect(AvaloniaBoxViewHandler handler, IShapeView shapeView)
	{
		// Avalonia Border stretches by default; no-op for now.
	}

	static CornerRadius TryGetCornerRadius(IShapeView shapeView)
	{
		if (shapeView is BoxView boxView)
			return boxView.CornerRadius;

		if (shapeView.Shape is BoxView shapeBoxView)
			return shapeBoxView.CornerRadius;

		return default;
	}
}
