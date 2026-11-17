"use client";

import "maplibre-gl/dist/maplibre-gl.css";
import {
  LngLatBounds,
  Map as MaplibreMap,
  NavigationControl,
  setWorkerUrl,
  type ExpressionSpecification,
  type GeoJSONSource,
  type StyleSpecification,
} from "maplibre-gl";
import { useTranslations } from "next-intl";
import { useEffect, useMemo, useRef, useState, useSyncExternalStore } from "react";
import { useLines } from "@/entities/transit";
import { liveVehicleStore, useConnectionStore } from "@/entities/vehicle";
import type { GeoPoint } from "@/shared/lib/geo";
import { EmptyState, ErrorState, LoadingState } from "@/shared/ui/state-views";
import { headingArrow, ICON_PIXEL_RATIO, travelArrow } from "../model/map-icons";
import { buildVehicleCollection, countMatches } from "../model/vehicle-geojson";

/** A planned route, already split at the vehicle: the page hands it to the map, which only draws it. */
export type RouteOverlay = {
  passed: GeoPoint[];
  remaining: GeoPoint[];
  stops: { id: string; name: string; lat: number; lon: number; passed: boolean }[];
  /** True when the path is only straight lines between stops (informational; the map draws it the same). */
  approximate: boolean;
};

/** A past track: the line to draw, and the real GPS fixes shown as dots on top of it. */
export type TrackOverlay = { path: GeoPoint[]; measurements: GeoPoint[] };

type LiveMapProps = {
  selectedKey: string | null;
  onSelect: (key: string | null) => void;
  filter: string;
  /** A past track to draw, or null. */
  track: TrackOverlay | null;
  /** The selected vehicle's planned route, or null. */
  route: RouteOverlay | null;
};

const VEHICLES_SOURCE = "vehicles";
const VEHICLES_LAYER = "vehicles";
const HEADINGS_LAYER = "vehicle-headings";
const SELECTED_HEADING_LAYER = "selected-vehicle-heading";
const TRACK_SOURCE = "track";
const TRACK_LAYER = "track";
const TRACK_ARROWS_LAYER = "track-arrows";
const MEASUREMENTS_SOURCE = "track-measurements";
const MEASUREMENTS_LAYER = "track-measurements";
const ROUTE_PASSED_SOURCE = "route-passed";
const ROUTE_PASSED_LAYER = "route-passed";
const ROUTE_REMAINING_SOURCE = "route-remaining";
const ROUTE_REMAINING_LAYER = "route-remaining";
const ROUTE_STOPS_SOURCE = "route-stops";
const ROUTE_STOPS_LAYER = "route-stops";
const ROUTE_STOP_NAMES_LAYER = "route-stop-names";

const VEHICLE_COLOR = "#0f766e";
const SELECTED_COLOR = "#dc2626";
const TRACK_COLOR = "#2563eb";
/** The planned route is purple, so it never looks like the measured path (blue). */
const ROUTE_COLOR = "#7c3aed";
const ROUTE_PASSED_OPACITY = 0.35;
const PASSED_STOP_COLOR = "#c4b5fd";
const ROUTE_DASHES = [2, 2];
const APPROXIMATE_DASHES = [0.2, 2];
/** Stop names are noise on a country-wide view, so they only show once zoomed in. */
const STOP_NAMES_MIN_ZOOM = 13;
const VEHICLE_RADIUS = 4.5;
const SELECTED_RADIUS = 9;
/** Below this zoom thousands of arrows would only be noise; the selected vehicle keeps its arrow at any zoom. */
const HEADINGS_MIN_ZOOM = 10;

const OSM_STYLE: StyleSpecification = {
  version: 8,
  // Text layers (stop names) need a glyph server; the style has none by default.
  glyphs: "https://demotiles.maplibre.org/font/{fontstack}/{range}.pbf",
  sources: {
    osm: {
      type: "raster",
      tiles: ["https://tile.openstreetmap.org/{z}/{x}/{y}.png"],
      tileSize: 256,
      attribution: "© OpenStreetMap contributors",
    },
  },
  layers: [{ id: "osm", type: "raster", source: "osm" }],
};

/** Served from /public; see scripts/copy-maplibre-worker.mjs for why. */
const WORKER_PATH = "/maplibre-gl-worker.mjs";

