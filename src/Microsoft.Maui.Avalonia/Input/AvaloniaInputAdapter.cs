using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaPointerEventArgs = Avalonia.Input.PointerEventArgs;
using AvaloniaPointerPressedEventArgs = Avalonia.Input.PointerPressedEventArgs;
using AvaloniaPointerReleasedEventArgs = Avalonia.Input.PointerReleasedEventArgs;
using AvaloniaDragEventArgsNative = Avalonia.Input.DragEventArgs;
using Microsoft.Maui;
using Microsoft.Maui.Avalonia.Handlers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Internals;
using Microsoft.Maui.Graphics;
using ButtonsMask = Microsoft.Maui.Controls.ButtonsMask;
using MauiControls = Microsoft.Maui.Controls;
using MauiPoint = Microsoft.Maui.Graphics.Point;

namespace Microsoft.Maui.Avalonia.Input;

internal sealed class AvaloniaInputAdapter : IDisposable
{
	const double DragStartThreshold = 4d;
	const double TapMovementThreshold = 8d;
	const double PanStartThreshold = 6d;
const string FilesPropertyKey = "Files";
const string UriPropertyKey = "Uri";
const string UnicodeTextFormat = "UnicodeText";
const string UriDataFormat = "Uri";
const string BitmapDataFormat = "Bitmap";

	readonly Control _control;
	readonly IView _view;
	readonly MauiControls.View? _controlsView;
	readonly IList<IGestureRecognizer>? _gestureCollection;
	readonly INotifyCollectionChanged? _gestureNotifier;
	readonly List<INotifyPropertyChanged> _gestureSubscriptions = new();
	readonly Dictionary<int, PointerContact> _activeContacts = new();

	MauiControls.DragGestureRecognizer? _pendingDragRecognizer;
	AvaloniaPointerPressedEventArgs? _dragTriggerEvent;
	AvaloniaPoint? _dragOrigin;
	int? _dragPointerId;
	bool _dragInProgress;
	bool _dropEventsAttached;
	bool _isDisposed;
	bool _isPanning;
	int _panTouchPoints;
	bool _isPinching;
	double _lastPinchDistance;

	AvaloniaInputAdapter(IView view, Control control)
	{
		_view = view ?? throw new ArgumentNullException(nameof(view));
		_control = control ?? throw new ArgumentNullException(nameof(control));
		_controlsView = view as MauiControls.View;
		_gestureCollection = _controlsView?.GestureRecognizers;
		if (_gestureCollection is INotifyCollectionChanged notifier)
		{
			_gestureNotifier = notifier;
			notifier.CollectionChanged += OnGesturesChanged;
		}

		if (_gestureCollection != null)
		{
			foreach (var recognizer in _gestureCollection)
				SubscribeGesture(recognizer);
		}

		UpdateDropSubscriptions();
		HookControlEvents();
	}

	public static AvaloniaInputAdapter? Attach(IView view, Control control)
	{
		if (view is null || control is null)
			return null;

		return new AvaloniaInputAdapter(view, control);
	}

	public void Dispose()
	{
		if (_isDisposed)
			return;

		_isDisposed = true;
		UnhookControlEvents();
		DetachGestureSubscriptions();
	}

	void HookControlEvents()
	{
		_control.PointerEntered += OnPointerEntered;
		_control.PointerExited += OnPointerExited;
		_control.PointerMoved += OnPointerMoved;
		_control.PointerPressed += OnPointerPressed;
		_control.PointerReleased += OnPointerReleased;
		_control.PointerCaptureLost += OnPointerCaptureLost;
		_control.GotFocus += OnGotFocus;
		_control.LostFocus += OnLostFocus;
	}

	void UnhookControlEvents()
	{
		_control.PointerEntered -= OnPointerEntered;
		_control.PointerExited -= OnPointerExited;
		_control.PointerMoved -= OnPointerMoved;
		_control.PointerPressed -= OnPointerPressed;
		_control.PointerReleased -= OnPointerReleased;
		_control.PointerCaptureLost -= OnPointerCaptureLost;
		_control.GotFocus -= OnGotFocus;
		_control.LostFocus -= OnLostFocus;
		DetachDropHandlers();
	}

