using Avalonia.Controls;
using Avalonia.Media;
using Microsoft.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Avalonia.Fonts;
using Microsoft.Maui.Avalonia.Internal;
using Microsoft.Maui.Avalonia.Platform;
using Microsoft.Maui.Handlers;

namespace Microsoft.Maui.Avalonia.Handlers;

public class AvaloniaLabelHandler : AvaloniaViewHandler<ILabel, TextBlock>, ILabelHandler
{
	public static IPropertyMapper<ILabel, AvaloniaLabelHandler> Mapper = new PropertyMapper<ILabel, AvaloniaLabelHandler>(ViewHandler.ViewMapper)
	{
		[nameof(IText.Text)] = MapText,
		[nameof(ITextStyle.TextColor)] = MapTextColor,
		[nameof(ITextStyle.Font)] = MapFont,
		[nameof(ITextStyle.CharacterSpacing)] = MapCharacterSpacing,
		[nameof(ITextAlignment.HorizontalTextAlignment)] = MapHorizontalTextAlignment,
		[nameof(ITextAlignment.VerticalTextAlignment)] = MapVerticalTextAlignment,
		[nameof(ILabel.LineHeight)] = MapLineHeight,
		[nameof(ILabel.Padding)] = MapPadding,
		[nameof(ILabel.TextDecorations)] = MapTextDecorations
	};

	public AvaloniaLabelHandler()
		: base(Mapper)
	{
	}

	protected override TextBlock CreatePlatformView() =>
		new()
		{
			TextWrapping = TextWrapping.Wrap,
			VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top
		};

	static void MapText(AvaloniaLabelHandler handler, ILabel label)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.Text = label.Text ?? string.Empty;
	}

	static void MapTextColor(AvaloniaLabelHandler handler, ILabel label)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.UpdateForegroundColor(label);
	}

	static void MapFont(AvaloniaLabelHandler handler, ILabel label)
	{
		if (handler.PlatformView is null)
			return;

		var fontManager = handler.GetRequiredService<IAvaloniaFontManager>();
		handler.PlatformView.UpdateFont(label, fontManager);
	}

	static void MapCharacterSpacing(AvaloniaLabelHandler handler, ILabel label)
	{
		if (handler.PlatformView is null)
			return;

		TextBlock.SetLetterSpacing(handler.PlatformView, label.CharacterSpacing.ToAvaloniaLetterSpacing());
	}

	static void MapHorizontalTextAlignment(AvaloniaLabelHandler handler, ILabel label)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.TextAlignment = label.HorizontalTextAlignment.ToAvaloniaHorizontalAlignment();
	}

	static void MapVerticalTextAlignment(AvaloniaLabelHandler handler, ILabel label)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.VerticalAlignment = label.VerticalTextAlignment.ToAvaloniaVerticalAlignment();
	}

	static void MapLineHeight(AvaloniaLabelHandler handler, ILabel label)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.LineHeight = label.LineHeight <= 0 ? double.NaN : label.LineHeight;
	}

	static void MapPadding(AvaloniaLabelHandler handler, ILabel label)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.Padding = Microsoft.Maui.Avalonia.Platform.ThicknessExtensions.ToAvalonia(label.Padding);
	}

	static void MapTextDecorations(AvaloniaLabelHandler handler, ILabel label)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.TextDecorations = label.TextDecorations.ToAvalonia();
	}
}
