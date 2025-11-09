using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Avalonia;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Maps.Handlers;
using IMap = Microsoft.Maui.Maps.IMap;
using IMapPin = Microsoft.Maui.Maps.IMapPin;
using MapsuiBrush = Mapsui.Styles.Brush;
using MapsuiColor = Mapsui.Styles.Color;

namespace Microsoft.Maui.Avalonia.Handlers;

public class AvaloniaMapHandler : ViewHandler<IMap, MapControl>, IMapHandler
{
	static readonly PropertyMapper<IMap, IMapHandler> Mapper = new(ViewMapper)
	{
		[nameof(IMap.MapType)] = MapMapType,
		[nameof(IMap.IsShowingUser)] = MapIsShowingUser,
		[nameof(IMap.IsScrollEnabled)] = MapIsScrollEnabled,
		[nameof(IMap.IsTrafficEnabled)] = MapIsTrafficEnabled,
		[nameof(IMap.IsZoomEnabled)] = MapIsZoomEnabled,
		[nameof(IMap.Pins)] = MapPins,
		[nameof(IMap.Elements)] = MapElements
	};

	static readonly CommandMapper<IMap, IMapHandler> CommandMapper = new(ViewCommandMapper)
	{
		[nameof(IMap.MoveToRegion)] = MapMoveToRegion,
		[nameof(IMapHandler.UpdateMapElement)] = MapUpdateMapElement
	};

	readonly MemoryLayer _pinLayer = new() { Name = "Pins" };
	readonly Dictionary<IMapPin, IFeature> _pinLookup = new();
	INotifyCollectionChanged? _observedPins;

	public AvaloniaMapHandler()
		: base(Mapper, CommandMapper)
	{
	}

	protected override MapControl CreatePlatformView()
	{
		var control = new MapControl
		{
			Map = new Mapsui.Map()
		};

		control.Map!.Layers.Add(OpenStreetMap.CreateTileLayer());
		control.Map.Layers.Add(_pinLayer);
		control.Info += OnInfo;
		return control;
	}

	protected override void ConnectHandler(MapControl platformView)
	{
		base.ConnectHandler(platformView);
		StartObservingPins();
		UpdatePins();
	}

	protected override void DisconnectHandler(MapControl platformView)
	{
		platformView.Info -= OnInfo;
		StopObservingPins();
		base.DisconnectHandler(platformView);
	}

	void StartObservingPins()
	{
		if (VirtualView?.Pins is INotifyCollectionChanged pins)
		{
			_observedPins = pins;
			_observedPins.CollectionChanged += OnPinsCollectionChanged;
		}
	}

	void StopObservingPins()
	{
		if (_observedPins is not null)
			_observedPins.CollectionChanged -= OnPinsCollectionChanged;

		_observedPins = null;
	}

	void OnPinsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdatePins();

	void UpdatePins()
	{
		if (PlatformView?.Map is null || VirtualView?.Pins is null)
			return;

		_pinLookup.Clear();
		var features = new List<IFeature>();
		foreach (var pin in VirtualView.Pins)
		{
			var feature = CreateFeature(pin);
			_pinLookup[pin] = feature;
			features.Add(feature);
		}

		_pinLayer.Features = features;
		PlatformView.Refresh();
	}

	static IFeature CreateFeature(IMapPin pin)
	{
		var mercator = SphericalMercator.FromLonLat(pin.Location.Longitude, pin.Location.Latitude);
		var feature = new PointFeature(new MPoint(mercator.x, mercator.y));

		feature["Pin"] = pin;
		feature.Styles.Add(new SymbolStyle
		{
			SymbolScale = 0.8f,
			Fill = new MapsuiBrush(new MapsuiColor(51, 102, 204)),
			Outline = new Pen(new MapsuiColor(26, 51, 102), 2)
		});

		if (!string.IsNullOrEmpty(pin.Label))
		{
			feature.Styles.Add(new LabelStyle
			{
				Text = pin.Label,
				Offset = new Offset(0, 24),
				BackColor = new MapsuiBrush(MapsuiColor.FromRgba(0, 0, 0, 128)),
				ForeColor = MapsuiColor.White
			});
		}

		return feature;
	}

	void OnInfo(object? sender, MapInfoEventArgs e)
	{
		if (PlatformView?.Map is null)
			return;

		var mapInfo = e.GetMapInfo(PlatformView.Map.Layers);
		if (mapInfo?.Feature is null)
			return;

		var value = mapInfo.Feature["Pin"];
		if (value is IMapPin pin)
			pin.SendMarkerClick();
	}

	static void MapMapType(IMapHandler handler, IMap map)
	{
		if (handler is not AvaloniaMapHandler { PlatformView: { } mapControl })
			return;

		var mapsuiMap = mapControl.Map;
		if (mapsuiMap is null)
			return;

		var baseLayer = mapsuiMap.Layers.FirstOrDefault(l => l.Name == "BaseTiles");
		if (baseLayer is not TileLayer)
		{
			baseLayer = OpenStreetMap.CreateTileLayer();
			baseLayer.Name = "BaseTiles";
			mapsuiMap.Layers.Insert(0, baseLayer);
		}
	}

	static void MapIsShowingUser(IMapHandler handler, IMap map)
	{
		// Desktop implementations do not expose user location yet.
	}

	static void MapIsScrollEnabled(IMapHandler handler, IMap map)
	{
		if (handler is not AvaloniaMapHandler { PlatformView: { } mapControl })
			return;

		var mapsuiMap = mapControl.Map;
		if (mapsuiMap is null)
			return;

		mapsuiMap.Navigator.PanLock = !map.IsScrollEnabled;
	}

	static void MapIsTrafficEnabled(IMapHandler handler, IMap map)
	{
		// Traffic overlays are not currently supported.
	}

	static void MapIsZoomEnabled(IMapHandler handler, IMap map)
	{
		if (handler is not AvaloniaMapHandler { PlatformView: { } mapControl })
			return;

		var mapsuiMap = mapControl.Map;
		if (mapsuiMap is null)
			return;

		mapsuiMap.Navigator.ZoomLock = !map.IsZoomEnabled;
	}

	static void MapPins(IMapHandler handler, IMap map)
	{
		if (handler is AvaloniaMapHandler avaloniaHandler)
			avaloniaHandler.UpdatePins();
	}

	static void MapElements(IMapHandler handler, IMap map)
	{
		// Elements (Polylines/Polygons) are not implemented yet.
	}

	static void MapMoveToRegion(IMapHandler handler, IMap map, object? arg)
	{
		if (handler is not AvaloniaMapHandler { PlatformView: { } mapControl })
			return;

		var mapsuiMap = mapControl.Map;
		if (mapsuiMap is null)
			return;

		var mapSpan = arg as MapSpan ?? map.VisibleRegion;
		if (mapSpan is null)
			return;

		var (x, y) = SphericalMercator.FromLonLat(mapSpan.Center.Longitude, mapSpan.Center.Latitude);
		var center = new MPoint(x, y);
		mapsuiMap.Navigator.CenterOn(center);

		var span = mapSpan.LongitudeDegrees;
		var viewportWidth = mapsuiMap.Navigator.Viewport.Width;
		if (span > 0 && viewportWidth > 0)
		{
			var resolution = span / viewportWidth;
			mapsuiMap.Navigator.ZoomTo(resolution);
		}
	}

	static void MapUpdateMapElement(IMapHandler handler, IMap map, object? arg)
	{
		// Map elements not yet supported.
	}

	public void UpdateMapElement(IMapElement element)
	{
		// No-op placeholder.
	}
}
