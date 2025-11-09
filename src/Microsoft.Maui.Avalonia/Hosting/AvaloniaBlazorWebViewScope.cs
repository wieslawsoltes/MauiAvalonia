using System;
using System.Reflection;
using System.Threading;
using AvaloniaBlazorWebView.Configurations;

namespace Microsoft.Maui.Avalonia.Hosting;

static class AvaloniaBlazorWebViewScope
{
	static readonly AsyncLocal<BlazorWebViewHostOptions?> Current = new();

	public static IDisposable Push(BlazorWebViewHostOptions options)
	{
		var previous = Current.Value;
		Current.Value = options;
		return new Scope(previous);
	}

	public static void Configure(BlazorWebViewSetting setting)
	{
		var options = Current.Value;
		if (options is null)
		{
			setting.StartAddress = "/";
			return;
		}

		var value = options.Value;
		setting.StartAddress = string.IsNullOrWhiteSpace(value.StartPath) ? "/" : value.StartPath!;
		if (!string.IsNullOrWhiteSpace(value.AppAddress))
			setting.AppAddress = value.AppAddress!;
		if (!string.IsNullOrWhiteSpace(value.WwwRoot))
			setting.WWWRoot = value.WwwRoot!;
		setting.ResourceAssembly = value.ResourceAssembly;
	}

	sealed class Scope : IDisposable
	{
		readonly BlazorWebViewHostOptions? _previous;

		public Scope(BlazorWebViewHostOptions? previous) => _previous = previous;

		public void Dispose() => Current.Value = _previous;
	}
}

readonly record struct BlazorWebViewHostOptions(string? StartPath, string? WwwRoot, string? AppAddress, Assembly? ResourceAssembly);
