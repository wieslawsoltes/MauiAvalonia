using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Maui.Avalonia.Internal;
using Microsoft.Maui.Avalonia.Platform;
using Microsoft.Maui.Controls;
using AvaloniaImage = Avalonia.Controls.Image;
using MauiButton = Microsoft.Maui.Controls.Button;

namespace Microsoft.Maui.Avalonia.Handlers;

internal sealed class AvaloniaButtonContentPresenter : StackPanel
{
	readonly AvaloniaImage _image;
	readonly TextBlock _textBlock;
	MauiButton.ButtonContentLayout _layout = new(MauiButton.ButtonContentLayout.ImagePosition.Left, 10);

	public AvaloniaButtonContentPresenter()
	{
		Orientation = Orientation.Horizontal;
		Spacing = 0;
		HorizontalAlignment = AvaloniaHorizontalAlignment.Center;
		VerticalAlignment = AvaloniaVerticalAlignment.Center;

		_image = new AvaloniaImage
		{
			Stretch = global::Avalonia.Media.Stretch.Uniform,
			HorizontalAlignment = AvaloniaHorizontalAlignment.Center,
			VerticalAlignment = AvaloniaVerticalAlignment.Center,
			IsVisible = false
		};

		_textBlock = new TextBlock
		{
			VerticalAlignment = AvaloniaVerticalAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis,
			IsVisible = false
		};

		Children.Add(_image);
		Children.Add(_textBlock);
	}

	public void UpdateText(string? text)
	{
		_textBlock.Text = text ?? string.Empty;
		_textBlock.IsVisible = !string.IsNullOrEmpty(_textBlock.Text);
		UpdateSpacing();
	}

	public void UpdateImage(Bitmap? bitmap)
	{
		_image.Source = bitmap;
		_image.IsVisible = bitmap is not null;
		UpdateSpacing();
	}

	public void UpdateCharacterSpacing(double characterSpacing) =>
		TextBlock.SetLetterSpacing(_textBlock, characterSpacing.ToAvaloniaLetterSpacing());

	public void UpdateLayout(MauiButton.ButtonContentLayout layout)
	{
		_layout = layout;
		UpdateOrientation();
		UpdateSpacing();
	}

	public void Reset()
	{
		_image.Source = null;
		_image.IsVisible = false;
		_textBlock.Text = string.Empty;
		_textBlock.IsVisible = false;
		UpdateSpacing();
	}

	void UpdateOrientation()
	{
		var isHorizontal = _layout.Position is MauiButton.ButtonContentLayout.ImagePosition.Left or MauiButton.ButtonContentLayout.ImagePosition.Right;
		Orientation = isHorizontal ? Orientation.Horizontal : Orientation.Vertical;

		var imageFirst = _layout.Position is MauiButton.ButtonContentLayout.ImagePosition.Left or MauiButton.ButtonContentLayout.ImagePosition.Top;
		Children.Clear();

		if (imageFirst)
		{
			Children.Add(_image);
			Children.Add(_textBlock);
		}
		else
		{
			Children.Add(_textBlock);
			Children.Add(_image);
		}
	}

	void UpdateSpacing()
	{
		Spacing = _image.IsVisible && _textBlock.IsVisible ? Math.Max(0, _layout.Spacing) : 0;
	}
}
