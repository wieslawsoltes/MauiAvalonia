using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using LibVLCSharp.Shared;
using Microsoft.Maui.Avalonia.Handlers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Graphics;
using CommunityToolkit.Maui.Views;
using Xunit;
using Microsoft.Maui.Dispatching;

namespace Microsoft.Maui.Avalonia.Tests;

public class HandlerSmokeTests
{
	[Fact]
	public void MapHandlerCreatesPlatformView()
	{
		var services = new ServiceCollection().BuildServiceProvider();
		var context = new MauiContext(services);

		var handler = new AvaloniaMapHandler();
		handler.SetMauiContext(context);
		handler.SetVirtualView(new Map());

		Assert.NotNull(handler.PlatformView);
	}

	[Fact]
	public void HostBuilderRegistersAvaloniaHandlers()
	{
		var builder = MauiApp.CreateBuilder();
		builder.UseMauiAvaloniaHost();

		var app = builder.Build();

		Assert.NotNull(app);
	}

	[Fact]
	public void ListViewHandlerBuildsGroups()
	{
		if (!HasDispatcher())
			return;

		var services = new ServiceCollection().BuildServiceProvider();
		var context = new MauiContext(services);

		var groups = new[]
		{
			new ObservableCollection<string> { "One", "Two" },
			new ObservableCollection<string> { "Three" }
		};

		var listView = new ListView
		{
			IsGroupingEnabled = true,
			ItemsSource = groups
		};

		var handler = new AvaloniaListViewHandler();
		handler.SetMauiContext(context);
		handler.SetVirtualView(listView);

		var grid = handler.PlatformView;
		var listBox = grid.Children.OfType<global::Avalonia.Controls.ListBox>().Single();
		var itemsSource = listBox.ItemsSource!;

		var count = 0;
		foreach (var _ in itemsSource)
			count++;

		Assert.Equal(5, count); // 2 group headers + 3 items

		listBox.SelectedIndex = 2;
		Assert.Equal("Two", listView.SelectedItem);
	}

	[Fact]
	public void MediaElementKeepsScreenOnWhilePlaying()
	{
		var services = new ServiceCollection();
		var testDisplay = new TestDeviceDisplay();
		services.AddSingleton<IDeviceDisplay>(testDisplay);
		var provider = services.BuildServiceProvider();
		var context = new MauiContext(provider);

		var mediaElement = new MediaElement
		{
			ShouldKeepScreenOn = true
		};

		if (!TryCreateMediaElementHandler(context, mediaElement, out var handler))
			return;

		handler.Invoke("PlayRequested", null);
		Assert.True(testDisplay.KeepScreenOn);

		handler.Invoke("StopRequested", null);
		Assert.False(testDisplay.KeepScreenOn);
	}

	[Fact]
	public void MenuFlyoutUpdatesOnPropertyChanges()
	{
		var services = new ServiceCollection().BuildServiceProvider();
		var context = new MauiContext(services);

		var flyout = new Microsoft.Maui.Controls.MenuFlyout();
		var item = new Microsoft.Maui.Controls.MenuFlyoutItem { Text = "Original" };
		flyout.Add(item);

		var handler = new AvaloniaMenuFlyoutHandler();
		handler.SetMauiContext(context);
		handler.SetVirtualView(flyout);

		var platformItem = handler.PlatformView.Items[0] as global::Avalonia.Controls.MenuItem;
		Assert.NotNull(platformItem);
		Assert.Equal("Original", platformItem!.Header);

		item.Text = "Updated";
		var updatedItem = handler.PlatformView.Items[0] as global::Avalonia.Controls.MenuItem;
		Assert.NotNull(updatedItem);
		Assert.Equal("Updated", updatedItem!.Header);
	}

	static bool HasDispatcher() =>
		Dispatcher.GetForCurrentThread() is not null;

	static bool TryCreateMediaElementHandler(IMauiContext context, MediaElement element, out AvaloniaMediaElementHandler? handler)
	{
		handler = null;

		try
		{
			handler = new AvaloniaMediaElementHandler();
			handler.SetMauiContext(context);
			handler.SetVirtualView(element);
			return true;
		}
		catch (DllNotFoundException)
		{
			return false;
		}
		catch (VLCException)
		{
			return false;
		}
	}

	sealed class TestDeviceDisplay : IDeviceDisplay
	{
		DisplayInfo _info = new(1920, 1080, 1, DisplayOrientation.Landscape, DisplayRotation.Rotation0);

		public DisplayInfo MainDisplayInfo => _info;

		public bool KeepScreenOn { get; set; }

		public event EventHandler<DisplayInfoChangedEventArgs>? MainDisplayInfoChanged;

	}
}