	void OnPointerEntered(object? sender, AvaloniaPointerEventArgs e) =>
		DispatchPointerGestures((view, recognizer) =>
			recognizer.SendPointerEntered(view, relative => GetPointerPosition(relative, e), null, GetButtonsMask(e)));

	void OnPointerExited(object? sender, AvaloniaPointerEventArgs e)
	{
		DispatchPointerGestures((view, recognizer) =>
			recognizer.SendPointerExited(view, relative => GetPointerPosition(relative, e), null, GetButtonsMask(e)));
		ResetPendingDrag();
		if (_activeContacts.ContainsKey(e.Pointer.Id))
			HandlePointerLost(e.Pointer.Id, canceled: true);
	}

	void OnPointerMoved(object? sender, AvaloniaPointerEventArgs e)
	{
		DispatchPointerGestures((view, recognizer) =>
			recognizer.SendPointerMoved(view, relative => GetPointerPosition(relative, e), null, GetButtonsMask(e)));

		TryStartDrag(e);
		UpdatePointerContact(e);
		UpdatePanGesture();
		UpdatePinchGesture();
	}

	void OnPointerPressed(object? sender, AvaloniaPointerPressedEventArgs e)
	{
		var buttons = GetButtonsMask(e);
		DispatchPointerGestures((view, recognizer) =>
			recognizer.SendPointerPressed(view, relative => GetPointerPosition(relative, e), null, buttons));

		TrackPointerPressed(e, buttons);
		PreparePendingDrag(e);
	}

	void OnPointerReleased(object? sender, AvaloniaPointerReleasedEventArgs e)
	{
		var buttons = e.InitialPressMouseButton == MouseButton.Right ? ButtonsMask.Secondary : ButtonsMask.Primary;
		DispatchPointerGestures((view, recognizer) =>
			recognizer.SendPointerReleased(view, relative => GetPointerPosition(relative, e), null, buttons));

		UpdatePointerContact(e);
		OnPointerReleaseCompleted(e, buttons);
	}

	void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
	{
		HandlePointerLost(e.Pointer.Id, canceled: true);
	}

	void OnGotFocus(object? sender, GotFocusEventArgs e)
	{
		if (!_view.IsFocused)
			_view.IsFocused = true;
	}

	void OnLostFocus(object? sender, RoutedEventArgs e)
	{
		if (_view.IsFocused)
			_view.IsFocused = false;
	}

	void DispatchPointerGestures(Action<MauiControls.View, PointerGestureRecognizer> dispatch)
	{
		var view = _controlsView;
		if (view?.GestureRecognizers is not IEnumerable<IGestureRecognizer> gestures)
			return;

		var pointerGestures = gestures.GetGesturesFor<PointerGestureRecognizer>();
		foreach (var recognizer in pointerGestures)
			dispatch(view, recognizer);
	}

	void TrackPointerPressed(AvaloniaPointerPressedEventArgs e, ButtonsMask buttons)
	{
		var point = e.GetCurrentPoint(_control).Position;
		var contact = new PointerContact(e.Pointer.Id, point, e.Pointer.Type, buttons, e.ClickCount);
		_activeContacts[e.Pointer.Id] = contact;
	}

	void UpdatePointerContact(AvaloniaPointerEventArgs e)
	{
		if (!_activeContacts.TryGetValue(e.Pointer.Id, out var contact))
			return;

		var position = e.GetCurrentPoint(_control).Position;
		contact.Update(position, TapMovementThreshold);
	}

	void OnPointerReleaseCompleted(AvaloniaPointerReleasedEventArgs e, ButtonsMask buttons)
	{
		HandlePointerRelease(e, buttons);
		ResetPendingDrag();
	}

