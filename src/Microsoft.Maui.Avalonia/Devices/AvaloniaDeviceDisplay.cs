using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Maui.Devices;
using Application = Avalonia.Application;

namespace Microsoft.Maui.Avalonia.Devices;

internal sealed class AvaloniaDeviceDisplay : IDeviceDisplay, IDisposable
{
	readonly object _lock = new();
	DisplayInfo _currentInfo;
	TopLevel? _currentTopLevel;
	bool _keepScreenOn;

	public AvaloniaDeviceDisplay()
	{
		_currentInfo = CreateDisplayInfo();
		AttachTopLevel();
	}

	public bool KeepScreenOn
	{
		get => _keepScreenOn;
		set => _keepScreenOn = value;
	}

	public DisplayInfo MainDisplayInfo => _currentInfo;

	public event EventHandler<DisplayInfoChangedEventArgs>? MainDisplayInfoChanged;

	public void Dispose()
	{
		if (_currentTopLevel is not null)
			_currentTopLevel.PropertyChanged -= OnTopLevelPropertyChanged;

	}

	void OnTopLevelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property == TopLevel.ClientSizeProperty ||
			e.Property == TopLevel.FrameSizeProperty)
		{
			UpdateDisplayInfo();
		}
	}

	void UpdateDisplayInfo()
	{
		var info = CreateDisplayInfo();
		lock (_lock)
		{
			if (DisplayInfoEquals(info, _currentInfo))
				return;

			_currentInfo = info;
		}

		MainDisplayInfoChanged?.Invoke(this, new DisplayInfoChangedEventArgs(info));
	}

	DisplayInfo CreateDisplayInfo()
	{
		AttachTopLevel();
		var topLevel = GetActiveTopLevel();
		var size = topLevel?.FrameSize ?? topLevel?.ClientSize;
		if (size is null || size.Value.Width <= 0 || size.Value.Height <= 0)
			return new DisplayInfo(1920, 1080, 1, DisplayOrientation.Landscape, DisplayRotation.Rotation0);

		var scaling = topLevel?.RenderScaling ?? 1;
		var width = Math.Max(1, size.Value.Width * scaling);
		var height = Math.Max(1, size.Value.Height * scaling);
		var density = Math.Max(1, scaling);
		var orientation = width >= height ? DisplayOrientation.Landscape : DisplayOrientation.Portrait;

		return new DisplayInfo(width, height, density, orientation, DisplayRotation.Rotation0);
	}

	static bool DisplayInfoEquals(DisplayInfo left, DisplayInfo right) =>
		left.Width == right.Width &&
		left.Height == right.Height &&
		Math.Abs(left.Density - right.Density) < double.Epsilon &&
		left.Orientation == right.Orientation &&
		left.Rotation == right.Rotation;

	static TopLevel? GetActiveTopLevel()
	{
		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
		{
			return desktopLifetime.Windows.FirstOrDefault(w => w.IsActive) ??
				desktopLifetime.Windows.FirstOrDefault() ??
				desktopLifetime.MainWindow;
		}

		if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
			return TopLevel.GetTopLevel(singleViewLifetime.MainView);

		return null;
	}

	void AttachTopLevel()
	{
		var topLevel = GetActiveTopLevel();
		if (ReferenceEquals(topLevel, _currentTopLevel))
			return;

		if (_currentTopLevel is not null)
			_currentTopLevel.PropertyChanged -= OnTopLevelPropertyChanged;

		_currentTopLevel = topLevel;

		if (_currentTopLevel is not null)
			_currentTopLevel.PropertyChanged += OnTopLevelPropertyChanged;
	}
}
