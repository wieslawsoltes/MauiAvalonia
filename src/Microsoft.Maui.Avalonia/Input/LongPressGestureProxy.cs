using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Microsoft.Maui.Avalonia.Input;

static class LongPressGestureProxy
{
	static readonly Type? GestureType = Type.GetType("Microsoft.Maui.Controls.LongPressGestureRecognizer, Microsoft.Maui.Controls");
	static readonly PropertyInfo? DurationProperty = GestureType?.GetProperty("Duration") ?? GestureType?.GetProperty("LongPressDuration");
	static readonly PropertyInfo? CommandProperty = GestureType?.GetProperty("Command");
	static readonly PropertyInfo? CommandParameterProperty = GestureType?.GetProperty("CommandParameter");
	static readonly MethodInfo? SendPressedMethod = GestureType?.GetMethod("SendLongPressed", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

	public static bool IsSupported => GestureType is not null;

	public static IReadOnlyList<object> GetGestures(Microsoft.Maui.Controls.View view)
	{
		if (!IsSupported || view?.GestureRecognizers is not IEnumerable<IGestureRecognizer> gestures)
			return Array.Empty<object>();

		return gestures.Where(g => GestureType!.IsInstanceOfType(g)).Cast<object>().ToArray();
	}

	public static TimeSpan GetDuration(object gesture)
	{
		if (gesture is null)
			return TimeSpan.FromMilliseconds(500);

		try
		{
			if (DurationProperty is not null)
			{
				var raw = DurationProperty.GetValue(gesture);
				return raw switch
				{
					TimeSpan timeSpan => timeSpan,
					double milliseconds => TimeSpan.FromMilliseconds(milliseconds),
					_ => TimeSpan.FromMilliseconds(500)
				};
			}
		}
		catch
		{
		}

		return TimeSpan.FromMilliseconds(500);
	}

	public static void SendLongPressed(Microsoft.Maui.Controls.View view, object gesture, Func<IElement?, Point?> positionProvider)
	{
		if (gesture is null)
			return;

		try
		{
			if (SendPressedMethod is not null)
			{
				var parameters = SendPressedMethod.GetParameters();
				if (parameters.Length == 2)
				{
					SendPressedMethod.Invoke(gesture, new object?[] { view, positionProvider });
					return;
				}

				if (parameters.Length == 1)
				{
					SendPressedMethod.Invoke(gesture, new object?[] { view });
					return;
				}

				if (parameters.Length == 0)
				{
					SendPressedMethod.Invoke(gesture, Array.Empty<object>());
					return;
				}
			}

			var command = CommandProperty?.GetValue(gesture) as System.Windows.Input.ICommand;
			if (command is null)
				return;

			var parameter = CommandParameterProperty?.GetValue(gesture);
			if (command.CanExecute(parameter))
				command.Execute(parameter);
		}
		catch
		{
		}
	}
}

sealed class LongPressTracker : IDisposable
{
	readonly int _pointerId;
	readonly object _gesture;
	readonly Microsoft.Maui.Controls.View _view;
	readonly Func<IElement?, Point?> _positionProvider;
	readonly Action<int, object, Func<IElement?, Point?>> _onTriggered;
	readonly TimeSpan _duration;
	CancellationTokenSource? _cts;

	public LongPressTracker(
		int pointerId,
		object gesture,
		Microsoft.Maui.Controls.View view,
		Func<IElement?, Point?> positionProvider,
		Action<int, object, Func<IElement?, Point?>> onTriggered)
	{
		_pointerId = pointerId;
		_gesture = gesture;
		_view = view;
		_positionProvider = positionProvider;
		_onTriggered = onTriggered;
		_duration = LongPressGestureProxy.GetDuration(gesture);
	}

	public void Start()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = new CancellationTokenSource();
		_ = WaitAsync(_cts.Token);
	}

	async Task WaitAsync(CancellationToken token)
	{
		try
		{
			await Task.Delay(_duration, token).ConfigureAwait(false);
		}
		catch (TaskCanceledException)
		{
			return;
		}
		catch (ObjectDisposedException)
		{
			return;
		}

		if (token.IsCancellationRequested)
			return;

		await AvaloniaUiDispatcher.UIThread.InvokeAsync(() =>
		{
			if (!token.IsCancellationRequested)
				_onTriggered(_pointerId, _gesture, _positionProvider);
		});
	}

	public void Cancel()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = null;
	}

	public void Dispose() => Cancel();
}
