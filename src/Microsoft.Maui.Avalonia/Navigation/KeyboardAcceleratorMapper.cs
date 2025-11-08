using System;
using System.Collections.Generic;
using Avalonia.Input;
using Microsoft.Maui;

namespace Microsoft.Maui.Avalonia.Navigation;

internal static class KeyboardAcceleratorMapper
{
	public static KeyGesture? FromAccelerators(IReadOnlyList<IKeyboardAccelerator>? accelerators)
	{
		if (accelerators is null || accelerators.Count == 0)
			return null;

		foreach (var accelerator in accelerators)
		{
			if (accelerator is null)
				continue;

			var gesture = BuildGestureString(accelerator);
			if (string.IsNullOrWhiteSpace(gesture))
				continue;

			try
			{
				return KeyGesture.Parse(gesture);
			}
			catch
			{
			}
		}

		return null;
	}

	public static string? BuildGestureString(IKeyboardAccelerator accelerator)
	{
		var key = accelerator.Key?.Trim();
		if (string.IsNullOrWhiteSpace(key))
			return null;

		var parts = new List<string>();
		var modifiers = accelerator.Modifiers;

		if (modifiers.HasFlag(KeyboardAcceleratorModifiers.Ctrl))
			parts.Add("Ctrl");
		if (modifiers.HasFlag(KeyboardAcceleratorModifiers.Shift))
			parts.Add("Shift");
		if (modifiers.HasFlag(KeyboardAcceleratorModifiers.Alt))
			parts.Add("Alt");
		if (modifiers.HasFlag(KeyboardAcceleratorModifiers.Cmd))
			parts.Add("Cmd");
		if (modifiers.HasFlag(KeyboardAcceleratorModifiers.Windows))
			parts.Add("Win");

		parts.Add(key);
		return string.Join("+", parts);
	}
}
