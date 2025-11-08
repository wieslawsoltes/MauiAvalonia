using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Fonts;
using Microsoft.Maui.Avalonia.Graphics;
using Microsoft.Maui.Avalonia.Internal;
using Microsoft.Maui.Avalonia.Platform;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using AvaloniaButton = Avalonia.Controls.Button;

namespace Microsoft.Maui.Avalonia.Handlers;

public class AvaloniaButtonHandler : AvaloniaViewHandler<IButton, AvaloniaButton>, IButtonHandler
{
	static readonly IPropertyMapper<IImage, AvaloniaButtonHandler> ImageMapper = new PropertyMapper<IImage, AvaloniaButtonHandler>();

	static readonly IPropertyMapper<ITextButton, AvaloniaButtonHandler> TextMapper = new PropertyMapper<ITextButton, AvaloniaButtonHandler>()
	{
		[nameof(IText.Text)] = MapText,
		[nameof(ITextStyle.TextColor)] = MapTextColor,
		[nameof(ITextStyle.Font)] = MapFont,
		[nameof(ITextStyle.CharacterSpacing)] = MapCharacterSpacing
	};

	public static IPropertyMapper<IButton, AvaloniaButtonHandler> Mapper = new PropertyMapper<IButton, AvaloniaButtonHandler>(TextMapper, ImageMapper, ViewHandler.ViewMapper)
	{
		[nameof(IButton.Padding)] = MapPadding,
		[nameof(IButtonStroke.CornerRadius)] = MapCornerRadius,
		[nameof(IButtonStroke.StrokeThickness)] = MapStrokeThickness,
		[nameof(IButtonStroke.StrokeColor)] = MapStrokeColor
	};

	public static CommandMapper<IButton, AvaloniaButtonHandler> CommandMapper = new(ViewCommandMapper);

	ImageSourcePartLoader? _imageSourcePartLoader;

	public AvaloniaButtonHandler()
		: base(Mapper, CommandMapper)
	{
	}

	protected override AvaloniaButton CreatePlatformView() =>
		new()
		{
			HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
			VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Center
		};

	protected override void ConnectHandler(AvaloniaButton platformView)
	{
		base.ConnectHandler(platformView);
		platformView.Click += OnClick;
		platformView.PointerPressed += OnPointerPressed;
		platformView.PointerReleased += OnPointerReleased;
	}

	protected override void DisconnectHandler(AvaloniaButton platformView)
	{
		base.DisconnectHandler(platformView);
		platformView.Click -= OnClick;
		platformView.PointerPressed -= OnPointerPressed;
		platformView.PointerReleased -= OnPointerReleased;
	}

	static void MapText(AvaloniaButtonHandler handler, ITextButton textButton)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.Content = textButton.Text ?? string.Empty;
	}

	static void MapTextColor(AvaloniaButtonHandler handler, ITextButton textButton)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.UpdateForegroundColor(textButton);
	}

	static void MapFont(AvaloniaButtonHandler handler, ITextButton textButton)
	{
		if (handler.PlatformView is null)
			return;

		var fontManager = handler.GetRequiredService<IAvaloniaFontManager>();
		handler.PlatformView.UpdateFont(textButton, fontManager);
	}

	static void MapCharacterSpacing(AvaloniaButtonHandler handler, ITextButton textButton)
	{
		if (handler.PlatformView is null)
			return;

		TextBlock.SetLetterSpacing(handler.PlatformView, textButton.CharacterSpacing.ToAvaloniaLetterSpacing());
	}

	static void MapPadding(AvaloniaButtonHandler handler, IButton button)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.Padding = Microsoft.Maui.Avalonia.Platform.ThicknessExtensions.ToAvalonia(button.Padding);
	}

	static void MapCornerRadius(AvaloniaButtonHandler handler, IButton button)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.CornerRadius = Microsoft.Maui.Avalonia.Internal.AvaloniaConversionExtensions.ToAvalonia(button.CornerRadius);
	}

	static void MapStrokeThickness(AvaloniaButtonHandler handler, IButton button)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.BorderThickness = new global::Avalonia.Thickness(button.StrokeThickness);
	}

	static void MapStrokeColor(AvaloniaButtonHandler handler, IButton button)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.BorderBrush = button.StrokeColor?.ToAvaloniaBrush();
	}

	void OnClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
		VirtualView?.Clicked();

	void OnPointerPressed(object? sender, PointerPressedEventArgs e) =>
		VirtualView?.Pressed();

	void OnPointerReleased(object? sender, PointerReleasedEventArgs e) =>
		VirtualView?.Released();

	public ImageSourcePartLoader ImageSourceLoader =>
		_imageSourcePartLoader ??= new ImageSourcePartLoader(new ButtonImageSourcePartSetter(this));

	sealed class ButtonImageSourcePartSetter : IImageSourcePartSetter
	{
		readonly AvaloniaButtonHandler _handler;

		public ButtonImageSourcePartSetter(AvaloniaButtonHandler handler) =>
			_handler = handler;

		public IElementHandler? Handler => _handler;

		public IImageSourcePart? ImageSourcePart => _handler.VirtualView as IImage;

		public void SetImageSource(object? platformImage)
		{
			// Image content is not yet supported for the Avalonia backend.
		}
	}
}