const EMPTY_COLLECTION: GeoJSON.FeatureCollection = { type: "FeatureCollection", features: [] };

/** Vehicles that reported a bearing; about a third of the fleet does not. */
const HAS_BEARING: ExpressionSpecification = ["!=", ["get", "bearing"], null];

function selectedHeadingFilter(selectedKey: string | null): ExpressionSpecification {
  return ["all", HAS_BEARING, ["==", ["get", "key"], selectedKey ?? ""]];
}

function selectedExpression<T extends number | string>(
  selectedKey: string | null,
  whenSelected: T,
  otherwise: T,
): ExpressionSpecification {
  return ["case", ["==", ["get", "key"], selectedKey ?? ""], whenSelected, otherwise];
}

type MapStatus = "loading" | "ready" | "error";

function lineCoordinates(points: GeoPoint[]): [number, number][] {
  // GeoJSON order is [longitude, latitude]
  return points.map((point): [number, number] => [point.lon, point.lat]);
}

function lineFeature(points: GeoPoint[]): GeoJSON.GeoJSON {
  if (points.length < 2) return EMPTY_COLLECTION;
  return { type: "Feature", properties: {}, geometry: { type: "LineString", coordinates: lineCoordinates(points) } };
}

export function LiveMap({ selectedKey, onSelect, filter, track, route }: LiveMapProps) {
  const t = useTranslations("liveMap");
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<MaplibreMap | null>(null);
  const scheduleUpdateRef = useRef<() => void>(() => {});
  // Duration of the last vehicle update in ms (the same number is in the "vehicles:render" measure).
  const lastUpdateMsRef = useRef(0);
  const trackFittedRef = useRef(false);
  const routeFittedRef = useRef(false);

  const [status, setStatus] = useState<MapStatus>("loading");
  // Bumping this re-runs the map effect, which is how "retry" re-creates the map.
  const [attempt, setAttempt] = useState(0);

  // Refs let the map effect read the latest props without being re-run (and re-creating the map).
  const filterRef = useRef(filter);
  const selectedKeyRef = useRef(selectedKey);
  const onSelectRef = useRef(onSelect);
  const lines = useLines();
  const linesRef = useRef(lines);
  useEffect(() => {
    linesRef.current = lines;
    filterRef.current = filter;
    selectedKeyRef.current = selectedKey;
    onSelectRef.current = onSelect;
  });

  const connectionStatus = useConnectionStore((state) => state.status);
  const version = useSyncExternalStore(liveVehicleStore.subscribe, liveVehicleStore.getVersion, () => 0);
  const total = liveVehicleStore.getCount();
  // `version` is not read inside, but it is what tells us the store content changed.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const matching = useMemo(() => countMatches(liveVehicleStore.all(), filter, lines), [version, filter, lines]);

  // Create the map once (and again after a retry); remove it on unmount.
  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    let map: MaplibreMap;
    try {
      setWorkerUrl(new URL(WORKER_PATH, window.location.origin).href);
      map = new MaplibreMap({
        container,
        style: OSM_STYLE,
        center: [10.75, 59.91],
        zoom: 9,
      });
    } catch {
      // WebGL unavailable: the constructor throws. Report it after the effect has finished.
      const timer = setTimeout(() => setStatus("error"), 0);
      return () => clearTimeout(timer);
    }
    mapRef.current = map;
    map.addControl(new NavigationControl(), "top-right");

    let frame: number | null = null;

    function updateVehicles() {
      frame = null;
      const source = map.getSource<GeoJSONSource>(VEHICLES_SOURCE);
      if (!source) return;
      performance.mark("vehicles:render:start");
      source.setData(buildVehicleCollection(liveVehicleStore.all(), filterRef.current, linesRef.current));
      const measure = performance.measure("vehicles:render", "vehicles:render:start");
      lastUpdateMsRef.current = measure.duration;
    }

    // Many notifications can arrive within one frame; one scheduled frame covers all of them.
    function scheduleUpdate() {
      if (frame === null) frame = requestAnimationFrame(updateVehicles);
    }
    scheduleUpdateRef.current = scheduleUpdate;

    map.on("load", () => {
      map.addImage("heading", headingArrow(VEHICLE_RADIUS + 1, 7.5, VEHICLE_COLOR), { pixelRatio: ICON_PIXEL_RATIO });
      map.addImage("heading-selected", headingArrow(SELECTED_RADIUS + 2, 12, SELECTED_COLOR), {
        pixelRatio: ICON_PIXEL_RATIO,
      });
      map.addImage("travel-arrow", travelArrow(TRACK_COLOR), { pixelRatio: ICON_PIXEL_RATIO });

      // Layers are drawn in the order they are added: planned route, track, vehicles, then the
      // selected vehicle's arrow.
      map.addSource(ROUTE_PASSED_SOURCE, { type: "geojson", data: EMPTY_COLLECTION });
      map.addLayer({
        id: ROUTE_PASSED_LAYER,
        type: "line",
        source: ROUTE_PASSED_SOURCE,
        // Same purple dashes as the part ahead, but faded: still the plan, just already behind the vehicle.
        layout: { "line-join": "round" },
        paint: {
          "line-color": ROUTE_COLOR,
          "line-width": 4,
          "line-opacity": ROUTE_PASSED_OPACITY,
          "line-dasharray": ROUTE_DASHES,
        },
      });
      map.addSource(ROUTE_REMAINING_SOURCE, { type: "geojson", data: EMPTY_COLLECTION });
      map.addLayer({
        id: ROUTE_REMAINING_LAYER,
        type: "line",
        source: ROUTE_REMAINING_SOURCE,
        // Dashes need butt caps, round ones would fill the gaps.
        layout: { "line-join": "round" },
        paint: { "line-color": ROUTE_COLOR, "line-width": 4, "line-dasharray": ROUTE_DASHES },
      });
      map.addSource(ROUTE_STOPS_SOURCE, { type: "geojson", data: EMPTY_COLLECTION });
      map.addLayer({
        id: ROUTE_STOPS_LAYER,
        type: "circle",
        source: ROUTE_STOPS_SOURCE,
        paint: {
          "circle-radius": 4,
          "circle-color": "#ffffff",
          "circle-stroke-width": 2,
          "circle-stroke-color": ["case", ["get", "passed"], PASSED_STOP_COLOR, ROUTE_COLOR],
        },
      });
      map.addLayer({
        id: ROUTE_STOP_NAMES_LAYER,
        type: "symbol",
        source: ROUTE_STOPS_SOURCE,
        minzoom: STOP_NAMES_MIN_ZOOM,
        layout: {
          "text-field": ["get", "name"],
          "text-font": ["Noto Sans Regular"],
          "text-size": 11,
          "text-offset": [0, 0.9],
          "text-anchor": "top",
          "text-allow-overlap": false,
        },
        paint: { "text-color": "#1e293b", "text-halo-color": "#ffffff", "text-halo-width": 1.5 },
      });

      map.addSource(TRACK_SOURCE, { type: "geojson", data: EMPTY_COLLECTION });
      map.addLayer({
        id: TRACK_LAYER,
        type: "line",
        source: TRACK_SOURCE,
        layout: { "line-cap": "round", "line-join": "round" },
        paint: { "line-color": TRACK_COLOR, "line-width": 4, "line-opacity": 0.8 },
      });
      // Arrowheads along the line show which way the vehicle travelled.
      map.addLayer({
        id: TRACK_ARROWS_LAYER,
        type: "symbol",
        source: TRACK_SOURCE,
        layout: {
          "symbol-placement": "line",
          "symbol-spacing": 70,
          "icon-image": "travel-arrow",
          "icon-allow-overlap": true,
          "icon-ignore-placement": true,
        },
      });
      map.addSource(MEASUREMENTS_SOURCE, { type: "geojson", data: EMPTY_COLLECTION });
      map.addLayer({
        id: MEASUREMENTS_LAYER,
        type: "circle",
        source: MEASUREMENTS_SOURCE,
        paint: {
          "circle-radius": 2.5,
          "circle-color": "#ffffff",
          "circle-stroke-width": 1.5,
          "circle-stroke-color": TRACK_COLOR,
        },
      });

      map.addSource(VEHICLES_SOURCE, { type: "geojson", data: EMPTY_COLLECTION, promoteId: "key" });
      // The arrow is rotated by the bearing the vehicle reported, so the direction of travel is
      // visible at once instead of only after the next position arrives.
      map.addLayer({
        id: HEADINGS_LAYER,
        type: "symbol",
        source: VEHICLES_SOURCE,
        minzoom: HEADINGS_MIN_ZOOM,
        filter: HAS_BEARING,
        layout: {
          "icon-image": "heading",
          "icon-rotate": ["get", "bearing"],
          "icon-rotation-alignment": "map",
          "icon-allow-overlap": true,
          "icon-ignore-placement": true,
        },
      });
      map.addLayer({
        id: VEHICLES_LAYER,
        type: "circle",
        source: VEHICLES_SOURCE,
        paint: {
          "circle-radius": selectedExpression(selectedKeyRef.current, SELECTED_RADIUS, VEHICLE_RADIUS),
          "circle-color": selectedExpression(selectedKeyRef.current, SELECTED_COLOR, VEHICLE_COLOR),
          "circle-stroke-width": 1.5,
          "circle-stroke-color": "#ffffff",
        },
      });
      map.addLayer({
        id: SELECTED_HEADING_LAYER,
        type: "symbol",
        source: VEHICLES_SOURCE,
        filter: selectedHeadingFilter(selectedKeyRef.current),
        layout: {
          "icon-image": "heading-selected",
          "icon-rotate": ["get", "bearing"],
          "icon-rotation-alignment": "map",
          "icon-allow-overlap": true,
          "icon-ignore-placement": true,
        },
      });

      map.on("mouseenter", VEHICLES_LAYER, () => (map.getCanvas().style.cursor = "pointer"));
      map.on("mouseleave", VEHICLES_LAYER, () => (map.getCanvas().style.cursor = ""));
      map.on("click", (event) => {
        const hit = map.queryRenderedFeatures(event.point, { layers: [VEHICLES_LAYER] })[0];
        const key: unknown = hit?.properties?.key;
        onSelectRef.current(typeof key === "string" ? key : null);
      });

      setStatus("ready");
      updateVehicles();
    });

    // Tile errors have a sourceId and are normal on a flaky network; an error without one
    // means the style or the map itself failed to start.
    map.on("error", (event) => {
      if (!("sourceId" in event) || !event.sourceId) setStatus((s) => (s === "ready" ? s : "error"));
    });

    const unsubscribe = liveVehicleStore.subscribe(scheduleUpdate);

    return () => {
      unsubscribe();
      if (frame !== null) cancelAnimationFrame(frame);
      scheduleUpdateRef.current = () => {};
      mapRef.current = null;
      map.remove();
    };
  }, [attempt]);

  // A new filter needs the same update as a new position.
  useEffect(() => {
    scheduleUpdateRef.current();
  }, [filter, lines]);

  // Selection only changes how the circles are painted; the data is not rebuilt.
  useEffect(() => {
    const map = mapRef.current;
    if (status !== "ready" || !map) return;
    map.setPaintProperty(VEHICLES_LAYER, "circle-radius", selectedExpression(selectedKey, SELECTED_RADIUS, VEHICLE_RADIUS));
    map.setPaintProperty(VEHICLES_LAYER, "circle-color", selectedExpression(selectedKey, SELECTED_COLOR, VEHICLE_COLOR));
    map.setFilter(SELECTED_HEADING_LAYER, selectedHeadingFilter(selectedKey));

    const vehicle = selectedKey ? liveVehicleStore.get(selectedKey) : undefined;
    // Only move the map when the vehicle is out of sight; never on every update.
    if (vehicle && !map.getBounds().contains([vehicle.lon, vehicle.lat])) {
      map.easeTo({ center: [vehicle.lon, vehicle.lat] });
    }
  }, [selectedKey, status]);

  useEffect(() => {
    const map = mapRef.current;
    if (status !== "ready" || !map) return;
    const source = map.getSource<GeoJSONSource>(TRACK_SOURCE);
    const measurements = map.getSource<GeoJSONSource>(MEASUREMENTS_SOURCE);
    if (!source || !measurements) return;
    if (!track || track.path.length < 2) {
      source.setData(EMPTY_COLLECTION);
      measurements.setData(EMPTY_COLLECTION);
      trackFittedRef.current = false;
      return;
    }
    source.setData(lineFeature(track.path));
    measurements.setData({
      type: "FeatureCollection",
      features: track.measurements.map((point) => ({
        type: "Feature",
        properties: {},
        geometry: { type: "Point", coordinates: [point.lon, point.lat] },
      })),
    });
    // The track grows while the vehicle moves. Fit the view once, when the track first appears;
    // after that the user's own panning and zooming is left alone.
    if (trackFittedRef.current) return;
    trackFittedRef.current = true;
    const coordinates = lineCoordinates(track.path);
    const bounds = new LngLatBounds(coordinates[0], coordinates[0]);
    for (const coordinate of coordinates) bounds.extend(coordinate);
    map.fitBounds(bounds, { padding: 48, maxZoom: 15 });
  }, [track, status]);

  useEffect(() => {
    const map = mapRef.current;
    if (status !== "ready" || !map) return;
    const passed = map.getSource<GeoJSONSource>(ROUTE_PASSED_SOURCE);
    const remaining = map.getSource<GeoJSONSource>(ROUTE_REMAINING_SOURCE);
    const stops = map.getSource<GeoJSONSource>(ROUTE_STOPS_SOURCE);
    if (!passed || !remaining || !stops) return;
    if (!route) {
      passed.setData(EMPTY_COLLECTION);
      remaining.setData(EMPTY_COLLECTION);
      stops.setData(EMPTY_COLLECTION);
      routeFittedRef.current = false;
      return;
    }
    passed.setData(lineFeature(route.passed));
    remaining.setData(lineFeature(route.remaining));
    // Dotted instead of dashed when the line only joins stops, so it does not pass for the real track.
    const dashes = route.approximate ? APPROXIMATE_DASHES : ROUTE_DASHES;
    map.setPaintProperty(ROUTE_PASSED_LAYER, "line-dasharray", dashes);
    map.setPaintProperty(ROUTE_REMAINING_LAYER, "line-dasharray", dashes);
    stops.setData({
      type: "FeatureCollection",
      features: route.stops.map((stop) => ({
        type: "Feature",
        properties: { name: stop.name, passed: stop.passed },
        geometry: { type: "Point", coordinates: [stop.lon, stop.lat] },
      })),
    });
    // The route updates with every position. Fit once, when it first appears (the vehicle is
    // included so it stays in view); later updates leave the user's own panning and zooming alone.
    if (routeFittedRef.current) return;
    routeFittedRef.current = true;
    const points = lineCoordinates(route.remaining);
    const vehicle = selectedKeyRef.current ? liveVehicleStore.get(selectedKeyRef.current) : undefined;
    if (vehicle) points.push([vehicle.lon, vehicle.lat]);
    if (points.length === 0) return;
    const bounds = new LngLatBounds(points[0], points[0]);
    for (const point of points) bounds.extend(point);
    map.fitBounds(bounds, { padding: 48, maxZoom: 15 });
  }, [route, status]);

  function retry() {
    setStatus("loading");
    setAttempt((n) => n + 1);
  }

  let overlay: React.ReactNode = null;
  if (status === "error") {
    overlay = <ErrorState message={t("mapError")} onRetry={retry} />;
  } else if (status === "loading" || (connectionStatus === "connecting" && total === 0)) {
    overlay = <LoadingState label={t("loading")} />;
  } else if (total === 0 && (connectionStatus === "reconnecting" || connectionStatus === "disconnected")) {
    // Nothing to show and no connection: say why the map is empty. Retrying is automatic.
    overlay = <ErrorState message={t("noConnection")} />;
  } else if (total > 0 && matching === 0) {
    overlay = <EmptyState title={t("noMatchTitle")} hint={t("noMatchHint")} />;
  } else if (total === 0 && connectionStatus === "connected") {
    overlay = <EmptyState title={t("emptyTitle")} hint={t("emptyHint")} />;
  }

  return (
    <div className="relative h-full w-full">
      {/* Stays mounted in every state; the overlays sit on top of it. */}
      <div ref={containerRef} role="application" aria-label={t("mapLabel")} className="h-full w-full" />
      {overlay ? (
        // Centred, so it never covers the controls the page places along the top edge of the map.
        <div className="absolute inset-x-4 top-1/2 z-10 mx-auto max-w-sm -translate-y-1/2 rounded-lg border bg-background/95 shadow-md">
          {overlay}
        </div>
      ) : null}
    </div>
  );
}
