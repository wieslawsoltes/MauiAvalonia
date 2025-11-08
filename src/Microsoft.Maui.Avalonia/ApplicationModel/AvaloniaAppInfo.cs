using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace Microsoft.Maui.Avalonia.ApplicationModel;

internal sealed class AvaloniaAppInfo : IAppInfo
{
	readonly string _packageName;
	readonly string _name;
	readonly Version _version;
	readonly string _versionString;
	readonly string _buildString;

	public AvaloniaAppInfo()
	{
		var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
		_packageName = AppDomain.CurrentDomain.FriendlyName;
		_name = assembly.GetName().Name ?? _packageName;
		_version = assembly.GetName().Version ?? new Version(1, 0, 0, 0);
		_versionString = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
			?? assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
			?? _version.ToString();
		_buildString = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
			?? (_version.Revision >= 0 ? _version.Revision.ToString() : "0");
	}

	public string PackageName => _packageName;

	public string Name => _name;

	public string VersionString => _versionString;

	public Version Version => _version;

	public string BuildString => _buildString;

	public AppTheme RequestedTheme
	{
		get
		{
			var theme = global::Avalonia.Application.Current?.ActualThemeVariant;
			if (theme == global::Avalonia.Styling.ThemeVariant.Dark)
				return AppTheme.Dark;
			if (theme == global::Avalonia.Styling.ThemeVariant.Light)
				return AppTheme.Light;

			return AppTheme.Unspecified;
		}
	}

	public AppPackagingModel PackagingModel =>
		OperatingSystem.IsWindows() ? AppPackagingModel.Unpackaged : AppPackagingModel.Packaged;

	public LayoutDirection RequestedLayoutDirection => LayoutDirection.LeftToRight;

	public void ShowSettingsUI() =>
		throw new FeatureNotSupportedException();

	public static void TryRegisterAsDefault(IAppInfo implementation)
	{
		var method = typeof(AppInfo).GetMethod("SetCurrent", BindingFlags.Static | BindingFlags.NonPublic);
		method?.Invoke(null, new object?[] { implementation });
	}
}
