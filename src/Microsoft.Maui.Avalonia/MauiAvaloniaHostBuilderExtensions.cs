using System;
using Avalonia.Threading;
using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui;
using Microsoft.Maui.Accessibility;
using Microsoft.Maui.Animations;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Avalonia.Animations;
using Microsoft.Maui.Avalonia.ApplicationModel;
using Microsoft.Maui.Avalonia.Devices;
using Microsoft.Maui.Avalonia.Dispatching;
using Microsoft.Maui.Avalonia.Navigation;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Avalonia.Handlers;
using Microsoft.Maui.Avalonia.Fonts;
using Microsoft.Maui.Avalonia.Hosting;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Internals;
using MauiControls = Microsoft.Maui.Controls;

namespace Microsoft.Maui.Hosting;

/// <summary>
/// Entry points that allow MAUI apps to opt into the Avalonia backend while the
/// implementation is under construction.
/// </summary>
public static class MauiAvaloniaHostBuilderExtensions
{
	/// <summary>
	/// Registers placeholder services so application projects can start referencing
	/// the Avalonia backend without compiler errors. Concrete services will be added
	/// as each phase of the roadmap lands.
	/// </summary>
	public static MauiAppBuilder UseMauiAvaloniaHost(this MauiAppBuilder builder)
	{
		AvaloniaToolbarMapper.EnsureInitialized();
		DependencyService.RegisterSingleton<ISystemResourcesProvider>(new AvaloniaSystemResourcesProvider());

		builder.Services.TryAddSingleton<AvaloniaMauiHostMarker>();
		builder.Services.TryAddSingleton<IAvaloniaWindowHost, AvaloniaWindowHost>();
		builder.Services.TryAddSingleton<IDeviceDisplay, AvaloniaDeviceDisplay>();
		builder.Services.TryAddSingleton<IDispatcherProvider, AvaloniaDispatcherProvider>();

		var deviceInfo = new AvaloniaDeviceInfo();
		AvaloniaDeviceInfo.TryRegisterAsDefault(deviceInfo);
		builder.Services.TryAddSingleton<IDeviceInfo>(deviceInfo);

		var appInfo = new AvaloniaAppInfo();
		AvaloniaAppInfo.TryRegisterAsDefault(appInfo);
		builder.Services.TryAddSingleton<IAppInfo>(appInfo);
		var clipboard = new AvaloniaClipboard();
		AvaloniaClipboard.TryRegisterAsDefault(clipboard);
		builder.Services.TryAddSingleton<IClipboard>(clipboard);

		var semanticScreenReader = new AvaloniaSemanticScreenReader();
		AvaloniaSemanticScreenReader.TryRegisterAsDefault(semanticScreenReader);
		builder.Services.TryAddSingleton<ISemanticScreenReader>(semanticScreenReader);
		builder.Services.AddSingleton<Microsoft.Maui.Dispatching.IDispatcher>(sp =>
			sp.GetRequiredService<IDispatcherProvider>().GetForCurrentThread() ??
			new AvaloniaDispatcher(global::Avalonia.Threading.Dispatcher.UIThread));
		builder.Services.TryAddSingleton<IAnimationManager>(_ => new AnimationManager(new AvaloniaTicker()));
		builder.Services.TryAddScoped<IAvaloniaNavigationRoot, AvaloniaNavigationRoot>();
		builder.Services.AddSingleton<IFontManager, AvaloniaFontManager>();
		builder.Services.AddSingleton<IAvaloniaFontManager>(sp => (AvaloniaFontManager)sp.GetRequiredService<IFontManager>());
		builder.Services.AddSingleton<IEmbeddedFontLoader, AvaloniaEmbeddedFontLoader>();
		builder.ConfigureMauiHandlers(handlers =>
		{
			handlers.AddHandler<IToolbar, AvaloniaToolbarHandler>();
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Toolbar", typeof(AvaloniaToolbarHandler));
			handlers.AddHandler<IStackNavigationView, AvaloniaNavigationViewHandler>();
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.NavigationPage", typeof(AvaloniaNavigationViewHandler));
			handlers.AddHandler<IFlyoutView, AvaloniaFlyoutViewHandler>();
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.FlyoutPage", typeof(AvaloniaFlyoutViewHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Shell", typeof(AvaloniaFlyoutViewHandler));
			handlers.AddHandler<ITabbedView, AvaloniaTabbedViewHandler>();
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.TabbedPage", typeof(AvaloniaTabbedViewHandler));
			handlers.AddHandler<IWindow, AvaloniaWindowHandler>();
			handlers.AddHandler<IContentView, AvaloniaContentViewHandler>();
			handlers.AddHandler<ILayout, AvaloniaLayoutHandler>();
			handlers.AddHandler<IStackLayout, AvaloniaStackLayoutHandler>();
			handlers.AddHandler<IGridLayout, AvaloniaGridLayoutHandler>();
			handlers.AddHandler<IScrollView, AvaloniaScrollViewHandler>();
			handlers.AddHandler<MauiControls.CollectionView, AvaloniaCollectionViewHandler>();
			handlers.AddHandler<MauiControls.CarouselView, AvaloniaCarouselViewHandler>();
			handlers.AddHandler<IActivityIndicator, AvaloniaActivityIndicatorHandler>();
			handlers.AddHandler<IRefreshView, AvaloniaRefreshViewHandler>();
			handlers.AddHandler<IPicker, AvaloniaPickerHandler>();
			handlers.AddHandler<ISearchBar, AvaloniaSearchBarHandler>();
			handlers.AddHandler<IStepper, AvaloniaStepperHandler>();
			handlers.AddHandler<IIndicatorView, AvaloniaIndicatorViewHandler>();
			handlers.AddHandler<ISwipeView, AvaloniaSwipeViewHandler>();
			handlers.AddHandler<ISwipeItemMenuItem, AvaloniaSwipeItemMenuItemHandler>();
			handlers.AddHandler<Popup, AvaloniaPopupHandler>();
			handlers.AddHandler<Expander, AvaloniaExpanderHandler>();
			handlers.AddHandler<ILabel, AvaloniaLabelHandler>();
			handlers.AddHandler<IButton, AvaloniaButtonHandler>();
			handlers.AddHandler<IImage, AvaloniaImageHandler>();
			handlers.AddHandler<IEntry, AvaloniaEntryHandler>();
			handlers.AddHandler<IEditor, AvaloniaEditorHandler>();
			handlers.AddHandler<ICheckBox, AvaloniaCheckBoxHandler>();
			handlers.AddHandler<IRadioButton, AvaloniaRadioButtonHandler>();
			handlers.AddHandler<ISlider, AvaloniaSliderHandler>();
			handlers.AddHandler<IProgress, AvaloniaProgressBarHandler>();
			handlers.AddHandler<ISwitch, AvaloniaSwitchHandler>();
			handlers.AddHandler<IDatePicker, AvaloniaDatePickerHandler>();
			handlers.AddHandler<ITimePicker, AvaloniaTimePickerHandler>();
			handlers.AddHandler<IGraphicsView, AvaloniaGraphicsViewHandler>();
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Window", typeof(AvaloniaWindowHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Page", typeof(AvaloniaPageHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.ContentView", typeof(AvaloniaContentViewHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Layout", typeof(AvaloniaLayoutHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.StackLayout", typeof(AvaloniaStackLayoutHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.VerticalStackLayout", typeof(AvaloniaStackLayoutHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.HorizontalStackLayout", typeof(AvaloniaStackLayoutHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Grid", typeof(AvaloniaGridLayoutHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.ScrollView", typeof(AvaloniaScrollViewHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.ActivityIndicator", typeof(AvaloniaActivityIndicatorHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.BoxView", typeof(AvaloniaBoxViewHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.RefreshView", typeof(AvaloniaRefreshViewHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Picker", typeof(AvaloniaPickerHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.SearchBar", typeof(AvaloniaSearchBarHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Stepper", typeof(AvaloniaStepperHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.IndicatorView", typeof(AvaloniaIndicatorViewHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Picker", typeof(AvaloniaPickerHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Stepper", typeof(AvaloniaStepperHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Label", typeof(AvaloniaLabelHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Button", typeof(AvaloniaButtonHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Image", typeof(AvaloniaImageHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Entry", typeof(AvaloniaEntryHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Editor", typeof(AvaloniaEditorHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.CollectionView", typeof(AvaloniaCollectionViewHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.CheckBox", typeof(AvaloniaCheckBoxHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.RadioButton", typeof(AvaloniaRadioButtonHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Slider", typeof(AvaloniaSliderHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.ProgressBar", typeof(AvaloniaProgressBarHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.Switch", typeof(AvaloniaSwitchHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.DatePicker", typeof(AvaloniaDatePickerHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.TimePicker", typeof(AvaloniaTimePickerHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.GraphicsView", typeof(AvaloniaGraphicsViewHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.SwipeView", typeof(AvaloniaSwipeViewHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.SwipeItem", typeof(AvaloniaSwipeItemMenuItemHandler));
			TryAddControlsHandler(handlers, "Microsoft.Maui.Controls.SwipeItemView", typeof(AvaloniaContentViewHandler));
			TryAddControlsHandler(handlers, "CommunityToolkit.Maui.Views.Popup", typeof(AvaloniaPopupHandler), "CommunityToolkit.Maui");
			TryAddControlsHandler(handlers, "CommunityToolkit.Maui.Views.Expander", typeof(AvaloniaExpanderHandler), "CommunityToolkit.Maui");
			TryAddControlsHandler(handlers, "CommunityToolkit.Maui.Views.DrawingView", typeof(AvaloniaGraphicsViewHandler), "CommunityToolkit.Maui");
		});
		return builder;
	}

	static void TryAddControlsHandler(IMauiHandlersCollection handlers, string typeName, Type handlerType, string assemblyName = "Microsoft.Maui.Controls")
	{
		var typeIdentifier = $"{typeName}, {assemblyName}";
		var targetType = Type.GetType(typeIdentifier, throwOnError: false);
		if (targetType is null)
			return;

		handlers.AddHandler(targetType, handlerType);
	}

	private sealed class AvaloniaMauiHostMarker;
}