	void HandlePointerRelease(AvaloniaPointerReleasedEventArgs e, ButtonsMask buttons)
	{
		if (_dragInProgress || !_activeContacts.TryGetValue(e.Pointer.Id, out var contact))
		{
			return;
		}

		UpdatePanGesture();

		if (!_isPanning)
			TryHandleTap(contact, e, buttons);

		_activeContacts.Remove(e.Pointer.Id);

		TryHandleSwipe(contact, e);

		if (_isPanning)
			CompletePanGesture(success: true);

		if (_isPinching && _activeContacts.Count < 2)
			CompletePinchGesture(success: true);
	}

	void HandlePointerLost(int pointerId, bool canceled)
	{
		if (!_activeContacts.ContainsKey(pointerId))
			return;

		_activeContacts.Remove(pointerId);

		if (canceled)
		{
			if (_isPanning)
				CompletePanGesture(success: false);
			if (_isPinching)
				CompletePinchGesture(success: false);
			ResetPendingDrag();
		}
	}

	void TryHandleTap(PointerContact contact, AvaloniaPointerReleasedEventArgs args, ButtonsMask buttons)
	{
		if (_controlsView?.GestureRecognizers is not IEnumerable<IGestureRecognizer> gestures)
			return;

		if (contact.ExceededTapThreshold || _isPinching || _isPanning)
			return;

		var tapGestures = gestures.GetGesturesFor<TapGestureRecognizer>().ToArray();
		if (tapGestures.Length == 0)
			return;

		var isDoubleTap = contact.ClickCount >= 2;
		var hasExplicitDoubleTap = tapGestures.Any(g => g.NumberOfTapsRequired == 2);
		var positionProvider = new Func<IElement?, MauiPoint?>(relative => GetPointerPosition(relative, args));

		foreach (var recognizer in tapGestures)
		{
			if (!MatchesButtonPreference(recognizer.Buttons, buttons))
				continue;

			if (isDoubleTap)
			{
				if (recognizer.NumberOfTapsRequired == 2 || (!hasExplicitDoubleTap && recognizer.NumberOfTapsRequired == 1))
				{
					recognizer.SendTapped(_controlsView!, positionProvider);
				}
			}
			else if (recognizer.NumberOfTapsRequired <= 1)
			{
				recognizer.SendTapped(_controlsView!, positionProvider);
			}
		}
	}

	void TryHandleSwipe(PointerContact contact, AvaloniaPointerReleasedEventArgs args)
	{
		if (_controlsView?.GestureRecognizers is not IEnumerable<IGestureRecognizer> gestures)
			return;

		if (_isPanning || _dragInProgress)
			return;

		var swipeGestures = gestures.GetGesturesFor<SwipeGestureRecognizer>().ToArray();
		if (swipeGestures.Length == 0)
			return;

		var totalX = contact.Last.X - contact.Start.X;
		var totalY = contact.Last.Y - contact.Start.Y;

		foreach (var recognizer in swipeGestures)
		{
			var controller = (ISwipeGestureController)recognizer;
			controller.SendSwipe(_controlsView!, totalX, totalY);
			controller.DetectSwipe(_controlsView!, recognizer.Direction);
		}
	}

	void UpdatePanGesture()
	{
		if (_controlsView?.GestureRecognizers is not IEnumerable<IGestureRecognizer> gestures)
			return;

		var view = _controlsView;
		var panGestures = gestures.GetGesturesFor<PanGestureRecognizer>()
			.Where(g => g.TouchPoints == _activeContacts.Count)
			.Cast<IPanGestureController>()
			.ToArray();

		if (panGestures.Length == 0)
		{
			if (_isPanning)
				CompletePanGesture(success: true);
			return;
		}

		var (deltaX, deltaY) = GetAverageDelta();
		var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

		if (!_isPanning)
		{
			if (distance < PanStartThreshold)
				return;

			foreach (var controller in panGestures)
				controller.SendPanStarted(view!, PanGestureRecognizer.CurrentId.Value);

			_isPanning = true;
			_panTouchPoints = _activeContacts.Count;
		}

		foreach (var controller in panGestures)
			controller.SendPan(view!, deltaX, deltaY, PanGestureRecognizer.CurrentId.Value);
	}

