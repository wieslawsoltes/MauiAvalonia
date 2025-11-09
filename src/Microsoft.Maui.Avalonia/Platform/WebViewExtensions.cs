using AvaloniaWebView;
using Microsoft.Maui;

namespace Microsoft.Maui.Avalonia.Platform;

static class WebViewExtensions
{
	public static void UpdateCanGoBackForward(this AvaloniaWebView.WebView platformView, IWebView webView)
	{
		if (platformView is null || webView is null)
			return;

		webView.CanGoBack = platformView.IsCanGoBack;
		webView.CanGoForward = platformView.IsCanGoForward;
	}
}
