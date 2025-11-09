using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Avalonia.Handlers;

internal static class AvaloniaToolbarMapper
{
	static bool _initialized;

	public static void EnsureInitialized()
	{
		if (_initialized)
			return;

		_initialized = true;

		ToolbarHandler.Mapper.ReplaceMapping<Toolbar, IToolbarHandler>(nameof(IToolbar.Title), MapTitle);
		ToolbarHandler.Mapper.ReplaceMapping<Toolbar, IToolbarHandler>(nameof(Toolbar.ToolbarItems), MapToolbarItems);
		ToolbarHandler.Mapper.ReplaceMapping<Toolbar, IToolbarHandler>(nameof(Toolbar.TitleView), MapTitleView);
		ToolbarHandler.Mapper.ReplaceMapping<Toolbar, IToolbarHandler>(nameof(Toolbar.TitleIcon), MapTitleIcon);
		ToolbarHandler.Mapper.ReplaceMapping<Toolbar, IToolbarHandler>(nameof(Toolbar.BarBackground), MapBarBackground);
		ToolbarHandler.Mapper.ReplaceMapping<Toolbar, IToolbarHandler>(nameof(Toolbar.BarTextColor), MapBarTextColor);
		ToolbarHandler.Mapper.ReplaceMapping<Toolbar, IToolbarHandler>(nameof(Toolbar.IconColor), MapIconColor);
		ToolbarHandler.Mapper.ReplaceMapping<Toolbar, IToolbarHandler>(nameof(Toolbar.BackButtonVisible), MapBackButtonVisible);
		ToolbarHandler.Mapper.ReplaceMapping<Toolbar, IToolbarHandler>(nameof(Toolbar.BackButtonEnabled), MapBackButtonEnabled);
		ToolbarHandler.Mapper.ReplaceMapping<Toolbar, IToolbarHandler>(nameof(IToolbar.IsVisible), MapIsVisible);
	}

	static AvaloniaToolbar? GetPlatformToolbar(IToolbarHandler handler) => handler.PlatformView as AvaloniaToolbar;

	static Toolbar? GetControlsToolbar(IToolbar toolbar) => toolbar as Toolbar;

	static void MapTitle(IToolbarHandler handler, IToolbar toolbar)
	{
		if (GetPlatformToolbar(handler) is AvaloniaToolbar platformToolbar && toolbar is Toolbar controlsToolbar)
			platformToolbar.UpdateTitle(controlsToolbar);
	}

	static void MapToolbarItems(IToolbarHandler handler, IToolbar toolbar)
	{
		var controlsToolbar = GetControlsToolbar(toolbar);
		if (controlsToolbar is null)
			return;

		var platformToolbar = GetPlatformToolbar(handler);
		if (platformToolbar is null)
			return;

		platformToolbar.SetContext(handler.MauiContext);
		platformToolbar.UpdateToolbarItems(controlsToolbar);
	}

	static void MapTitleView(IToolbarHandler handler, IToolbar toolbar)
	{
		if (GetPlatformToolbar(handler) is AvaloniaToolbar platformToolbar && toolbar is Toolbar controlsToolbar)
		{
			platformToolbar.SetContext(handler.MauiContext);
			platformToolbar.UpdateTitleView(controlsToolbar);
		}
	}

	static void MapTitleIcon(IToolbarHandler handler, IToolbar toolbar)
	{
		if (GetPlatformToolbar(handler) is AvaloniaToolbar platformToolbar && toolbar is Toolbar controlsToolbar)
		{
			platformToolbar.UpdateTitleIcon(controlsToolbar);
		}
	}

	static void MapBarBackground(IToolbarHandler handler, IToolbar toolbar)
	{
		if (GetPlatformToolbar(handler) is AvaloniaToolbar platformToolbar && toolbar is Toolbar controlsToolbar)
		{
			platformToolbar.UpdateBarBackground(controlsToolbar);
		}
	}

	static void MapBarTextColor(IToolbarHandler handler, IToolbar toolbar)
	{
		if (GetPlatformToolbar(handler) is AvaloniaToolbar platformToolbar && toolbar is Toolbar controlsToolbar)
		{
			platformToolbar.UpdateBarTextColor(controlsToolbar);
		}
	}

	static void MapIconColor(IToolbarHandler handler, IToolbar toolbar)
	{
		if (GetPlatformToolbar(handler) is AvaloniaToolbar platformToolbar && toolbar is Toolbar controlsToolbar)
		{
			platformToolbar.UpdateIconColor(controlsToolbar);
		}
	}

	static void MapBackButtonVisible(IToolbarHandler handler, IToolbar toolbar)
	{
		if (GetPlatformToolbar(handler) is AvaloniaToolbar platformToolbar && toolbar is Toolbar controlsToolbar)
		{
			platformToolbar.UpdateBackButton(controlsToolbar);
		}
	}

	static void MapBackButtonEnabled(IToolbarHandler handler, IToolbar toolbar)
	{
		MapBackButtonVisible(handler, toolbar);
	}

	static void MapIsVisible(IToolbarHandler handler, IToolbar toolbar)
	{
		if (GetPlatformToolbar(handler) is AvaloniaToolbar platformToolbar)
		{
			platformToolbar.IsVisible = toolbar.IsVisible;
		}
	}
}
