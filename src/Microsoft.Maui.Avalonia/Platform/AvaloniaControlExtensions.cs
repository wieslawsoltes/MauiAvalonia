using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.TextInput;
using AvaloniaAutomation = Avalonia.Automation;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Accessibility;
using Microsoft.Maui.Avalonia.Fonts;
using Microsoft.Maui.Avalonia.Graphics;
using Microsoft.Maui.Avalonia.Internal;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using MauiReturnType = Microsoft.Maui.ReturnType;
using Thickness = Microsoft.Maui.Thickness;
using MauiColor = Microsoft.Maui.Graphics.Color;

namespace Microsoft.Maui.Avalonia.Platform;

internal static class AvaloniaControlExtensions
{
	public static void UpdateBackground(this Control control, IView view)
	{
		var brush = view.Background?.ToAvaloniaBrush();

		switch (control)
		{
			case AvaloniaBorderControl border:
				border.Background = brush;
				break;
			case global::Avalonia.Controls.Panel panel:
				panel.Background = brush;
				break;
			case TemplatedControl templated:
				templated.Background = brush;
				break;
			case AvaloniaContentPresenter presenter:
				presenter.Background = brush;
				break;
			case global::Avalonia.Controls.TextBlock textBlock:
				textBlock.Background = brush;
				break;
		}
	}

	public static void UpdateOpacity(this Control control, IView view) =>
		control.Opacity = view.Opacity;

	public static void UpdateIsEnabled(this Control control, IView view) =>
		control.IsEnabled = view.IsEnabled;

	public static void UpdateAutomationId(this Control control, IView view)
	{
		var id = view.AutomationId;
		if (string.IsNullOrWhiteSpace(id))
			control.ClearValue(AvaloniaAutomation.AutomationProperties.AutomationIdProperty);
		else
			AvaloniaAutomation.AutomationProperties.SetAutomationId(control, id);
	}

	public static void UpdateInputTransparent(this Control control, IView view)
	{
		control.IsHitTestVisible = !view.InputTransparent;
	}

	public static void UpdateForegroundColor(this Control control, ITextStyle textStyle)
	{
		if (textStyle.TextColor is not MauiColor color)
			return;

		switch (control)
		{
			case TemplatedControl templated:
				templated.Foreground = color.ToAvaloniaBrush();
				break;
			case global::Avalonia.Controls.TextBlock textBlock:
				textBlock.Foreground = color.ToAvaloniaBrush();
				break;
			case AvaloniaContentPresenter presenter:
				presenter.Foreground = color.ToAvaloniaBrush();
				break;
		}
	}

	public static void UpdateFont(this Control control, ITextStyle textStyle, IAvaloniaFontManager fontManager)
	{
		var font = textStyle.Font;
		var fontFamily = fontManager.GetFontFamily(font);
		var currentSize = control switch
		{
			TemplatedControl templated => templated.FontSize,
			global::Avalonia.Controls.TextBlock textBlock => textBlock.FontSize,
			AvaloniaContentPresenter presenter => presenter.FontSize,
			_ => 0
		};
		var fontSize = fontManager.GetFontSize(font, currentSize);
		var fontWeight = font.ToAvaloniaFontWeight();
		var fontStyle = font.ToAvaloniaFontStyle();

		switch (control)
		{
			case TemplatedControl templated:
				templated.FontFamily = fontFamily;
				templated.FontSize = fontSize;
				templated.FontWeight = fontWeight;
				templated.FontStyle = fontStyle;
				break;
			case global::Avalonia.Controls.TextBlock textBlock:
				textBlock.FontFamily = fontFamily;
				textBlock.FontSize = fontSize;
				textBlock.FontWeight = fontWeight;
				textBlock.FontStyle = fontStyle;
				break;
			case AvaloniaContentPresenter presenter:
				presenter.FontFamily = fontFamily;
				presenter.FontSize = fontSize;
				presenter.FontWeight = fontWeight;
				presenter.FontStyle = fontStyle;
				break;
		}
	}

	public static void UpdatePadding(this Control control, Thickness padding)
	{
		if (control is TemplatedControl templated)
			templated.Padding = padding.ToAvalonia();
	}

	public static void ApplySemantics(this Control control, IView view)
		=> AvaloniaSemanticNode.Apply(control, view);

	public static void UpdateTextInputOptions(this TextBox textBox, ITextInput input, bool isMultiline, MauiReturnType? returnType = null, bool isPassword = false)
	{
		if (textBox is null || input is null)
			return;

		var contentType = GetContentType(input.Keyboard, isPassword);
		TextInputOptions.SetContentType(textBox, contentType);
		TextInputOptions.SetMultiline(textBox, isMultiline);
		TextInputOptions.SetIsSensitive(textBox, isPassword);

		var flags = GetKeyboardFlags(input.Keyboard);
		// Avalonia 11.1 does not expose a suggestions toggle; rely on auto-capitalization instead.

		var autoCapitalize = flags.HasFlag(KeyboardFlags.CapitalizeSentence) || flags.HasFlag(KeyboardFlags.CapitalizeWord);
		TextInputOptions.SetAutoCapitalization(textBox, autoCapitalize && !flags.HasFlag(KeyboardFlags.CapitalizeNone));
		TextInputOptions.SetUppercase(textBox, flags.HasFlag(KeyboardFlags.CapitalizeCharacter));
		TextInputOptions.SetLowercase(textBox, flags.HasFlag(KeyboardFlags.CapitalizeNone));

		if (returnType.HasValue)
			TextInputOptions.SetReturnKeyType(textBox, MapReturnKeyType(returnType.Value));
		else
			textBox.ClearValue(TextInputOptions.ReturnKeyTypeProperty);
	}

	static TextInputContentType GetContentType(Microsoft.Maui.Keyboard? keyboard, bool isPassword)
	{
		var mauiKeyboard = keyboard ?? Microsoft.Maui.Keyboard.Default;

		if (isPassword)
			return TextInputContentType.Password;

		if (ReferenceEquals(mauiKeyboard, Microsoft.Maui.Keyboard.Email))
			return TextInputContentType.Email;
		if (ReferenceEquals(mauiKeyboard, Microsoft.Maui.Keyboard.Url))
			return TextInputContentType.Url;
		if (ReferenceEquals(mauiKeyboard, Microsoft.Maui.Keyboard.Telephone))
			return TextInputContentType.Digits;
		if (ReferenceEquals(mauiKeyboard, Microsoft.Maui.Keyboard.Numeric))
			return TextInputContentType.Number;
		if (ReferenceEquals(mauiKeyboard, Microsoft.Maui.Keyboard.Chat))
			return TextInputContentType.Social;
		if (ReferenceEquals(mauiKeyboard, Microsoft.Maui.Keyboard.Text))
			return TextInputContentType.Alpha;

		return TextInputContentType.Normal;
	}

	static KeyboardFlags GetKeyboardFlags(Microsoft.Maui.Keyboard? keyboard) =>
		keyboard is CustomKeyboard custom ? custom.Flags : KeyboardFlags.None;

	static TextInputReturnKeyType MapReturnKeyType(MauiReturnType returnType) =>
		returnType switch
		{
			MauiReturnType.Done => TextInputReturnKeyType.Done,
			MauiReturnType.Go => TextInputReturnKeyType.Go,
			MauiReturnType.Next => TextInputReturnKeyType.Next,
			MauiReturnType.Search => TextInputReturnKeyType.Search,
			MauiReturnType.Send => TextInputReturnKeyType.Send,
			_ => TextInputReturnKeyType.Default
		};
}
