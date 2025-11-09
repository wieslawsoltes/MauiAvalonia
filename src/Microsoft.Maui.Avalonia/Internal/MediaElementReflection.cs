using System;
using System.Reflection;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core;

namespace Microsoft.Maui.Avalonia.Internal;

static class MediaElementReflection
{
	static readonly PropertyInfo? DurationProperty = typeof(MediaElement).GetProperty(nameof(IMediaElement.Duration));
	static readonly PropertyInfo? PositionProperty = typeof(MediaElement).GetProperty(nameof(IMediaElement.Position));
	static readonly PropertyInfo? WidthProperty = typeof(MediaElement).GetProperty(nameof(IMediaElement.MediaWidth));
	static readonly PropertyInfo? HeightProperty = typeof(MediaElement).GetProperty(nameof(IMediaElement.MediaHeight));
	static readonly MethodInfo? SeekCompletedMethod = typeof(MediaElement).GetMethod(
		"CommunityToolkit.Maui.Core.IMediaElement.SeekCompleted",
		BindingFlags.Instance | BindingFlags.NonPublic);
	static readonly MethodInfo? MediaOpenedMethod = typeof(MediaElement).GetMethod("OnMediaOpened", BindingFlags.Instance | BindingFlags.NonPublic);
	static readonly MethodInfo? MediaEndedMethod = typeof(MediaElement).GetMethod("OnMediaEnded", BindingFlags.Instance | BindingFlags.NonPublic);
	static readonly MethodInfo? MediaFailedMethod = typeof(MediaElement).GetMethod("OnMediaFailed", BindingFlags.Instance | BindingFlags.NonPublic);
	static readonly Type? MediaFailedEventArgsType = typeof(MediaElement).Assembly.GetType("CommunityToolkit.Maui.Core.MediaFailedEventArgs");

	public static void SetDuration(MediaElement element, TimeSpan value) => DurationProperty?.SetValue(element, value);

	public static void SetPosition(MediaElement element, TimeSpan value) => PositionProperty?.SetValue(element, value);

	public static void SetDimensions(MediaElement element, int width, int height)
	{
		WidthProperty?.SetValue(element, width);
		HeightProperty?.SetValue(element, height);
	}

	public static void NotifySeekCompleted(MediaElement element) =>
		SeekCompletedMethod?.Invoke(element, Array.Empty<object>());

	public static void RaiseMediaOpened(MediaElement element) =>
		MediaOpenedMethod?.Invoke(element, Array.Empty<object>());

	public static void RaiseMediaEnded(MediaElement element) =>
		MediaEndedMethod?.Invoke(element, Array.Empty<object>());

	public static void RaiseMediaFailed(MediaElement element, string message)
	{
		if (MediaFailedMethod is null || MediaFailedEventArgsType is null)
			return;

		var args = Activator.CreateInstance(MediaFailedEventArgsType, message);
		if (args is not null)
			MediaFailedMethod.Invoke(element, new[] { args });
	}
}
