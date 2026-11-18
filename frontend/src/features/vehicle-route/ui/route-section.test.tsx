import { act, screen, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { afterEach, describe, expect, it, vi } from "vitest";
import { liveVehicleStore, type Vehicle } from "@/entities/vehicle";
import { renderWithProviders } from "@/test/render";
import { api, server } from "@/test/server";
import type { ProgressStop, TripProgress } from "../api/progress";
import type { TripRoute } from "../api/route";
import { RouteSection } from "./route-section";

const KEY = "ATB:2667";
const TRIP = "ATB:ServiceJourney:74_1";

// Four stops on a straight north-bound street in Trondheim.
const stops = [
  { id: "NSR:Quay:1", name: "Lade", lat: 63.44, lon: 10.44 },
  { id: "NSR:Quay:2", name: "Steine", lat: 63.441, lon: 10.44 },
  { id: "NSR:Quay:3", name: "Rotvoll", lat: 63.442, lon: 10.44 },
  { id: "NSR:Quay:4", name: "Ranheim", lat: 63.443, lon: 10.44 },
];

const route: TripRoute = {
  tripId: TRIP,
  line: { id: "ATB:Line:2_74", publicCode: "74", name: "Lade", transportMode: "bus" },
  path: stops.map(({ lat, lon }) => ({ lat, lon })),
  stops,
  pathIsApproximate: false,
};

const vehicle = (overrides: Partial<Vehicle> = {}): Vehicle => ({
  key: KEY,
  recordedAt: "2026-11-18T14:40:00Z",
  // Between Lade and Steine, heading to Steine.
  lat: 63.4405,
  lon: 10.44,
  bearing: 0,
  speed: null,
  routeId: "ATB:Line:2_74",
  tripId: TRIP,
  stopId: "NSR:Quay:2",
  status: 2,
  occupancy: null,
  ...overrides,
});

// Norway is UTC+1 in November: 14:41Z is 15:41 on screen.
const progressStop = (sequence: number, aimed: string, expected: string): ProgressStop => ({
  sequence,
  stopId: stops[sequence - 1].id,
  name: stops[sequence - 1].name,
  aimedArrival: aimed,
  expectedArrival: expected,
  aimedDeparture: null,
  expectedDeparture: null,
  arrivalDelaySeconds: null,
  departureDelaySeconds: null,
  skipped: false,
});

const progress = (overrides: Partial<TripProgress> = {}): TripProgress => ({
  tripId: TRIP,
  startDate: "2026-11-18",
  updatedAt: "2026-11-18T14:40:00Z",
  canceled: false,
  delaySeconds: 240,
  stops: [
    progressStop(2, "2026-11-18T14:41:00Z", "2026-11-18T14:45:00Z"),
    progressStop(3, "2026-11-18T14:43:00Z", "2026-11-18T14:47:00Z"),
    progressStop(4, "2026-11-18T14:45:00Z", "2026-11-18T14:49:00Z"),
  ],
  ...overrides,
});

const progressUrl = api(`/api/trips/${encodeURIComponent(TRIP)}/progress`);
const routeUrl = api(`/api/trips/${encodeURIComponent(TRIP)}/route`);

/** Puts the vehicle in the live store the way the WebSocket snapshot does. */
function seed(v: Vehicle | null, seq = 1) {
  act(() => {
    liveVehicleStore.apply({ type: "snapshot", seq, vehicles: v ? [v] : [] });
  });
}

function renderSection() {
  const onRouteChange = vi.fn();
  return { onRouteChange, ...renderWithProviders(<RouteSection vehicleKey={KEY} onRouteChange={onRouteChange} />) };
}

describe("RouteSection", () => {
  afterEach(() => seed(null));

  it("shows how late the trip is next to the title, before the route is opened", async () => {
    server.use(http.get(progressUrl, () => HttpResponse.json(progress())));
    seed(vehicle());
    renderSection();

    expect(await screen.findByText("4 dk gecikmeli")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Güzergahı göster" })).toHaveAttribute("aria-pressed", "false");
  });

  it("says there is no realtime schedule when the trip has none (404 is not an error)", async () => {
    server.use(http.get(progressUrl, () => HttpResponse.json({ detail: "Not found" }, { status: 404 })));
    seed(vehicle());
    renderSection();

    expect(await screen.findByText("Gerçek zamanlı tarife yok")).toBeInTheDocument();
  });

  it("says the schedule is unavailable when the server fails, without blocking the section", async () => {
    server.use(http.get(progressUrl, () => HttpResponse.json({ detail: "Boom" }, { status: 500 })));
    seed(vehicle());
    renderSection();

    // The progress query retries once (about a second) before it gives up.
    expect(await screen.findByText("Tarife kullanılamıyor", undefined, { timeout: 4000 })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Güzergahı göster" })).toBeEnabled();
  });

  it("opens the route with the next stops and their planned → expected times, and draws it on the map", async () => {
    server.use(
      http.get(progressUrl, () => HttpResponse.json(progress())),
      http.get(routeUrl, () => HttpResponse.json(route)),
    );
    seed(vehicle());
    const { user, onRouteChange } = renderSection();

    await user.click(screen.getByRole("button", { name: "Güzergahı göster" }));

    const list = await screen.findByRole("list");
    const items = within(list).getAllByRole("listitem");
    expect(items.map((li) => li.textContent?.match(/^\D+/)?.[0])).toEqual(["Steine", "Rotvoll", "Ranheim"]);
    expect(items[0]).toHaveTextContent("15:41");
    expect(items[0]).toHaveTextContent("15:45");
    expect(screen.getByText(/Hat:/).parentElement).toHaveTextContent("74");

    const overlay = onRouteChange.mock.calls.at(-1)?.[0];
    expect(overlay.stops.map((s: { name: string; passed: boolean }) => [s.name, s.passed])).toEqual([
      ["Lade", true],
      ["Steine", false],
      ["Rotvoll", false],
      ["Ranheim", false],
    ]);
  });

  it("updates the next stops live as the vehicle moves on", async () => {
    server.use(
      http.get(progressUrl, () => HttpResponse.json(progress())),
      http.get(routeUrl, () => HttpResponse.json(route)),
    );
    seed(vehicle());
    const { user } = renderSection();
    await user.click(screen.getByRole("button", { name: "Güzergahı göster" }));
    await screen.findByText("Steine");

    act(() => {
      liveVehicleStore.apply({
        type: "delta",
        fromSeq: 2,
        toSeq: 2,
        upserted: [vehicle({ lat: 63.4415, stopId: "NSR:Quay:3", recordedAt: "2026-11-18T14:42:00Z" })],
        removed: [],
      });
    });

    expect(screen.queryByText("Steine")).not.toBeInTheDocument();
    expect(within(screen.getByRole("list")).getAllByRole("listitem")).toHaveLength(2);
  });

  it("says when the trip has no planned route (404)", async () => {
    server.use(
      http.get(progressUrl, () => HttpResponse.json(progress())),
      http.get(routeUrl, () => HttpResponse.json({ detail: "Not found" }, { status: 404 })),
    );
    seed(vehicle());
    const { user } = renderSection();

    await user.click(screen.getByRole("button", { name: "Güzergahı göster" }));

    expect(await screen.findByText("Bu sefer için planlanmış güzergah mevcut değil")).toBeInTheDocument();
  });

  it("asks nothing from the server for a vehicle that does not report its trip", async () => {
    // No handlers: any request would fail the test (onUnhandledRequest: "error").
    seed(vehicle({ tripId: null }));
    const { user } = renderSection();

    await user.click(screen.getByRole("button", { name: "Güzergahı göster" }));

    expect(screen.getByText("Sefer bilgisi yok")).toBeInTheDocument();
    expect(screen.queryByText("Gerçek zamanlı tarife yok")).not.toBeInTheDocument();
  });
});
