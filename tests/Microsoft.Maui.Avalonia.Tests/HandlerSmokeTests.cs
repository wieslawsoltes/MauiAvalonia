using System.Collections.ObjectModel;
using Avalonia.Controls;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Avalonia.Handlers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Graphics;
using Xunit;

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
		var listBox = grid.Children.OfType<ListBox>().Single();
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

		using var handler = new AvaloniaMediaElementHandler();
		handler.SetMauiContext(context);
		handler.SetVirtualView(mediaElement);

		handler.Invoke(nameof(MediaElement.PlayRequested), null);
		Assert.True(testDisplay.KeepScreenOn);

		handler.Invoke(nameof(MediaElement.StopRequested), null);
		Assert.False(testDisplay.KeepScreenOn);
	}

	[Fact]
	public void MenuFlyoutUpdatesOnPropertyChanges()
	{
		var services = new ServiceCollection().BuildServiceProvider();
		var context = new MauiContext(services);

		var flyout = new MenuFlyout();
		var item = new MenuFlyoutItem { Text = "Original" };
		flyout.Add(item);

		var handler = new AvaloniaMenuFlyoutHandler();
		handler.SetMauiContext(context);
		handler.SetVirtualView(flyout);

		Assert.Equal("Original", ((Avalonia.Controls.MenuItem)handler.PlatformView.Items[0]).Header);

		item.Text = "Updated";
		Assert.Equal("Updated", ((Avalonia.Controls.MenuItem)handler.PlatformView.Items[0]).Header);
	}

	sealed class TestDeviceDisplay : IDeviceDisplay
	{
		DisplayInfo _info = new(1920, 1080, 1, DisplayOrientation.Landscape, DisplayRotation.Rotation0);

		public DisplayInfo MainDisplayInfo => _info;

		public bool KeepScreenOn { get; set; }

		public event EventHandler<DisplayInfoChangedEventArgs>? MainDisplayInfoChanged;

	}
}
