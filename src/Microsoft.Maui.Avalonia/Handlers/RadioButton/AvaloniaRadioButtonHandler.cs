using Avalonia.Controls;
using Microsoft.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Avalonia.Fonts;
using Microsoft.Maui.Avalonia.Graphics;
using Microsoft.Maui.Avalonia.Internal;
using Microsoft.Maui.Avalonia.Platform;
using Microsoft.Maui.Handlers;
using AvaloniaRadioButton = Avalonia.Controls.RadioButton;

namespace Microsoft.Maui.Avalonia.Handlers;

public class AvaloniaRadioButtonHandler : AvaloniaViewHandler<IRadioButton, AvaloniaRadioButton>, IRadioButtonHandler
{
	public static PropertyMapper<IRadioButton, AvaloniaRadioButtonHandler> Mapper = new(ViewMapper)
	{
		[nameof(IRadioButton.IsChecked)] = MapIsChecked,
		[nameof(IContentView.Content)] = MapContent,
		[nameof(ITextStyle.TextColor)] = MapTextColor,
		[nameof(ITextStyle.Font)] = MapFont,
		[nameof(ITextStyle.CharacterSpacing)] = MapCharacterSpacing,
		[nameof(IPadding.Padding)] = MapPadding,
		[nameof(IButtonStroke.StrokeThickness)] = MapStrokeThickness,
		[nameof(IButtonStroke.StrokeColor)] = MapStrokeColor,
		[nameof(IButtonStroke.CornerRadius)] = MapCornerRadius
	};

	public AvaloniaRadioButtonHandler()
		: base(Mapper)
	{
	}

	protected override AvaloniaRadioButton CreatePlatformView() => new();

	protected override void ConnectHandler(AvaloniaRadioButton platformView)
	{
		base.ConnectHandler(platformView);
		platformView.Checked += OnCheckedChanged;
		platformView.Unchecked += OnCheckedChanged;
	}

	protected override void DisconnectHandler(AvaloniaRadioButton platformView)
	{
		base.DisconnectHandler(platformView);
		platformView.Checked -= OnCheckedChanged;
		platformView.Unchecked -= OnCheckedChanged;
	}

	static void MapIsChecked(AvaloniaRadioButtonHandler handler, IRadioButton radioButton)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.IsChecked = radioButton.IsChecked;
	}

	static void MapContent(AvaloniaRadioButtonHandler handler, IRadioButton radioButton)
	{
		if (handler.PlatformView is null)
			return;

		if (radioButton.PresentedContent is IView view && handler.MauiContext is not null)
		{
			handler.PlatformView.Content = view.ToAvaloniaControl(handler.MauiContext);
		}
		else
		{
			handler.PlatformView.Content = radioButton.Content ?? string.Empty;
		}
	}

	static void MapTextColor(AvaloniaRadioButtonHandler handler, IRadioButton radioButton)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.UpdateForegroundColor(radioButton);
	}

	static void MapFont(AvaloniaRadioButtonHandler handler, IRadioButton radioButton)
	{
		if (handler.PlatformView is null)
			return;

		var fontManager = handler.GetRequiredService<IAvaloniaFontManager>();
		handler.PlatformView.UpdateFont(radioButton, fontManager);
	}

	static void MapCharacterSpacing(AvaloniaRadioButtonHandler handler, IRadioButton radioButton)
	{
		if (handler.PlatformView is null)
			return;

		TextBlock.SetLetterSpacing(handler.PlatformView, radioButton.CharacterSpacing.ToAvaloniaLetterSpacing());
	}

	static void MapPadding(AvaloniaRadioButtonHandler handler, IRadioButton radioButton)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.Padding = Microsoft.Maui.Avalonia.Platform.ThicknessExtensions.ToAvalonia(radioButton.Padding);
	}

	static void MapStrokeThickness(AvaloniaRadioButtonHandler handler, IRadioButton radioButton)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.BorderThickness = new global::Avalonia.Thickness(radioButton.StrokeThickness);
	}

	static void MapStrokeColor(AvaloniaRadioButtonHandler handler, IRadioButton radioButton)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.BorderBrush = radioButton.StrokeColor?.ToAvaloniaBrush();
	}

	static void MapCornerRadius(AvaloniaRadioButtonHandler handler, IRadioButton radioButton)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.CornerRadius = new global::Avalonia.CornerRadius(radioButton.CornerRadius);
	}

	void OnCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
	{
		if (VirtualView is null || PlatformView is null)
			return;

		var value = PlatformView.IsChecked ?? false;
		if (VirtualView.IsChecked != value)
			VirtualView.IsChecked = value;
	}
}
