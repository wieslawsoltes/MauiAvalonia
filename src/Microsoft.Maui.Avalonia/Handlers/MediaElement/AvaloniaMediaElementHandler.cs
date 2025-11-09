using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;
using Microsoft.Maui.Avalonia.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Devices;
using LibVlcMedia = LibVLCSharp.Shared.Media;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaSlider = Avalonia.Controls.Slider;
using AvaloniaRowDefinition = Avalonia.Controls.RowDefinition;
using AvaloniaGridLength = Avalonia.Controls.GridLength;

namespace Microsoft.Maui.Avalonia.Handlers;

public class AvaloniaMediaElementHandler : AvaloniaViewHandler<MediaElement, AvaloniaGrid>
{
	static readonly PropertyMapper<MediaElement, AvaloniaMediaElementHandler> Mapper = new(ViewMapper)
	{
		[nameof(MediaElement.Source)] = MapSource,
		[nameof(MediaElement.Aspect)] = MapAspect,
		[nameof(MediaElement.Volume)] = MapVolume,
		[nameof(MediaElement.ShouldMute)] = MapMute,
		[nameof(MediaElement.ShouldLoopPlayback)] = MapLoop,
		[nameof(MediaElement.Speed)] = MapSpeed,
		[nameof(MediaElement.ShouldShowPlaybackControls)] = MapControls,
		[nameof(MediaElement.ShouldKeepScreenOn)] = MapKeepScreenOn
	};

	const string PlayRequestedCommand = "PlayRequested";
	const string PauseRequestedCommand = "PauseRequested";
	const string StopRequestedCommand = "StopRequested";
	const string SeekRequestedCommand = "SeekRequested";

	static readonly CommandMapper<MediaElement, AvaloniaMediaElementHandler> CommandMapper = new(ViewCommandMapper)
	{
		[PlayRequestedCommand] = MapPlay,
		[PauseRequestedCommand] = MapPause,
		[StopRequestedCommand] = MapStop,
		[SeekRequestedCommand] = MapSeek
	};

static bool _initialized;
static LibVLC? _sharedLibVlc;

LibVLC? _libVlc;
MediaPlayer? _mediaPlayer;
VideoView? _videoView;
AvaloniaGrid? _root;
StackPanel? _transportPanel;
	AvaloniaButton? _playPauseButton;
	AvaloniaSlider? _positionSlider;
DispatcherTimer? _positionTimer;
bool _isScrubbing;
	IDeviceDisplay? _deviceDisplay;
	bool _keepScreenRequestActive;

	public AvaloniaMediaElementHandler()
		: base(Mapper, CommandMapper)
	{
	}

	protected override AvaloniaGrid CreatePlatformView()
	{
		EnsureInitialized();
		_videoView = new VideoView();
		_mediaPlayer = new MediaPlayer(_libVlc!);
		_videoView.MediaPlayer = _mediaPlayer;

		_mediaPlayer.EndReached += OnEndReached;
		_mediaPlayer.EncounteredError += OnError;
		_mediaPlayer.Playing += OnPlaying;
		_mediaPlayer.Paused += OnPaused;

		_positionTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(200)
		};
		_positionTimer.Tick += (_, _) => UpdatePosition();

		_root = new AvaloniaGrid
		{
			RowDefinitions =
			{
				new AvaloniaRowDefinition { Height = AvaloniaGridLength.Star },
				new AvaloniaRowDefinition { Height = AvaloniaGridLength.Auto }
			}
		};

		AvaloniaGrid.SetRow(_videoView, 0);
		_root.Children.Add(_videoView);

		_transportPanel = CreateTransportControls();
		AvaloniaGrid.SetRow(_transportPanel, 1);
		_root.Children.Add(_transportPanel);

