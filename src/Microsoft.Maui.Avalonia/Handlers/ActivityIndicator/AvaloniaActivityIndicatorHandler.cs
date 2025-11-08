using Avalonia.Controls;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using AvaloniaProgressBar = global::Avalonia.Controls.ProgressBar;

namespace Microsoft.Maui.Avalonia.Handlers;

public sealed class AvaloniaActivityIndicatorHandler : AvaloniaViewHandler<IActivityIndicator, AvaloniaProgressBar>
{
	public static readonly IPropertyMapper<IActivityIndicator, AvaloniaActivityIndicatorHandler> Mapper =
		new PropertyMapper<IActivityIndicator, AvaloniaActivityIndicatorHandler>(ViewHandler.ViewMapper)
		{
			[nameof(IActivityIndicator.IsRunning)] = MapIsRunning,
			[nameof(IActivityIndicator.Color)] = MapColor,
			[nameof(IActivityIndicator.Visibility)] = MapIsRunning
		};

	public AvaloniaActivityIndicatorHandler()
		: base(Mapper)
	{
	}

	protected override AvaloniaProgressBar CreatePlatformView() =>
		new()
		{
			IsIndeterminate = true,
			Minimum = 0,
			Maximum = 1,
			Value = 0,
			HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
			VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
		};

	static void MapIsRunning(AvaloniaActivityIndicatorHandler handler, IActivityIndicator indicator)
	{
		if (handler.PlatformView is null)
			return;

		var isRunning = indicator.IsRunning;
		handler.PlatformView.IsIndeterminate = isRunning;
		handler.PlatformView.IsVisible = isRunning && indicator.Visibility == Visibility.Visible;
	}

	static void MapColor(AvaloniaActivityIndicatorHandler handler, IActivityIndicator indicator)
	{
		if (handler.PlatformView is null)
			return;

		var color = indicator.Color;
		if (color.IsDefault())
			handler.PlatformView.ClearValue(AvaloniaProgressBar.ForegroundProperty);
		else
			handler.PlatformView.Foreground = color.ToAvaloniaBrush();
	}
}
