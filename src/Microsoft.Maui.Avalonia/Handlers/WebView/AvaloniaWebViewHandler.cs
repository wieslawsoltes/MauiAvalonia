using System;
using System.Threading.Tasks;
using AvaloniaWebView;
using Microsoft.Maui.Avalonia.Platform;
using Microsoft.Maui;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using WebViewCore.Events;

namespace Microsoft.Maui.Avalonia.Handlers;

public class AvaloniaWebViewHandler : AvaloniaViewHandler<IWebView, AvaloniaWebView.WebView>, IWebViewHandler
{
	static readonly PropertyMapper<IWebView, AvaloniaWebViewHandler> PropertyMapper = new(ViewHandler.ViewMapper)
	{
		[nameof(IWebView.Source)] = MapSource,
		[nameof(IWebView.UserAgent)] = MapUserAgent
	};

	static readonly CommandMapper<IWebView, AvaloniaWebViewHandler> CommandMapper = new(ViewCommandMapper)
	{
		[nameof(IWebView.GoBack)] = MapGoBack,
		[nameof(IWebView.GoForward)] = MapGoForward,
		[nameof(IWebView.Reload)] = MapReload,
		[nameof(IWebView.Eval)] = MapEval,
		[nameof(IWebView.EvaluateJavaScriptAsync)] = MapEvaluateJavaScriptAsync
	};

	readonly IWebViewDelegate _webViewDelegate;
	WebNavigationEvent _navigationEvent = WebNavigationEvent.NewPage;
	WebNavigationResult _navigationResult = WebNavigationResult.Success;

	public AvaloniaWebViewHandler()
		: base(PropertyMapper, CommandMapper)
	{
		_webViewDelegate = new AvaloniaWebViewDelegate(this);
	}

	protected override AvaloniaWebView.WebView CreatePlatformView() => new();

	protected override void ConnectHandler(AvaloniaWebView.WebView platformView)
	{
		base.ConnectHandler(platformView);
		platformView.NavigationStarting += OnNavigationStarting;
		platformView.NavigationCompleted += OnNavigationCompleted;
	}

	protected override void DisconnectHandler(AvaloniaWebView.WebView platformView)
	{
		platformView.NavigationStarting -= OnNavigationStarting;
		platformView.NavigationCompleted -= OnNavigationCompleted;
		base.DisconnectHandler(platformView);
	}

	void OnNavigationStarting(object? sender, WebViewUrlLoadingEventArg arg)
	{
		if (VirtualView is null)
			return;

		var url = arg.Url?.AbsoluteUri ?? string.Empty;
		var shouldCancel = VirtualView.Navigating(_navigationEvent, url);
		_navigationResult = shouldCancel ? WebNavigationResult.Cancel : WebNavigationResult.Success;
		arg.Cancel = shouldCancel;
	}

	void OnNavigationCompleted(object? sender, WebViewUrlLoadedEventArg arg)
	{
		if (VirtualView is null)
			return;

		var result = arg.IsSuccess ? _navigationResult : WebNavigationResult.Failure;
		var url = PlatformView?.Url?.AbsoluteUri ?? string.Empty;
		VirtualView.Navigated(_navigationEvent, url, result);
		_navigationEvent = WebNavigationEvent.NewPage;
		PlatformView?.UpdateCanGoBackForward(VirtualView);
	}

	static void MapSource(AvaloniaWebViewHandler handler, IWebView webView)
	{
		if (handler.PlatformView is null)
			return;

		webView.Source?.Load(handler._webViewDelegate);
		handler.PlatformView.UpdateCanGoBackForward(webView);
	}

	static void MapUserAgent(AvaloniaWebViewHandler handler, IWebView webView)
	{
		// Avalonia WebView does not expose user-agent customization yet.
	}

	static void MapGoBack(IWebViewHandler handler, IWebView view, object? args)
	{
		if (handler is AvaloniaWebViewHandler avHandler && avHandler.PlatformView is { })
		{
			avHandler._navigationEvent = WebNavigationEvent.Back;
			avHandler.PlatformView.GoBack();
		}
	}

	static void MapGoForward(IWebViewHandler handler, IWebView view, object? args)
	{
		if (handler is AvaloniaWebViewHandler avHandler && avHandler.PlatformView is { })
		{
			avHandler._navigationEvent = WebNavigationEvent.Forward;
			avHandler.PlatformView.GoForward();
		}
	}

	static void MapReload(IWebViewHandler handler, IWebView view, object? args)
	{
		if (handler is AvaloniaWebViewHandler avHandler && avHandler.PlatformView is { })
		{
			avHandler._navigationEvent = WebNavigationEvent.Refresh;
			avHandler.PlatformView.Reload();
		}
	}

	static void MapEval(IWebViewHandler handler, IWebView webView, object? args)
	{
		if (handler is not AvaloniaWebViewHandler avHandler || avHandler.PlatformView is null)
			return;

		if (args is not string script || string.IsNullOrWhiteSpace(script))
			return;

		_ = avHandler.PlatformView.ExecuteScriptAsync(script);
	}

	static void MapEvaluateJavaScriptAsync(IWebViewHandler handler, IWebView webView, object? args)
	{
		if (handler is not AvaloniaWebViewHandler avHandler || avHandler.PlatformView is null)
			return;

		if (args is not EvaluateJavaScriptAsyncRequest request)
			return;

		_ = ExecuteAsync(avHandler.PlatformView.ExecuteScriptAsync(request.Script), request);

		static async Task ExecuteAsync(Task<string?> task, EvaluateJavaScriptAsyncRequest request)
		{
			try
			{
				var result = await task.ConfigureAwait(false);
				request.SetResult(result ?? string.Empty);
			}
			catch (Exception ex)
			{
				request.SetException(ex);
			}
		}
	}

	sealed class AvaloniaWebViewDelegate : IWebViewDelegate
	{
		readonly AvaloniaWebViewHandler _handler;

		public AvaloniaWebViewDelegate(AvaloniaWebViewHandler handler) => _handler = handler;

		public void LoadHtml(string? html, string? baseUrl)
		{
			var platformView = _handler.PlatformView;
			if (platformView is null)
				return;

			if (string.IsNullOrEmpty(html))
			{
				platformView.HtmlContent = string.Empty;
				return;
			}

			if (!string.IsNullOrWhiteSpace(baseUrl))
			{
				var baseTag = $"<base href=\"{baseUrl}\"></base>";
				platformView.HtmlContent = baseTag + html;
			}
			else
			{
				platformView.HtmlContent = html;
			}
		}

		public void LoadUrl(string? url)
		{
			var platformView = _handler.PlatformView;
			if (platformView is null || string.IsNullOrWhiteSpace(url))
				return;

			if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
			{
				platformView.Url = uri;
			}
		}
	}
}