	void CompletePanGesture(bool success)
	{
		if (!_isPanning || _controlsView?.GestureRecognizers is not IEnumerable<IGestureRecognizer> gestures)
			return;

		var view = _controlsView;
		var panGestures = gestures.GetGesturesFor<PanGestureRecognizer>()
			.Where(g => g.TouchPoints == _panTouchPoints)
			.Cast<IPanGestureController>()
			.ToArray();

		foreach (var controller in panGestures)
		{
			if (success)
				controller.SendPanCompleted(view!, PanGestureRecognizer.CurrentId.Value);
			else
				controller.SendPanCanceled(view!, PanGestureRecognizer.CurrentId.Value);
		}

		PanGestureRecognizer.CurrentId.Increment();
		_isPanning = false;
		_panTouchPoints = 0;
	}

	void UpdatePinchGesture()
	{
		if (_controlsView?.GestureRecognizers is not IEnumerable<IGestureRecognizer> gestures)
			return;

		var pinchGestures = gestures.GetGesturesFor<MauiControls.PinchGestureRecognizer>()
			.Cast<IPinchGestureController>()
			.ToArray();

		if (pinchGestures.Length == 0)
		{
			if (_isPinching)
				CompletePinchGesture(success: true);
			return;
		}

		if (_activeContacts.Count < 2)
		{
			if (_isPinching)
				CompletePinchGesture(success: true);
			return;
		}

		var contacts = _activeContacts.Values.Take(2).ToArray();
		var first = contacts[0].Last;
		var second = contacts[1].Last;
		var distance = GetDistance(first, second);
		if (distance <= double.Epsilon)
			return;

		var center = new AvaloniaPoint((first.X + second.X) * 0.5, (first.Y + second.Y) * 0.5);
		var origin = NormalizeToView(center);

		if (!_isPinching)
		{
			_isPinching = true;
			_lastPinchDistance = distance;
			foreach (var controller in pinchGestures)
				controller.SendPinchStarted(_controlsView!, origin);
			return;
		}

		if (_lastPinchDistance <= double.Epsilon)
			_lastPinchDistance = distance;

		var delta = distance / _lastPinchDistance;
		if (double.IsNaN(delta) || double.IsInfinity(delta) || Math.Abs(delta - 1) < 0.001)
			return;

		foreach (var controller in pinchGestures)
			controller.SendPinch(_controlsView!, delta, origin);

		_lastPinchDistance = distance;
	}

	void CompletePinchGesture(bool success)
	{
		if (!_isPinching || _controlsView?.GestureRecognizers is not IEnumerable<IGestureRecognizer> gestures)
			return;

		var pinchGestures = gestures.GetGesturesFor<MauiControls.PinchGestureRecognizer>()
			.Cast<IPinchGestureController>()
			.ToArray();

		foreach (var controller in pinchGestures)
		{
			if (success)
				controller.SendPinchEnded(_controlsView!);
			else
				controller.SendPinchCanceled(_controlsView!);
		}

		_isPinching = false;
		_lastPinchDistance = 0;
	}

	(double deltaX, double deltaY) GetAverageDelta()
	{
		if (_activeContacts.Count == 0)
			return (0, 0);

		double totalX = 0;
		double totalY = 0;
		foreach (var contact in _activeContacts.Values)
		{
			totalX += contact.Last.X - contact.Start.X;
			totalY += contact.Last.Y - contact.Start.Y;
		}

		var count = _activeContacts.Count;
		return (totalX / count, totalY / count);
	}

	static bool MatchesButtonPreference(ButtonsMask preferred, ButtonsMask current)
	{
		if (current == ButtonsMask.Secondary)
			return (preferred & ButtonsMask.Secondary) == ButtonsMask.Secondary;

		return (preferred & ButtonsMask.Primary) == ButtonsMask.Primary;
	}

