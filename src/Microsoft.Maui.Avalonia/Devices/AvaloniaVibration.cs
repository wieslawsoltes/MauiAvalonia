using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace Microsoft.Maui.Avalonia.Devices;

internal sealed class AvaloniaVibration : IVibration
{
	CancellationTokenSource? _cts;

	public bool IsSupported => OperatingSystem.IsWindows();

	public void Vibrate()
	{
		Vibrate(TimeSpan.FromMilliseconds(500));
	}

	public void Vibrate(TimeSpan duration)
	{
		if (!IsSupported)
			throw new FeatureNotSupportedException();

		CancelInternal();
		_cts = new CancellationTokenSource();
		_ = PlayToneAsync(duration, _cts.Token);
	}

	public void Cancel()
	{
		if (!IsSupported)
			throw new FeatureNotSupportedException();

		CancelInternal();
	}

	void CancelInternal()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = null;
	}

	static async Task PlayToneAsync(TimeSpan duration, CancellationToken token)
	{
		try
		{
			await Task.Delay(duration, token).ConfigureAwait(false);
		}
		catch (TaskCanceledException)
		{
		}
	}

	public static void TryRegisterAsDefault(IVibration implementation)
	{
		var method = typeof(Vibration).GetMethod("SetDefault", BindingFlags.Static | BindingFlags.NonPublic);
		method?.Invoke(null, new object?[] { implementation });
	}
}
