using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Internals;
using Microsoft.Maui.Graphics;
using ButtonsMask = Microsoft.Maui.Controls.ButtonsMask;

namespace Microsoft.Maui.Avalonia.Input;

internal static class GestureExtensions
{
	static readonly Action<PointerGestureRecognizer, View, Func<IElement?, Point?>?, PlatformPointerEventArgs?, ButtonsMask> PointerEnteredProxy =
		CreatePointerDelegate("SendPointerEntered");
	static readonly Action<PointerGestureRecognizer, View, Func<IElement?, Point?>?, PlatformPointerEventArgs?, ButtonsMask> PointerExitedProxy =
		CreatePointerDelegate("SendPointerExited");
	static readonly Action<PointerGestureRecognizer, View, Func<IElement?, Point?>?, PlatformPointerEventArgs?, ButtonsMask> PointerMovedProxy =
		CreatePointerDelegate("SendPointerMoved");
	static readonly Action<PointerGestureRecognizer, View, Func<IElement?, Point?>?, PlatformPointerEventArgs?, ButtonsMask> PointerPressedProxy =
		CreatePointerDelegate("SendPointerPressed");
	static readonly Action<PointerGestureRecognizer, View, Func<IElement?, Point?>?, PlatformPointerEventArgs?, ButtonsMask> PointerReleasedProxy =
		CreatePointerDelegate("SendPointerReleased");
	static readonly Action<TapGestureRecognizer, View, Func<IElement?, Point?>?> TapProxy =
		CreateTapDelegate();

	static readonly Func<DragGestureRecognizer, View, Func<IElement?, Point?>?, PlatformDragStartingEventArgs?, DragStartingEventArgs> DragStartingProxy =
		CreateDragStartingDelegate();
	static readonly Action<DragGestureRecognizer, DropCompletedEventArgs> DropCompletedProxy =
		CreateDropCompletedDelegate();
	static readonly Action<DropGestureRecognizer, DragEventArgs> DragLeaveProxy =
		CreateDragLeaveDelegate();
	static readonly Func<DropGestureRecognizer, DropEventArgs, Task> DropProxy =
		CreateDropDelegate();

	public static IEnumerable<T> GetGesturesFor<T>(this IEnumerable<IGestureRecognizer> gestures)
		where T : IGestureRecognizer
	{
		foreach (var recognizer in gestures)
		{
			if (recognizer is T match)
				yield return match;

			if (recognizer is IGestureController controller)
			{
				foreach (var composite in controller.CompositeGestureRecognizers.OfType<T>())
					yield return composite;
			}
		}
	}

	public static void SendPointerEntered(this PointerGestureRecognizer recognizer, View sender, Func<IElement?, Point?>? getPosition, PlatformPointerEventArgs? platformArgs = null, ButtonsMask button = ButtonsMask.Primary) =>
		PointerEnteredProxy(recognizer, sender, getPosition, platformArgs, button);

	public static void SendPointerExited(this PointerGestureRecognizer recognizer, View sender, Func<IElement?, Point?>? getPosition, PlatformPointerEventArgs? platformArgs = null, ButtonsMask button = ButtonsMask.Primary) =>
		PointerExitedProxy(recognizer, sender, getPosition, platformArgs, button);

	public static void SendPointerMoved(this PointerGestureRecognizer recognizer, View sender, Func<IElement?, Point?>? getPosition, PlatformPointerEventArgs? platformArgs = null, ButtonsMask button = ButtonsMask.Primary) =>
		PointerMovedProxy(recognizer, sender, getPosition, platformArgs, button);

	public static void SendPointerPressed(this PointerGestureRecognizer recognizer, View sender, Func<IElement?, Point?>? getPosition, PlatformPointerEventArgs? platformArgs = null, ButtonsMask button = ButtonsMask.Primary) =>
		PointerPressedProxy(recognizer, sender, getPosition, platformArgs, button);

	public static void SendPointerReleased(this PointerGestureRecognizer recognizer, View sender, Func<IElement?, Point?>? getPosition, PlatformPointerEventArgs? platformArgs = null, ButtonsMask button = ButtonsMask.Primary) =>
		PointerReleasedProxy(recognizer, sender, getPosition, platformArgs, button);

