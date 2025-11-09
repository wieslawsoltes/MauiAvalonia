using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Microsoft.Maui.Avalonia.Handlers;
using Microsoft.Maui.Controls;

namespace Microsoft.Maui.Avalonia.Navigation;

internal sealed class AvaloniaStackNavigationManager
{
	static readonly TimeSpan DefaultTransitionDuration = TimeSpan.FromMilliseconds(200);
	readonly Dictionary<IView, Control> _realizedViews = new();
	readonly List<IView> _previousNonModalStack = new();

	IReadOnlyList<IView> _currentStack = Array.Empty<IView>();
	IStackNavigation? _navigationView;
	ContentControl? _presenter;
	TransitioningContentControl? _transitionHost;
	AvaloniaGrid? _hostGrid;
	Panel? _modalLayer;
	IMauiContext? _mauiContext;
	IPageTransition _defaultTransition = new CrossFade(DefaultTransitionDuration);
	IPageTransition? _slideTransition;

	public void Connect(IStackNavigation navigationView, ContentControl presenter, IMauiContext? context)
	{
		_navigationView = navigationView;
		_presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
		_mauiContext = context;
		EnsureHost();
	}

	public void Disconnect()
	{
		ClearHostContent();
		_realizedViews.Clear();
		_currentStack = Array.Empty<IView>();
		_previousNonModalStack.Clear();
		_navigationView = null;
		_presenter = null;
		_transitionHost = null;
		_mauiContext = null;
	}

	public void NavigateTo(NavigationRequest request)
	{
		_currentStack = request.NavigationStack;
		EnsureHost();

		if (_currentStack.Count == 0)
		{
			ClearHostContent();
			_navigationView?.NavigationFinished(_currentStack);
			return;
		}

		if (_mauiContext is null)
			return;

		var nonModalStack = ExtractNonModalStack(_currentStack);
		var animationKind = DetermineAnimation(_previousNonModalStack, nonModalStack, request.Animated);
		_previousNonModalStack.Clear();
		_previousNonModalStack.AddRange(nonModalStack);

		var baseView = ResolveBaseView(_currentStack, out var modalViews);
		if (baseView is not null)
		{
			var control = GetOrCreateControl(baseView);
			var animateBase = request.Animated && modalViews.Count == 0;
			ShowBaseControl(control, animationKind, animateBase);
		}

		UpdateModalOverlays(modalViews);
		RecycleStaleViews(_currentStack);
		_navigationView?.NavigationFinished(_currentStack);
	}

	void EnsureHost()
	{
		if (_presenter is null)
			return;

		if (_hostGrid is null)
		{
			_transitionHost = new TransitioningContentControl
			{
				HorizontalAlignment = AvaloniaHorizontalAlignment.Stretch,
				VerticalAlignment = AvaloniaVerticalAlignment.Stretch,
				PageTransition = _defaultTransition
			};

			_modalLayer = new AvaloniaGrid
			{
				HorizontalAlignment = AvaloniaHorizontalAlignment.Stretch,
				VerticalAlignment = AvaloniaVerticalAlignment.Stretch,
				IsHitTestVisible = true
			};

			_hostGrid = new AvaloniaGrid();
			_hostGrid.Children.Add(_transitionHost);
			_hostGrid.Children.Add(_modalLayer);
		}

		_presenter.Content = _hostGrid;
	}

	void ClearHostContent()
	{
		if (_transitionHost is not null)
			_transitionHost.Content = null;

		_modalLayer?.Children.Clear();
		if (_presenter is not null && _hostGrid is null)
		{
			_presenter.SetValue(ContentControl.ContentProperty, null);
		}
	}

	Control GetOrCreateControl(IView view)
	{
		if (_realizedViews.TryGetValue(view, out var control))
			return control;

		if (_mauiContext is null)
			throw new InvalidOperationException("MAUI context is unavailable for navigation.");

		control = view.ToAvaloniaControl(_mauiContext)
			?? throw new InvalidOperationException($"Failed to create platform view for {view}.");

		_realizedViews[view] = control;
		return control;
	}

