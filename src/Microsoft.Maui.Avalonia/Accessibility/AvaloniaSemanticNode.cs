using System;
using Avalonia.Automation;
using Avalonia.Controls;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using MauiAutomationProperties = Microsoft.Maui.Controls.AutomationProperties;

namespace Microsoft.Maui.Avalonia.Accessibility;

internal static class AvaloniaSemanticNode
{
	public static void Apply(Control control, IView view)
	{
		if (control is null || view is null)
			return;

		var description = GetDescription(view);
		SetName(control, description);

		var hint = GetHint(view);
		SetHelpText(control, hint);

	}

	public static void Apply(Control element, BindableObject? source, string? fallbackName = null, string? fallbackHint = null)
	{
		if (element is null)
			return;

		var name = fallbackName;
		var hint = fallbackHint;

		if (source is not null)
		{
			name = GetAutomationName(source) ?? name;
			hint = GetAutomationHint(source) ?? hint;
		}

		SetName(element, name);
		SetHelpText(element, hint);
	}

	static string? GetDescription(IView view)
	{
		if (!string.IsNullOrWhiteSpace(view.Semantics?.Description))
			return view.Semantics?.Description;

		if (view is BindableObject bindable)
		{
			var description = SemanticProperties.GetDescription(bindable);
			if (!string.IsNullOrWhiteSpace(description))
				return description;

			return GetAutomationName(bindable);
		}

		return null;
	}

	static string? GetHint(IView view)
	{
		if (!string.IsNullOrWhiteSpace(view.Semantics?.Hint))
			return view.Semantics?.Hint;

		if (view is BindableObject bindable)
		{
			var hint = SemanticProperties.GetHint(bindable);
			if (!string.IsNullOrWhiteSpace(hint))
				return hint;

			return GetAutomationHint(bindable);
		}

		return null;
	}

	static void SetName(Control element, string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			element.ClearValue(global::Avalonia.Automation.AutomationProperties.NameProperty);
		else
			global::Avalonia.Automation.AutomationProperties.SetName(element, value);
	}

	static void SetHelpText(Control element, string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			element.ClearValue(global::Avalonia.Automation.AutomationProperties.HelpTextProperty);
		else
			global::Avalonia.Automation.AutomationProperties.SetHelpText(element, value);
	}

	static string? GetAutomationName(BindableObject bindable)
	{
#pragma warning disable CS0618
		var automationName = MauiAutomationProperties.GetName(bindable);
#pragma warning restore CS0618
		return string.IsNullOrWhiteSpace(automationName) ? null : automationName;
	}

	static string? GetAutomationHint(BindableObject bindable)
	{
#pragma warning disable CS0618
		var helpText = MauiAutomationProperties.GetHelpText(bindable);
#pragma warning restore CS0618
		return string.IsNullOrWhiteSpace(helpText) ? null : helpText;
	}
}
