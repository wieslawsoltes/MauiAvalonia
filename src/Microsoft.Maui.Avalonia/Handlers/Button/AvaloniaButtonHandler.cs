using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Fonts;
using Microsoft.Maui.Avalonia.Graphics;
using Microsoft.Maui.Avalonia.Internal;
using Microsoft.Maui.Avalonia.Platform;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Controls;
using AvaloniaButton = Avalonia.Controls.Button;

namespace Microsoft.Maui.Avalonia.Handlers;

public class AvaloniaButtonHandler : AvaloniaViewHandler<IButton, AvaloniaButton>, IButtonHandler
{
	static readonly IPropertyMapper<IImage, AvaloniaButtonHandler> ImageMapper = new PropertyMapper<IImage, AvaloniaButtonHandler>()
	{
		[nameof(IImage.Source)] = MapImageSource
	};

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

	AvaloniaButtonContentPresenter? _contentPresenter;
	CancellationTokenSource? _imageLoadingCts;
	Bitmap? _currentImage;
	PropertyChangedEventHandler? _buttonPropertyChangedHandler;
	ImageSourcePartLoader? _imageSourcePartLoader;

	public AvaloniaButtonHandler()
		: base(Mapper, CommandMapper)
	{
	}

	protected override AvaloniaButton CreatePlatformView()
	{
		_contentPresenter = new AvaloniaButtonContentPresenter();

		return new AvaloniaButton
		{
			HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
			VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
			Content = _contentPresenter
		};
	}

	protected override void ConnectHandler(AvaloniaButton platformView)
	{
		base.ConnectHandler(platformView);
		platformView.Click += OnClick;
		platformView.PointerPressed += OnPointerPressed;
		platformView.PointerReleased += OnPointerReleased;
		SubscribeToButtonPropertyChanges();
	}

	protected override void DisconnectHandler(AvaloniaButton platformView)
	{
		base.DisconnectHandler(platformView);
		platformView.Click -= OnClick;
		platformView.PointerPressed -= OnPointerPressed;
		platformView.PointerReleased -= OnPointerReleased;
		CancelImageLoading();
		ClearImage();
		_contentPresenter?.Reset();
		UnsubscribeFromButtonPropertyChanges();
	}

	static void MapText(AvaloniaButtonHandler handler, ITextButton textButton)
	{
		if (handler.PlatformView is null)
			return;

		handler.GetContentPresenter().UpdateText(textButton.Text ?? string.Empty);
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

		handler.GetContentPresenter().UpdateCharacterSpacing(textButton.CharacterSpacing);
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

	static void MapImageSource(AvaloniaButtonHandler handler, IImage image) =>
		_ = handler.UpdateImageSourceAsync();

	void OnClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
		VirtualView?.Clicked();

	void OnPointerPressed(object? sender, PointerPressedEventArgs e) =>
		VirtualView?.Pressed();

	void OnPointerReleased(object? sender, PointerReleasedEventArgs e) =>
		VirtualView?.Released();

	public ImageSourcePartLoader ImageSourceLoader =>
		_imageSourcePartLoader ??= new ImageSourcePartLoader(new ButtonImageSourcePartSetter(this));

	AvaloniaButtonContentPresenter GetContentPresenter()
	{
		if (_contentPresenter is null)
		{
			_contentPresenter = new AvaloniaButtonContentPresenter();
			if (PlatformView is not null)
				PlatformView.Content = _contentPresenter;
		}

		return _contentPresenter;
	}

	async Task UpdateImageSourceAsync()
	{
		CancelImageLoading();

		if (MauiContext is null || PlatformView is null || VirtualView is not IImage image || image.Source is null)
		{
			await SetButtonImageAsync(null).ConfigureAwait(false);
			return;
		}

		_imageLoadingCts = new CancellationTokenSource();
		var token = _imageLoadingCts.Token;

		try
		{
			var bitmap = await AvaloniaImageSourceLoader.LoadAsync(image.Source, MauiContext.Services, token).ConfigureAwait(false);
			if (token.IsCancellationRequested)
			{
				bitmap?.Dispose();
				return;
			}

			await SetButtonImageAsync(bitmap).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// no-op
		}
		catch
		{
			await SetButtonImageAsync(null).ConfigureAwait(false);
		}
	}

	async Task SetButtonImageAsync(Bitmap? bitmap)
	{
		if (PlatformView is null)
		{
			bitmap?.Dispose();
			return;
		}

		await AvaloniaUiDispatcher.UIThread.InvokeAsync(() =>
		{
			var presenter = GetContentPresenter();
			var previous = _currentImage;
			_currentImage = bitmap;
			presenter.UpdateImage(bitmap);
			previous?.Dispose();
		});
	}

	void CancelImageLoading()
	{
		if (_imageLoadingCts is null)
			return;

		try
		{
			_imageLoadingCts.Cancel();
		}
		catch (ObjectDisposedException)
		{
		}
		finally
		{
			_imageLoadingCts.Dispose();
			_imageLoadingCts = null;
		}
	}

	void ClearImage()
	{
		var bitmap = _currentImage;
		_currentImage = null;
		bitmap?.Dispose();
	}

	void SubscribeToButtonPropertyChanges()
	{
		UnsubscribeFromButtonPropertyChanges();

		if (VirtualView is Microsoft.Maui.Controls.Button button)
		{
			_buttonPropertyChangedHandler = (sender, args) =>
			{
				if (args.PropertyName == nameof(Microsoft.Maui.Controls.Button.ContentLayout))
					GetContentPresenter().UpdateLayout(button.ContentLayout);
			};
			button.PropertyChanged += _buttonPropertyChangedHandler;
			GetContentPresenter().UpdateLayout(button.ContentLayout);
		}
	}

	void UnsubscribeFromButtonPropertyChanges()
	{
		if (VirtualView is Microsoft.Maui.Controls.Button button && _buttonPropertyChangedHandler is not null)
			button.PropertyChanged -= _buttonPropertyChangedHandler;

		_buttonPropertyChangedHandler = null;
	}

	sealed class ButtonImageSourcePartSetter : IImageSourcePartSetter
	{
		readonly AvaloniaButtonHandler _handler;

		public ButtonImageSourcePartSetter(AvaloniaButtonHandler handler) =>
			_handler = handler;

		public IElementHandler? Handler => _handler;

		public IImageSourcePart? ImageSourcePart => _handler.VirtualView as IImage;

		public void SetImageSource(object? platformImage)
		{
			// Avalonia button handler manages image loading itself.
		}
	}
}
