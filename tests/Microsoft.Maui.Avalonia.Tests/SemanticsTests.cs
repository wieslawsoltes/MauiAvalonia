using Avalonia.Controls;
using Microsoft.Maui.Avalonia.Accessibility;
using Xunit;

namespace Microsoft.Maui.Avalonia.Tests;

public sealed class SemanticsTests
{
	[Fact]
	public void ApplySetsAutomationProperties()
	{
		var label = new Microsoft.Maui.Controls.Label();
		Microsoft.Maui.Controls.SemanticProperties.SetDescription(label, "Accessible text");
		Microsoft.Maui.Controls.SemanticProperties.SetHint(label, "Helpful hint");

		var control = new TextBlock();

		AvaloniaSemanticNode.Apply(control, label);

		Assert.Equal("Accessible text", global::Avalonia.Automation.AutomationProperties.GetName(control));
		Assert.Equal("Helpful hint", global::Avalonia.Automation.AutomationProperties.GetHelpText(control));
	}
}