	public static void SendTapped(this TapGestureRecognizer recognizer, View sender, Func<IElement?, Point?>? getPosition = null) =>
		TapProxy(recognizer, sender, getPosition);

	public static DragStartingEventArgs SendDragStarting(this DragGestureRecognizer recognizer, View element, Func<IElement?, Point?>? getPosition = null, PlatformDragStartingEventArgs? platformArgs = null) =>
		DragStartingProxy(recognizer, element, getPosition, platformArgs);

	public static void SendDropCompleted(this DragGestureRecognizer recognizer, DropCompletedEventArgs args) =>
		DropCompletedProxy(recognizer, args);

	public static void SendDragLeave(this DropGestureRecognizer recognizer, DragEventArgs args) =>
		DragLeaveProxy(recognizer, args);

	public static Task SendDrop(this DropGestureRecognizer recognizer, DropEventArgs args) =>
		DropProxy(recognizer, args);

	static Action<PointerGestureRecognizer, View, Func<IElement?, Point?>?, PlatformPointerEventArgs?, ButtonsMask> CreatePointerDelegate(string name)
	{
		var method = typeof(PointerGestureRecognizer).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMemberException(typeof(PointerGestureRecognizer).FullName, name);
		return (Action<PointerGestureRecognizer, View, Func<IElement?, Point?>?, PlatformPointerEventArgs?, ButtonsMask>)
			Delegate.CreateDelegate(typeof(Action<PointerGestureRecognizer, View, Func<IElement?, Point?>?, PlatformPointerEventArgs?, ButtonsMask>), method);
	}

	static Func<DragGestureRecognizer, View, Func<IElement?, Point?>?, PlatformDragStartingEventArgs?, DragStartingEventArgs> CreateDragStartingDelegate()
	{
		const string name = "SendDragStarting";
		var method = typeof(DragGestureRecognizer).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMemberException(typeof(DragGestureRecognizer).FullName, name);
		return (Func<DragGestureRecognizer, View, Func<IElement?, Point?>?, PlatformDragStartingEventArgs?, DragStartingEventArgs>)
			Delegate.CreateDelegate(typeof(Func<DragGestureRecognizer, View, Func<IElement?, Point?>?, PlatformDragStartingEventArgs?, DragStartingEventArgs>), method);
	}

	static Action<DragGestureRecognizer, DropCompletedEventArgs> CreateDropCompletedDelegate()
	{
		const string name = "SendDropCompleted";
		var method = typeof(DragGestureRecognizer).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMemberException(typeof(DragGestureRecognizer).FullName, name);
		return (Action<DragGestureRecognizer, DropCompletedEventArgs>)
			Delegate.CreateDelegate(typeof(Action<DragGestureRecognizer, DropCompletedEventArgs>), method);
	}

	static Action<DropGestureRecognizer, DragEventArgs> CreateDragLeaveDelegate()
	{
		const string name = "SendDragLeave";
		var method = typeof(DropGestureRecognizer).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMemberException(typeof(DropGestureRecognizer).FullName, name);
		return (Action<DropGestureRecognizer, DragEventArgs>)
			Delegate.CreateDelegate(typeof(Action<DropGestureRecognizer, DragEventArgs>), method);
	}

	static Func<DropGestureRecognizer, DropEventArgs, Task> CreateDropDelegate()
	{
		const string name = "SendDrop";
		var method = typeof(DropGestureRecognizer).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMemberException(typeof(DropGestureRecognizer).FullName, name);
		return (Func<DropGestureRecognizer, DropEventArgs, Task>)
			Delegate.CreateDelegate(typeof(Func<DropGestureRecognizer, DropEventArgs, Task>), method);
	}

	static Action<TapGestureRecognizer, View, Func<IElement?, Point?>?> CreateTapDelegate()
	{
		const string name = "SendTapped";
		var method = typeof(TapGestureRecognizer).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMemberException(typeof(TapGestureRecognizer).FullName, name);
		return (Action<TapGestureRecognizer, View, Func<IElement?, Point?>?>)
			Delegate.CreateDelegate(typeof(Action<TapGestureRecognizer, View, Func<IElement?, Point?>?>), method);
	}
}
