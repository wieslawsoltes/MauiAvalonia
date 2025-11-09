using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace Microsoft.Maui.Avalonia.Devices;

internal sealed class AvaloniaHapticFeedback : IHapticFeedback
{
	public bool IsSupported => OperatingSystem.IsWindows();

	public void Perform(HapticFeedbackType type)
	{
		if (!IsSupported)
			throw new FeatureNotSupportedException();

		Debug.WriteLine($"[MauiAvalonia] Simulated haptic feedback: {type}");
	}

	public static void TryRegisterAsDefault(IHapticFeedback implementation)
	{
		var method = typeof(HapticFeedback).GetMethod("SetDefault", BindingFlags.Static | BindingFlags.NonPublic);
		method?.Invoke(null, new object?[] { implementation });
	}
}
