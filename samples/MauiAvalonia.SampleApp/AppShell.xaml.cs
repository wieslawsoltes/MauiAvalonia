using Microsoft.Maui.Controls;

namespace MauiAvalonia.SampleApp;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(TabsPage), typeof(TabsPage));
	}
}
