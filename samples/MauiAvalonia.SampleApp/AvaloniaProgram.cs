#if NET8_0
using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.WebView.Desktop;

namespace MauiAvalonia.SampleApp;

internal static class AvaloniaProgram
{
	[STAThread]
	public static void Main(string[] args) =>
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

	public static AppBuilder BuildAvaloniaApp() =>
		AppBuilder.Configure<AvaloniaHostApplication>()
			.UsePlatformDetect()
			.UseDesktopWebView()
			.LogToTrace();
}
#endif
