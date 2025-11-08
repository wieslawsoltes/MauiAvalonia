using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Fonts;
using Microsoft.Maui.Avalonia.Graphics;
using Microsoft.Maui.Avalonia.Platform;
using Microsoft.Maui.Handlers;
using AvaloniaTextChangedEventArgs = global::Avalonia.Controls.TextChangedEventArgs;

namespace Microsoft.Maui.Avalonia.Handlers;

public sealed class AvaloniaSearchBarHandler : AvaloniaViewHandler<ISearchBar, TextBox>
{
	public static readonly IPropertyMapper<ISearchBar, AvaloniaSearchBarHandler> Mapper =
		new PropertyMapper<ISearchBar, AvaloniaSearchBarHandler>(ViewHandler.ViewMapper)
		{
			[nameof(ITextInput.Text)] = MapText,
			[nameof(ISearchBar.Placeholder)] = MapPlaceholder,
			[nameof(ISearchBar.PlaceholderColor)] = MapPlaceholderColor,
			[nameof(ITextStyle.TextColor)] = MapTextColor,
			[nameof(ITextStyle.Font)] = MapFont,
			[nameof(ITextStyle.CharacterSpacing)] = MapCharacterSpacing,
			[nameof(ITextAlignment.HorizontalTextAlignment)] = MapHorizontalTextAlignment,
			[nameof(ITextAlignment.VerticalTextAlignment)] = MapVerticalTextAlignment,
			[nameof(ISearchBar.Background)] = MapBackground,
			[nameof(ISearchBar.IsReadOnly)] = MapIsReadOnly,
			[nameof(ISearchBar.MaxLength)] = MapMaxLength,
			[nameof(ISearchBar.IsTextPredictionEnabled)] = MapTextInputOptions,
			[nameof(ISearchBar.IsSpellCheckEnabled)] = MapTextInputOptions,
			[nameof(ISearchBar.Keyboard)] = MapTextInputOptions,
			[nameof(ISearchBar.CancelButtonColor)] = MapCancelButtonColor
		};

	public static readonly CommandMapper<ISearchBar, AvaloniaSearchBarHandler> CommandMapper = new(ViewCommandMapper);

	public AvaloniaSearchBarHandler()
		: base(Mapper, CommandMapper)
	{
	}

	protected override TextBox CreatePlatformView() =>
		new()
		{
			Watermark = string.Empty,
			VerticalContentAlignment = AvaloniaVerticalAlignment.Center
		};

	protected override void ConnectHandler(TextBox platformView)
	{
		base.ConnectHandler(platformView);
		platformView.TextChanged += OnTextChanged;
		platformView.KeyUp += OnKeyUp;
	}

	protected override void DisconnectHandler(TextBox platformView)
	{
		platformView.TextChanged -= OnTextChanged;
		platformView.KeyUp -= OnKeyUp;
		base.DisconnectHandler(platformView);
	}

	static void MapText(AvaloniaSearchBarHandler handler, ISearchBar searchBar)
	{
		if (handler.PlatformView is null)
			return;

		var text = searchBar.Text ?? string.Empty;
		if (handler.PlatformView.Text != text)
			handler.PlatformView.Text = text;
	}

	static void MapPlaceholder(AvaloniaSearchBarHandler handler, ISearchBar searchBar)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.Watermark = searchBar.Placeholder ?? string.Empty;
	}

	static void MapPlaceholderColor(AvaloniaSearchBarHandler handler, ISearchBar searchBar)
	{
		// Avalonia TextBox does not currently expose placeholder brush customization.
	}

	static void MapTextColor(AvaloniaSearchBarHandler handler, ISearchBar searchBar)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.UpdateForegroundColor(searchBar);
	}

	static void MapFont(AvaloniaSearchBarHandler handler, ISearchBar searchBar)
	{
		if (handler.PlatformView is null)
			return;

		var fontManager = handler.GetRequiredService<IAvaloniaFontManager>();
		handler.PlatformView.UpdateFont(searchBar, fontManager);
	}

	static void MapCharacterSpacing(AvaloniaSearchBarHandler handler, ISearchBar searchBar)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.LetterSpacing = searchBar.CharacterSpacing.ToAvaloniaLetterSpacing();
	}

	static void MapHorizontalTextAlignment(AvaloniaSearchBarHandler handler, ISearchBar searchBar)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.TextAlignment = searchBar.HorizontalTextAlignment.ToAvaloniaHorizontalAlignment();
	}

	static void MapVerticalTextAlignment(AvaloniaSearchBarHandler handler, ISearchBar searchBar)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.VerticalContentAlignment = searchBar.VerticalTextAlignment.ToAvaloniaVerticalAlignment();
	}

	static void MapBackground(AvaloniaSearchBarHandler handler, ISearchBar searchBar)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.Background = searchBar.Background?.ToAvaloniaBrush();
	}

	static void MapIsReadOnly(AvaloniaSearchBarHandler handler, ISearchBar searchBar)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.IsReadOnly = searchBar.IsReadOnly;
	}

	static void MapMaxLength(AvaloniaSearchBarHandler handler, ISearchBar searchBar)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.MaxLength = searchBar.MaxLength <= 0 ? int.MaxValue : searchBar.MaxLength;
	}

	static void MapTextInputOptions(AvaloniaSearchBarHandler handler, ISearchBar searchBar)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.UpdateTextInputOptions(searchBar, isMultiline: false);
	}

	static void MapCancelButtonColor(AvaloniaSearchBarHandler handler, ISearchBar searchBar)
	{
		// No native cancel button yet; placeholder for future support.
	}

	void OnTextChanged(object? sender, AvaloniaTextChangedEventArgs e)
	{
		if (VirtualView is null || PlatformView is null)
			return;

		var text = PlatformView.Text ?? string.Empty;
		if (VirtualView.Text != text)
			VirtualView.Text = text;
	}

	void OnKeyUp(object? sender, KeyEventArgs e)
	{
		if (VirtualView is null)
			return;

		if (e.Key is Key.Enter or Key.Return)
		{
			VirtualView.SearchButtonPressed();
			e.Handled = true;
		}
	}

}
