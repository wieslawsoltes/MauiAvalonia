using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Avalonia.Navigation;

namespace Microsoft.Maui.Avalonia.Handlers;

public class AvaloniaMenuFlyoutHandler : ElementHandler<IMenuFlyout, ContextMenu>, IMenuFlyoutHandler
{
	static readonly PropertyMapper<IMenuFlyout, AvaloniaMenuFlyoutHandler> Mapper = new(ElementMapper);
	static readonly CommandMapper<IMenuFlyout, AvaloniaMenuFlyoutHandler> CommandMapper = new(ElementCommandMapper);

	readonly List<INotifyPropertyChanged> _propertySubscriptions = new();
	readonly List<INotifyCollectionChanged> _subMenuSubscriptions = new();
	INotifyCollectionChanged? _menuSubscription;

	public AvaloniaMenuFlyoutHandler()
		: base(Mapper, CommandMapper)
	{
	}

	protected override ContextMenu CreatePlatformElement()
	{
		return BuildMenu() ?? new ContextMenu();
	}

	protected override void ConnectHandler(ContextMenu platformView)
	{
		base.ConnectHandler(platformView);
		StartObserving();
		Rebuild();
	}

	protected override void DisconnectHandler(ContextMenu platformView)
	{
		StopObserving();
		base.DisconnectHandler(platformView);
	}

	public void Add(IMenuElement view) => Rebuild();

	public void Remove(IMenuElement view) => Rebuild();

	public void Clear() => Rebuild();

	public void Insert(int index, IMenuElement view) => Rebuild();

	void Rebuild()
	{
		if (PlatformView is null)
			return;

		var menu = BuildMenu();
		if (menu is null)
			return;

		PlatformView.Items.Clear();
		foreach (var item in menu.Items)
			PlatformView.Items.Add(item);

		RefreshElementObservers();
	}

	ContextMenu? BuildMenu()
	{
		if (VirtualView is null || MauiContext is null)
			return null;

		return AvaloniaMenuBuilder.BuildContextMenu(VirtualView, MauiContext);
	}

	void StartObserving()
	{
		if (VirtualView is INotifyCollectionChanged notify)
		{
			_menuSubscription = notify;
			_menuSubscription.CollectionChanged += OnMenuChanged;
		}

		RefreshElementObservers();
	}

	void StopObserving()
	{
		if (_menuSubscription is not null)
			_menuSubscription.CollectionChanged -= OnMenuChanged;
		_menuSubscription = null;

		ClearElementObservers();
	}

	void OnMenuChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		Rebuild();
	}

	void OnSubMenuChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		Rebuild();
	}

	void OnElementPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		Rebuild();
	}

	void RefreshElementObservers()
	{
		ClearElementObservers();

		if (VirtualView is null)
			return;

		foreach (var element in VirtualView)
			ObserveElement(element);
	}

	void ClearElementObservers()
	{
		foreach (var subscription in _propertySubscriptions)
			subscription.PropertyChanged -= OnElementPropertyChanged;
		_propertySubscriptions.Clear();

		foreach (var subscription in _subMenuSubscriptions)
			subscription.CollectionChanged -= OnSubMenuChanged;
		_subMenuSubscriptions.Clear();
	}

	void ObserveElement(IMenuElement element)
	{
		if (element is INotifyPropertyChanged propertyChanged)
		{
			propertyChanged.PropertyChanged += OnElementPropertyChanged;
			_propertySubscriptions.Add(propertyChanged);
		}

		if (element is IMenuFlyoutSubItem subItem)
		{
			if (subItem is INotifyCollectionChanged notify)
			{
				notify.CollectionChanged += OnSubMenuChanged;
				_subMenuSubscriptions.Add(notify);
			}

			foreach (var child in subItem)
				ObserveElement(child);
		}
	}
}