		return _root;
	}

	protected override void DisconnectHandler(AvaloniaGrid platformView)
	{
		UpdateKeepScreenState(false);
		base.DisconnectHandler(platformView);
		if (_positionTimer is not null)
		{
			_positionTimer.Stop();
			_positionTimer = null;
		}

		if (_mediaPlayer is not null)
		{
			_mediaPlayer.EndReached -= OnEndReached;
			_mediaPlayer.EncounteredError -= OnError;
			_mediaPlayer.Playing -= OnPlaying;
			_mediaPlayer.Paused -= OnPaused;
			_mediaPlayer.Dispose();
			_mediaPlayer = null;
		}

		_videoView = null;
		_root = null;
		_transportPanel = null;
	}

	static void MapSource(AvaloniaMediaElementHandler handler, MediaElement element) => handler.LoadSource();

	static void MapAspect(AvaloniaMediaElementHandler handler, MediaElement element) =>
		handler.UpdateAspect();

	static void MapVolume(AvaloniaMediaElementHandler handler, MediaElement element)
	{
		if (handler._mediaPlayer is null)
			return;

		handler._mediaPlayer.Volume = (int)(element.Volume * 100);
	}

	static void MapMute(AvaloniaMediaElementHandler handler, MediaElement element)
	{
		if (handler._mediaPlayer is null)
			return;

		handler._mediaPlayer.Mute = element.ShouldMute;
	}

	static void MapLoop(AvaloniaMediaElementHandler handler, MediaElement element)
	{
		// Handled when EndReached fires.
	}

	static void MapSpeed(AvaloniaMediaElementHandler handler, MediaElement element)
	{
		if (handler._mediaPlayer is null)
			return;

		handler._mediaPlayer.SetRate((float)element.Speed);
	}

	static void MapControls(AvaloniaMediaElementHandler handler, MediaElement element) =>
		handler.UpdateTransportVisibility();

	static void MapKeepScreenOn(AvaloniaMediaElementHandler handler, MediaElement element) =>
		handler.UpdateKeepScreenState(element.ShouldKeepScreenOn && handler._mediaPlayer?.IsPlaying == true);

	static void MapPlay(AvaloniaMediaElementHandler handler, MediaElement element, object? args)
	{
		handler._mediaPlayer?.Play();
		handler.UpdatePlayPauseGlyph();
		handler.UpdateKeepScreenState(element.ShouldKeepScreenOn);
	}

	static void MapPause(AvaloniaMediaElementHandler handler, MediaElement element, object? args)
	{
		handler._mediaPlayer?.Pause();
		handler.UpdatePlayPauseGlyph();
		handler.UpdateKeepScreenState(false);
	}

	static void MapStop(AvaloniaMediaElementHandler handler, MediaElement element, object? args)
	{
		if (handler._mediaPlayer is null)
			return;

		handler._mediaPlayer.Stop();
		handler._mediaPlayer.Time = 0;
		MediaElementReflection.SetPosition(element, TimeSpan.Zero);
		handler.UpdatePlayPauseGlyph();
		handler.UpdateKeepScreenState(false);
	}

	static void MapSeek(AvaloniaMediaElementHandler handler, MediaElement element, object? args)
	{
		if (handler._mediaPlayer is null || args is null)
			return;

		var position = GetRequestedPosition(args);
		if (position is null)
			return;

		handler._mediaPlayer.Time = (long)position.Value.TotalMilliseconds;
		MediaElementReflection.SetPosition(element, position.Value);
		MediaElementReflection.NotifySeekCompleted(element);
	}

	void LoadSource()
	{
		if (_mediaPlayer is null || VirtualView is null)
			return;

		if (VirtualView.Source is null)
		{
			_mediaPlayer.Stop();
			return;
		}

		using var media = CreateMedia(VirtualView.Source);
		if (media is null)
		{
			if (VirtualView is not null)
				MediaElementReflection.RaiseMediaFailed(VirtualView, "Unsupported media source");
			return;
		}

		_mediaPlayer.Media = media;
		if (VirtualView.ShouldAutoPlay)
			_mediaPlayer.Play();
		UpdatePlayPauseGlyph();
	}

	LibVlcMedia? CreateMedia(MediaSource source)
	{
		if (_libVlc is null)
			return null;

		return source switch
		{
			FileMediaSource file when !string.IsNullOrEmpty(file.Path) => new LibVlcMedia(_libVlc, file.Path, FromType.FromPath),
			UriMediaSource uri when uri.Uri is not null => new LibVlcMedia(_libVlc, uri.Uri.ToString(), FromType.FromLocation),
			_ => null
		};
	}

	void UpdatePosition()
	{
		if (_mediaPlayer is null || VirtualView is null)
			return;

		var milliseconds = _mediaPlayer.Time;
		if (milliseconds >= 0)
		{
			var position = TimeSpan.FromMilliseconds(milliseconds);
			MediaElementReflection.SetPosition(VirtualView, position);
			UpdateSlider(position);
		}
	}

	void OnPlaying(object? sender, EventArgs e)
	{
		if (VirtualView is null || _mediaPlayer is null)
			return;

		_positionTimer?.Start();
		MediaElementReflection.SetDuration(VirtualView, TimeSpan.FromMilliseconds(_mediaPlayer.Length));
		UpdateDimensions();
		MediaElementReflection.RaiseMediaOpened(VirtualView);
		UpdatePlayPauseGlyph();
		UpdateKeepScreenState(VirtualView.ShouldKeepScreenOn);
	}

	void OnPaused(object? sender, EventArgs e)
	{
		UpdatePlayPauseGlyph();
		UpdateKeepScreenState(false);
	}

	void OnEndReached(object? sender, EventArgs e)
	{
		if (VirtualView is null || _mediaPlayer is null)
			return;

		_positionTimer?.Stop();
		if (VirtualView.ShouldLoopPlayback)
		{
			_mediaPlayer.Time = 0;
			_mediaPlayer.Play();
			UpdateKeepScreenState(VirtualView.ShouldKeepScreenOn);
		}
		else
		{
			MediaElementReflection.RaiseMediaEnded(VirtualView);
			UpdateKeepScreenState(false);
		}

		UpdatePlayPauseGlyph();
	}

	void OnError(object? sender, EventArgs e)
	{
		if (VirtualView is null)
			return;

		_positionTimer?.Stop();
		MediaElementReflection.RaiseMediaFailed(VirtualView, "Playback failed");
		UpdateKeepScreenState(false);
	}

	void UpdateDimensions()
	{
		if (VirtualView is null || _mediaPlayer?.Media is null)
			return;

		foreach (var track in _mediaPlayer.Media.Tracks)
		{
			if (track.TrackType == TrackType.Video)
			{
					MediaElementReflection.SetDimensions(VirtualView, (int)track.Data.Video.Width, (int)track.Data.Video.Height);
				break;
			}
		}
	}

	void UpdateTransportVisibility()
	{
		if (_transportPanel is null)
			return;

		_transportPanel.IsVisible = VirtualView?.ShouldShowPlaybackControls ?? false;
	}

	void UpdateAspect()
	{
		// VideoView currently does not expose stretch/alignment hooks on Avalonia.
		// Leave as-is so the containing layout controls sizing.
	}

	void UpdatePlayPauseGlyph()
	{
		if (_playPauseButton is null || _mediaPlayer is null)
			return;

		_playPauseButton.Content = _mediaPlayer.IsPlaying ? "❚❚" : "▶";
	}

	void UpdateSlider(TimeSpan position)
	{
		if (_positionSlider is null || _mediaPlayer is null || _isScrubbing)
			return;

		var duration = _mediaPlayer.Length;
		if (duration <= 0)
			return;

		var ratio = position.TotalMilliseconds / duration;
		_positionSlider.Value = Math.Clamp(ratio, 0, 1);
	}

	void TogglePlayback()
	{
		if (_mediaPlayer is null)
			return;

		if (_mediaPlayer.IsPlaying)
			MapPause(this, VirtualView!, null);
		else
			MapPlay(this, VirtualView!, null);
	}

	StackPanel CreateTransportControls()
	{
		_playPauseButton = new AvaloniaButton
		{
			Content = "▶",
			Margin = new global::Avalonia.Thickness(8, 4)
		};
		_playPauseButton.Click += (_, _) => TogglePlayback();

		_positionSlider = new AvaloniaSlider
		{
			Minimum = 0,
			Maximum = 1,
			Width = 180,
			Margin = new global::Avalonia.Thickness(4)
		};
		_positionSlider.PointerPressed += (_, _) => _isScrubbing = true;
		_positionSlider.PointerReleased += (_, _) =>
		{
			if (_mediaPlayer?.Length is > 0 && _positionSlider is not null)
			{
				var target = TimeSpan.FromMilliseconds(_positionSlider.Value * _mediaPlayer.Length);
				_mediaPlayer.Time = (long)target.TotalMilliseconds;
				MediaElementReflection.SetPosition(VirtualView!, target);
			}

			_isScrubbing = false;
		};

		var panel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 8,
			HorizontalAlignment = AvaloniaHorizontalAlignment.Center,
			Margin = new global::Avalonia.Thickness(12)
		};
		panel.Children.Add(_playPauseButton);
		panel.Children.Add(_positionSlider);
		return panel;
	}

	void EnsureInitialized()
	{
		if (!_initialized)
		{
			Core.Initialize();
			_sharedLibVlc = new LibVLC();
			_initialized = true;
		}

		_libVlc ??= _sharedLibVlc;
	}

	void UpdateKeepScreenState(bool request)
	{
		if (VirtualView?.ShouldKeepScreenOn is not true)
			request = false;

		var display = _deviceDisplay ??= MauiContext?.Services?.GetService<IDeviceDisplay>();
		if (display is null)
			return;

		if (request && !_keepScreenRequestActive)
		{
			display.KeepScreenOn = true;
			_keepScreenRequestActive = true;
		}
		else if (!request && _keepScreenRequestActive)
		{
			display.KeepScreenOn = false;
			_keepScreenRequestActive = false;
		}
	}

	static TimeSpan? GetRequestedPosition(object args)
	{
		var property = args.GetType().GetProperty("RequestedPosition");
		if (property?.GetValue(args) is TimeSpan timeSpan)
			return timeSpan;

		return null;
	}
}
