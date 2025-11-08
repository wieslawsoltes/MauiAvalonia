namespace MauiAvalonia.SampleApp;

public partial class MainPage : ContentPage
{
	int count = 0;

	public MainPage()
	{
		InitializeComponent();
	}

	private void OnCounterClicked(object? sender, EventArgs e)
	{
		count++;

		if (count == 1)
			CounterBtn.Text = $"Clicked {count} time";
		else
			CounterBtn.Text = $"Clicked {count} times";

		SemanticScreenReader.Announce(CounterBtn.Text);
	}

	private async void OnNavigateToTabsClicked(object? sender, EventArgs e)
	{
		if (Shell.Current is null)
			return;

		try
		{
			await Shell.Current.GoToAsync(nameof(TabsPage));
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Navigation to {nameof(TabsPage)} failed: {ex}");
		}
	}
}
