using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Microsoft.Maui.Avalonia.Graphics;
using Microsoft.Maui.Handlers;
using SpinnerLocation = global::Avalonia.Controls.Location;

namespace Microsoft.Maui.Avalonia.Handlers;

public sealed class AvaloniaStepperHandler : AvaloniaViewHandler<IStepper, NumericUpDown>, IStepperHandler
{
	public static readonly IPropertyMapper<IStepper, AvaloniaStepperHandler> Mapper =
		new PropertyMapper<IStepper, AvaloniaStepperHandler>(ViewHandler.ViewMapper)
		{
			[nameof(IRange.Minimum)] = MapMinimum,
			[nameof(IRange.Maximum)] = MapMaximum,
			[nameof(IRange.Value)] = MapValue,
			[nameof(IStepper.Interval)] = MapInterval,
			[nameof(IView.Background)] = MapBackground
		};

	bool _updatingValue;

	public AvaloniaStepperHandler()
		: base(Mapper)
	{
	}

	protected override NumericUpDown CreatePlatformView() =>
		new()
		{
			AllowSpin = true,
			ClipValueToMinMax = true,
			ButtonSpinnerLocation = SpinnerLocation.Right,
			HorizontalAlignment = AvaloniaHorizontalAlignment.Left,
			Increment = 1m,
			Minimum = 0m,
			Maximum = 100m,
			Value = 0m,
			Width = 120
		};

	protected override void ConnectHandler(NumericUpDown platformView)
	{
		base.ConnectHandler(platformView);
		platformView.ValueChanged += OnValueChanged;
	}

	protected override void DisconnectHandler(NumericUpDown platformView)
	{
		platformView.ValueChanged -= OnValueChanged;
		base.DisconnectHandler(platformView);
	}

	static void MapMinimum(AvaloniaStepperHandler handler, IRange range) => handler.UpdateRange();

	static void MapMaximum(AvaloniaStepperHandler handler, IRange range) => handler.UpdateRange();

	static void MapValue(AvaloniaStepperHandler handler, IRange range)
	{
		if (handler.PlatformView is null)
			return;

		try
		{
			handler._updatingValue = true;
			handler.PlatformView.Value = handler.ToDecimal(range.Value);
		}
		finally
		{
			handler._updatingValue = false;
		}
	}

	static void MapInterval(AvaloniaStepperHandler handler, IStepper stepper)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.Increment = handler.ToDecimal(stepper.Interval);
	}

	static void MapBackground(AvaloniaStepperHandler handler, IStepper stepper)
	{
		if (handler.PlatformView is null)
			return;

		handler.PlatformView.Background = stepper.Background?.ToAvaloniaBrush();
	}

	void UpdateRange()
	{
		if (PlatformView is null || VirtualView is null)
			return;

		PlatformView.Minimum = ToDecimal(VirtualView.Minimum);
		PlatformView.Maximum = ToDecimal(VirtualView.Maximum);
		PlatformView.Value = ToDecimal(VirtualView.Value);
	}

	void OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
	{
		if (_updatingValue || VirtualView is null || e.NewValue is null)
			return;

		var newValue = (double)e.NewValue.Value;
		if (Math.Abs(newValue - VirtualView.Value) < double.Epsilon)
			return;

		VirtualView.Value = newValue;
	}

	decimal ToDecimal(double value)
	{
		if (double.IsNaN(value) || double.IsInfinity(value))
			return 0m;

		if (value > (double)decimal.MaxValue)
			return decimal.MaxValue;

		if (value < (double)decimal.MinValue)
			return decimal.MinValue;

		return (decimal)value;
	}
}
