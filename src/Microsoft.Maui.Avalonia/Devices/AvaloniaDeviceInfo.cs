using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Maui.Devices;

namespace Microsoft.Maui.Avalonia.Devices;

internal sealed class AvaloniaDeviceInfo : IDeviceInfo
{
	public string Model { get; } = RuntimeInformation.OSDescription;

	public string Manufacturer { get; } = GetManufacturer();

	public string Name { get; } = Environment.MachineName;

	public string VersionString { get; } = Environment.OSVersion.VersionString;

	public Version Version { get; } = Environment.OSVersion.Version;

	public DevicePlatform Platform { get; } = DetectPlatform();

	public DeviceIdiom Idiom { get; } = DeviceIdiom.Desktop;

	public DeviceType DeviceType { get; } = DeviceType.Physical;

	public static void TryRegisterAsDefault(IDeviceInfo implementation)
	{
		var method = typeof(DeviceInfo).GetMethod("SetCurrent", BindingFlags.Static | BindingFlags.NonPublic);
		method?.Invoke(null, new object?[] { implementation });
	}

	static string GetManufacturer()
	{
		if (OperatingSystem.IsWindows())
			return "Microsoft";
		if (OperatingSystem.IsMacOS())
			return "Apple";
		if (OperatingSystem.IsLinux())
			return "Linux";

		return "Unknown";
	}

	static DevicePlatform DetectPlatform()
	{
		if (OperatingSystem.IsWindows())
			return DevicePlatform.WinUI;
		if (OperatingSystem.IsMacOS())
			return DevicePlatform.macOS;
		if (OperatingSystem.IsIOS())
			return DevicePlatform.iOS;
		if (OperatingSystem.IsAndroid())
			return DevicePlatform.Android;
		if (OperatingSystem.IsTvOS())
			return DevicePlatform.tvOS;
		if (OperatingSystem.IsWatchOS())
			return DevicePlatform.watchOS;
		if (OperatingSystem.IsFreeBSD())
			return DevicePlatform.Unknown;
		if (OperatingSystem.IsLinux())
			return DevicePlatform.Unknown;

		return DevicePlatform.Unknown;
	}
}