	MauiPoint NormalizeToView(AvaloniaPoint point)
	{
		var bounds = _control.Bounds;
		var width = Math.Max(1, bounds.Width);
		var height = Math.Max(1, bounds.Height);
		return new MauiPoint(point.X / width, point.Y / height);
	}

	static double GetDistance(AvaloniaPoint first, AvaloniaPoint second)
	{
		var dx = first.X - second.X;
		var dy = first.Y - second.Y;
		return Math.Sqrt((dx * dx) + (dy * dy));
	}

	static ImageSource? TryCreateImageSource(object? native)
	{
		try
		{
			switch (native)
			{
				case Bitmap bitmap:
				{
					using var buffer = new MemoryStream();
					bitmap.Save(buffer);
					var bytes = buffer.ToArray();
					return ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
				}
				case Stream stream:
				{
					using var buffer = new MemoryStream();
					stream.CopyTo(buffer);
					if (stream.CanSeek)
						stream.Seek(0, SeekOrigin.Begin);
					var bytes = buffer.ToArray();
					return ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
				}
				default:
					return null;
			}
		}
		catch
		{
			return null;
		}
	}

	sealed class PointerContact
	{
		public PointerContact(int id, AvaloniaPoint start, PointerType pointerType, ButtonsMask buttons, int clickCount)
		{
			Id = id;
			Start = start;
			Last = start;
			PointerType = pointerType;
			Buttons = buttons;
			ClickCount = clickCount;
		}

		public int Id { get; }
		public AvaloniaPoint Start { get; }
		public AvaloniaPoint Last { get; private set; }
		public PointerType PointerType { get; }
		public ButtonsMask Buttons { get; }
		public int ClickCount { get; }
		public bool ExceededTapThreshold { get; private set; }

		public void Update(AvaloniaPoint position, double threshold)
		{
			Last = position;
			if (!ExceededTapThreshold && GetDistanceSquared(Start, position) > threshold * threshold)
				ExceededTapThreshold = true;
		}

		static double GetDistanceSquared(AvaloniaPoint first, AvaloniaPoint second)
		{
			var dx = first.X - second.X;
			var dy = first.Y - second.Y;
			return (dx * dx) + (dy * dy);
		}
	}

	void PreparePendingDrag(AvaloniaPointerPressedEventArgs e)
	{
		if (_controlsView?.GestureRecognizers is not IEnumerable<IGestureRecognizer> gestures)
			return;

		var dragRecognizer = gestures.GetGesturesFor<MauiControls.DragGestureRecognizer>()
			.FirstOrDefault(g => g.CanDrag);

		if (dragRecognizer is null)
		{
			ResetPendingDrag();
			return;
		}

		_pendingDragRecognizer = dragRecognizer;
		_dragTriggerEvent = e;
		_dragOrigin = e.GetCurrentPoint(_control).Position;
		_dragPointerId = e.Pointer.Id;
	}

	void TryStartDrag(AvaloniaPointerEventArgs e)
	{
		if (_pendingDragRecognizer is null ||
			_dragOrigin is null ||
			_dragPointerId != e.Pointer.Id ||
			_dragInProgress ||
			_dragTriggerEvent is null)
		{
			return;
		}

		var current = e.GetCurrentPoint(_control).Position;
		if (!ExceedsDragThreshold(current, _dragOrigin.Value))
			return;

		var recognizer = _pendingDragRecognizer;
		var trigger = _dragTriggerEvent;
		ResetPendingDrag();
		_ = StartDragAsync(recognizer, trigger);
	}

	async Task StartDragAsync(MauiControls.DragGestureRecognizer recognizer, AvaloniaPointerPressedEventArgs triggerEvent)
	{
		if (_controlsView is null)
			return;

		_dragInProgress = true;

		try
		{
			var args = recognizer.SendDragStarting(
				_controlsView,
				relative => GetPointerPosition(relative, triggerEvent),
				null);

			if (args.Cancel)
				return;

			var services = _controlsView?.Handler?.MauiContext?.Services;
			var dataObject = await BuildDataObjectAsync(args.Data, services, CancellationToken.None).ConfigureAwait(false);
#pragma warning disable CS0618
			var effects = await DragDrop.DoDragDrop(triggerEvent, dataObject, DragDropEffects.Copy);
#pragma warning restore CS0618
			var _ = effects; // currently unused but kept for parity
			recognizer.SendDropCompleted(new DropCompletedEventArgs());
		}
		finally
		{
			_dragInProgress = false;
		}
	}