	void ShowBaseControl(Control control, NavigationAnimationKind animationKind, bool allowAnimation)
	{
		if (_transitionHost is null)
			return;

		var previousTransition = _transitionHost.PageTransition;
		var previousReverse = _transitionHost.IsTransitionReversed;

		if (!allowAnimation || animationKind == NavigationAnimationKind.None)
		{
			_transitionHost.PageTransition = null;
			_transitionHost.IsTransitionReversed = false;
			_transitionHost.Content = control;
			_transitionHost.PageTransition = previousTransition;
			_transitionHost.IsTransitionReversed = previousReverse;
			return;
		}

		var transition = animationKind switch
		{
			NavigationAnimationKind.Push => _slideTransition ??= CreateSlideTransition(),
			NavigationAnimationKind.Pop => _slideTransition ??= CreateSlideTransition(),
			NavigationAnimationKind.Replace => _defaultTransition,
			_ => null
		};

		_transitionHost.PageTransition = transition ?? _defaultTransition;
		_transitionHost.IsTransitionReversed = animationKind == NavigationAnimationKind.Pop;
		_transitionHost.Content = control;
		_transitionHost.PageTransition = previousTransition;
		_transitionHost.IsTransitionReversed = previousReverse;
	}

	void RecycleStaleViews(IReadOnlyList<IView> liveStack)
	{
		if (_realizedViews.Count == 0)
			return;

		var liveSet = liveStack.ToHashSet();
		var stale = _realizedViews.Keys.Where(view => !liveSet.Contains(view)).ToList();

		foreach (var view in stale)
		{
			if (_realizedViews.TryGetValue(view, out var control))
			{
				if (control is IDisposable disposable)
					disposable.Dispose();
			}

			if (view is IElement element && element.Handler is IElementHandler handler)
			{
				handler.DisconnectHandler();
				element.Handler = null;
			}

			_realizedViews.Remove(view);
		}
	}

	static IView? ResolveBaseView(IReadOnlyList<IView> stack, out List<IView> modalViews)
	{
		modalViews = new List<IView>();
		IView? baseView = null;

		foreach (var view in stack)
		{
			if (IsModalView(view))
			{
				modalViews.Add(view);
			}
			else
			{
				baseView = view;
				modalViews.Clear();
			}
		}

		baseView ??= stack.LastOrDefault();
		return baseView;
	}

	static bool IsModalView(IView view) =>
		view is Page page && page.Navigation?.ModalStack?.Contains(page) == true;

	static List<IView> ExtractNonModalStack(IReadOnlyList<IView> stack)
	{
		var result = new List<IView>();

		foreach (var view in stack)
		{
			if (IsModalView(view))
				break;

			result.Add(view);
		}

		if (result.Count == 0 && stack.Count > 0)
			result.Add(stack[stack.Count - 1]);

		return result;
	}

	static NavigationAnimationKind DetermineAnimation(IReadOnlyList<IView> previous, IReadOnlyList<IView> current, bool animated)
	{
		if (!animated)
			return NavigationAnimationKind.None;

		if (current.Count == 0)
			return NavigationAnimationKind.None;

		if (previous.Count == 0)
			return NavigationAnimationKind.None;

		var previousTop = previous[^1];
		var currentTop = current[^1];

		if (ReferenceEquals(previousTop, currentTop))
			return NavigationAnimationKind.None;

		if (current.Count > previous.Count)
			return NavigationAnimationKind.Push;

		if (current.Count < previous.Count)
			return NavigationAnimationKind.Pop;

		var previousIndex = IndexOf(previous, currentTop);
		if (previousIndex >= 0 && previousIndex < previous.Count - 1)
			return NavigationAnimationKind.Pop;

		return NavigationAnimationKind.Replace;
	}

	static int IndexOf(IReadOnlyList<IView> stack, IView view)
	{
		for (var i = 0; i < stack.Count; i++)
		{
			if (ReferenceEquals(stack[i], view))
				return i;
		}

		return -1;
	}

	static IPageTransition CreateSlideTransition() =>
		new PageSlide(DefaultTransitionDuration);

	void UpdateModalOverlays(IReadOnlyList<IView> modalViews)
	{
		if (_modalLayer is null)
			return;

		_modalLayer.Children.Clear();

		if (modalViews.Count == 0)
			return;

		foreach (var modalView in modalViews)
		{
			var control = GetOrCreateControl(modalView);
			var overlay = new AvaloniaBorderControl
			{
				Background = new AvaloniaSolidColorBrush(AvaloniaColor.FromArgb(0x60, 0, 0, 0)),
				HorizontalAlignment = AvaloniaHorizontalAlignment.Stretch,
				VerticalAlignment = AvaloniaVerticalAlignment.Stretch,
				Child = control
			};
			_modalLayer.Children.Add(overlay);
		}
	}

	enum NavigationAnimationKind
	{
		None,
		Push,
		Pop,
		Replace
	}
}