	static async Task<IDataObject> BuildDataObjectAsync(DataPackage package, IServiceProvider? services, CancellationToken token)
	{
		var dataObject = new DataObject();

		if (!string.IsNullOrWhiteSpace(package.Text))
		{
			dataObject.Set(UnicodeTextFormat, package.Text);
		}

		if (package.Image is not null && services is not null)
		{
			try
			{
				var bitmap = await AvaloniaImageSourceLoader.LoadAsync(package.Image, services, token).ConfigureAwait(false);
				if (bitmap is not null)
					dataObject.Set(BitmapDataFormat, bitmap);
			}
			catch
			{
			}
		}

		if (package.Properties.TryGetValue(FilesPropertyKey, out var filesValue) &&
			filesValue is IEnumerable<string> files)
		{
			var fileArray = files.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
			if (fileArray.Length > 0)
				dataObject.Set(DataFormats.FileNames, fileArray);
		}

		if (package.Properties.TryGetValue(UriPropertyKey, out var uriValue))
		{
			var uriString = uriValue switch
			{
				Uri parsed => parsed.ToString(),
				string text => text,
				_ => null
			};

			if (!string.IsNullOrWhiteSpace(uriString) &&
				Uri.TryCreate(uriString, UriKind.RelativeOrAbsolute, out var uri))
			{
				dataObject.Set(UriDataFormat, uri);
			}
		}

		foreach (var property in package.Properties)
		{
			if (property.Key.Equals(FilesPropertyKey, StringComparison.OrdinalIgnoreCase) ||
				property.Key.Equals(UriPropertyKey, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (property.Value is not null)
				dataObject.Set(property.Key, property.Value);
		}

		return dataObject;
	}

	void ResetPendingDrag()
	{
		_pendingDragRecognizer = null;
		_dragTriggerEvent = null;
		_dragOrigin = null;
		_dragPointerId = null;
	}

	void UpdateDropSubscriptions()
	{
		if (HasDropGestures())
			AttachDropHandlers();
		else
			DetachDropHandlers();
	}

	bool HasDropGestures()
	{
		if (_controlsView?.GestureRecognizers is not IEnumerable<IGestureRecognizer> gestures)
			return false;

		return gestures.GetGesturesFor<MauiControls.DropGestureRecognizer>()
			.Any(recognizer => recognizer.AllowDrop);
	}

	void AttachDropHandlers()
	{
		if (_dropEventsAttached)
			return;

		if (!HasDropGestures())
			return;

		_dropEventsAttached = true;
		DragDrop.SetAllowDrop(_control, true);
		_control.AddHandler(DragDrop.DragEnterEvent, OnDragEnter, RoutingStrategies.Bubble);
		_control.AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Bubble);
		_control.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave, RoutingStrategies.Bubble);
		_control.AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Bubble);
	}

	void DetachDropHandlers()
	{
		if (!_dropEventsAttached)
			return;

		_dropEventsAttached = false;
		DragDrop.SetAllowDrop(_control, false);
		_control.RemoveHandler(DragDrop.DragEnterEvent, OnDragEnter);
		_control.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
		_control.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
		_control.RemoveHandler(DragDrop.DropEvent, OnDrop);
	}

	void OnDragEnter(object? sender, AvaloniaDragEventArgsNative e) =>
		HandleDragOver(e);

	void OnDragOver(object? sender, AvaloniaDragEventArgsNative e) =>
		HandleDragOver(e);

	void OnDragLeave(object? sender, AvaloniaDragEventArgsNative e)
	{
		if (!TryGetDropGestures(out var gestures))
			return;

		var package = CreateDataPackage(e.Data);
		var args = new AvaloniaDragEventArgs(package, relative => GetDragPosition(relative, e));
		foreach (var recognizer in gestures)
			recognizer.SendDragLeave(args);

		e.Handled = true;
	}

	async void OnDrop(object? sender, AvaloniaDragEventArgsNative e)
	{
		if (!TryGetDropGestures(out var gestures))
			return;

		var package = CreateDataPackage(e.Data);
		var dropArgs = new AvaloniaDropEventArgs(package.View, relative => GetDragPosition(relative, e));
		foreach (var recognizer in gestures)
			await recognizer.SendDrop(dropArgs);

		e.DragEffects = dropArgs.Handled ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = dropArgs.Handled;
	}

	void HandleDragOver(AvaloniaDragEventArgsNative e)
	{
		if (!TryGetDropGestures(out var gestures))
			return;

		var package = CreateDataPackage(e.Data);
		var dragArgs = new AvaloniaDragEventArgs(package, relative => GetDragPosition(relative, e));
		foreach (var recognizer in gestures)
			recognizer.SendDragOver(dragArgs);

		e.DragEffects = ConvertToDragEffects(dragArgs.AcceptedOperation);
		if (dragArgs.AcceptedOperation != DataPackageOperation.None)
			e.Handled = true;
	}

	static DragDropEffects ConvertToDragEffects(DataPackageOperation operation) =>
		operation == DataPackageOperation.Copy ? DragDropEffects.Copy : DragDropEffects.None;

	static DataPackage CreateDataPackage(IDataObject? data)
	{
		var package = new DataPackage();
		if (data is null)
			return package;

		if (data.Contains(UnicodeTextFormat) && data.Get(UnicodeTextFormat) is string text && !string.IsNullOrWhiteSpace(text))
		{
			package.Text = text;
		}
		else if (data.Contains(DataFormats.Text) && data.Get(DataFormats.Text) is string legacy && !string.IsNullOrWhiteSpace(legacy))
		{
			package.Text = legacy;
		}

		if (data.Contains(DataFormats.FileNames) && data.Get(DataFormats.FileNames) is IEnumerable<string> files)
		{
			var fileList = files.Where(path => !string.IsNullOrWhiteSpace(path)).ToList();
			if (fileList.Count > 0)
				package.Properties[FilesPropertyKey] = fileList;
		}

		if (data.Contains(UriDataFormat))
		{
			var uriValue = data.Get(UriDataFormat);
			switch (uriValue)
			{
				case Uri uri:
					package.Properties[UriPropertyKey] = uri;
					break;
				case string uriString when Uri.TryCreate(uriString, UriKind.RelativeOrAbsolute, out var parsed):
					package.Properties[UriPropertyKey] = parsed;
					break;
			}
		}

		if (data.Contains(BitmapDataFormat))
		{
			var bitmapData = data.Get(BitmapDataFormat);
			var imageSource = TryCreateImageSource(bitmapData);
			if (imageSource is not null)
				package.Image = imageSource;
		}

		foreach (var format in data.GetDataFormats())
		{
			if (string.Equals(format, UnicodeTextFormat, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(format, DataFormats.Text, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(format, DataFormats.FileNames, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(format, UriDataFormat, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(format, BitmapDataFormat, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			try
			{
				var value = data.Get(format);
				if (value is not null)
					package.Properties[format] = value;
			}
			catch
			{
			}
		}

		return package;
	}

	bool TryGetDropGestures(out IEnumerable<MauiControls.DropGestureRecognizer> gestures)
	{
		gestures = Enumerable.Empty<MauiControls.DropGestureRecognizer>();
		if (_controlsView?.GestureRecognizers is not IEnumerable<IGestureRecognizer> list)
			return false;

		var dropGestures = list.GetGesturesFor<MauiControls.DropGestureRecognizer>().ToArray();
		if (dropGestures.Length == 0)
			return false;

		gestures = dropGestures;
		return true;
	}

	MauiPoint? GetPointerPosition(IElement? relativeTo, AvaloniaPointerEventArgs e)
	{
		var target = ResolveVisual(relativeTo);
		if (target is null)
			return null;

		var point = e.GetPosition(target);
		return new MauiPoint(point.X, point.Y);
	}

	MauiPoint? GetPointerPosition(IElement? relativeTo, AvaloniaPointerPressedEventArgs e)
	{
		var target = ResolveVisual(relativeTo);
		if (target is null)
			return null;

		var point = e.GetPosition(target);
		return new MauiPoint(point.X, point.Y);
	}

	MauiPoint? GetPointerPosition(IElement? relativeTo, AvaloniaPointerReleasedEventArgs e)
	{
		var target = ResolveVisual(relativeTo);
		if (target is null)
			return null;

		var point = e.GetPosition(target);
		return new MauiPoint(point.X, point.Y);
	}

	MauiPoint? GetDragPosition(IElement? relativeTo, AvaloniaDragEventArgsNative e)
	{
		var target = ResolveVisual(relativeTo);
		if (target is null)
			return null;

		var point = e.GetPosition(target);
		return new MauiPoint(point.X, point.Y);
	}

	Visual? ResolveVisual(IElement? relativeTo)
	{
		if (relativeTo?.Handler?.PlatformView is Visual visual)
			return visual;

		return _control;
	}

	static bool ExceedsDragThreshold(AvaloniaPoint current, AvaloniaPoint origin) =>
		Math.Abs(current.X - origin.X) >= DragStartThreshold ||
		Math.Abs(current.Y - origin.Y) >= DragStartThreshold;

	ButtonsMask GetButtonsMask(AvaloniaPointerEventArgs e)
	{
		if (e.Pointer.Type != PointerType.Mouse)
			return ButtonsMask.Primary;

		var point = e.GetCurrentPoint(_control);
		if (point.Properties.IsRightButtonPressed ||
			point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed ||
			point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonReleased)
		{
			return ButtonsMask.Secondary;
		}

		return ButtonsMask.Primary;
	}

	void OnGesturesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.OldItems != null)
		{
			foreach (var gesture in e.OldItems.OfType<IGestureRecognizer>())
				UnsubscribeGesture(gesture);
		}

		if (e.NewItems != null)
		{
			foreach (var gesture in e.NewItems.OfType<IGestureRecognizer>())
				SubscribeGesture(gesture);
		}

		if (e.Action == NotifyCollectionChangedAction.Reset)
		{
			DetachGestureSubscriptions(detachCollectionChanged: false);
			if (_gestureCollection != null)
			{
				foreach (var gesture in _gestureCollection)
					SubscribeGesture(gesture);
			}
		}

		UpdateDropSubscriptions();
	}

	void SubscribeGesture(IGestureRecognizer recognizer)
	{
		if (recognizer is not INotifyPropertyChanged notify)
			return;

		notify.PropertyChanged += OnGesturePropertyChanged;
		_gestureSubscriptions.Add(notify);
	}

	void UnsubscribeGesture(IGestureRecognizer recognizer)
	{
		if (recognizer is not INotifyPropertyChanged notify)
			return;

		notify.PropertyChanged -= OnGesturePropertyChanged;
		_gestureSubscriptions.Remove(notify);
	}

	void OnGesturePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (string.Equals(e.PropertyName, nameof(MauiControls.DropGestureRecognizer.AllowDrop), StringComparison.Ordinal) ||
			string.Equals(e.PropertyName, nameof(MauiControls.DragGestureRecognizer.CanDrag), StringComparison.Ordinal))
		{
			UpdateDropSubscriptions();
		}
	}

	void DetachGestureSubscriptions(bool detachCollectionChanged = true)
	{
		if (detachCollectionChanged && _gestureNotifier != null)
			_gestureNotifier.CollectionChanged -= OnGesturesChanged;

		foreach (var notify in _gestureSubscriptions)
			notify.PropertyChanged -= OnGesturePropertyChanged;

		_gestureSubscriptions.Clear();
	}
}
