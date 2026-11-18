import { screen, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { currentUrl, setUrl } from "@/test/navigation";
import { renderWithProviders } from "@/test/render";
import { api, server } from "@/test/server";
import type { ServiceAlert } from "../api/alerts";
import { AlertsPanel } from "./alerts-panel";

const LINE = "ATB:Line:2_74";
const STOP = "NSR:Quay:73978";

const alert = (id: string, overrides: Partial<ServiceAlert> = {}): ServiceAlert => ({
  id,
  active: true,
  hasInvalidPeriod: false,
  activePeriods: [],
  lineIds: [LINE],
  stopIds: [STOP],
  tripIds: [],
  header: [
    { text: `Bussen er forsinket ${id}`, language: "no", languageDetected: true },
    { text: `The bus is delayed ${id}`, language: "en", languageDetected: true },
  ],
  description: [],
  url: null,
  ...overrides,
});

/** A fake backend for the alerts screen. Records the query string of every alerts request. */
function backend(alerts: () => ServiceAlert[] | Response) {
  const requests: URLSearchParams[] = [];
  server.use(
    http.get(api("/api/alerts"), ({ request }) => {
      requests.push(new URL(request.url).searchParams);
      const result = alerts();
      return result instanceof Response ? result : HttpResponse.json({ version: 1, generatedAt: null, alerts: result });
    }),
    http.get(api("/api/lines"), () =>
      HttpResponse.json({ lines: [{ id: LINE, publicCode: "74", name: "Lade", transportMode: "bus" }] }),
    ),
    http.get(api("/api/stops"), () =>
      HttpResponse.json({ stops: [{ id: STOP, name: "Trondheim S", lat: 63.43, lon: 10.4 }] }),
    ),
  );
  return requests;
}

describe("AlertsPanel", () => {
  it("lists the alerts with line and stop names, in English for a Turkish reader", async () => {
    backend(() => [alert("a1")]);
    setUrl("/tr/alerts");
    renderWithProviders(<AlertsPanel />);

    const card = (await screen.findByRole("heading", { name: "The bus is delayed a1" })).closest("li")!;
    expect(screen.getByRole("status")).toHaveTextContent("1 uyarı");
    // There is no Turkish text in the feed: the English one is shown, marked, with a translate link.
    expect(within(card).getByText("İngilizce orijinal")).toBeInTheDocument();
    expect(within(card).getByRole("link", { name: "Google Translate'te çevir" })).toHaveAttribute(
      "href",
      expect.stringContaining("tl=tr"),
    );
    expect(await within(card).findByText(/Trondheim S/)).toBeInTheDocument();
    expect(within(card).getByRole("list", { name: "Hatlar" })).toHaveTextContent("74");
  });

  it("takes the line filter from the URL, sends it to the server and clears it", async () => {
    const requests = backend(() => [alert("a1")]);
    setUrl(`/tr/alerts?line=${encodeURIComponent(LINE)}`);
    const { user } = renderWithProviders(<AlertsPanel />);

    await screen.findByRole("heading", { name: "The bus is delayed a1" });
    expect(requests.at(-1)?.get("lineId")).toBe(LINE);

    await user.click(screen.getByRole("button", { name: "Filtreleri temizle" }));

    expect(currentUrl().search).toBe("");
    await expect.poll(() => requests.at(-1)?.get("lineId") ?? null).toBeNull();
    expect(screen.queryByRole("button", { name: "Filtreleri temizle" })).not.toBeInTheDocument();
  });

  it("keeps inactive alerts in the URL and asks the server for them", async () => {
    const requests = backend(() => [alert("a1")]);
    setUrl("/tr/alerts");
    const { user } = renderWithProviders(<AlertsPanel />);
    await screen.findByRole("heading", { name: "The bus is delayed a1" });
    expect(requests.at(-1)?.get("includeInactive")).toBe("false");

    await user.click(screen.getByRole("checkbox", { name: "Etkin olmayan uyarıları göster" }));

    expect(currentUrl().searchParams.get("inactive")).toBe("1");
    await expect.poll(() => requests.at(-1)?.get("includeInactive")).toBe("true");
  });

  it("shows the empty state when nothing matches", async () => {
    backend(() => []);
    setUrl("/tr/alerts?stop=NSR%3AQuay%3A1");
    renderWithProviders(<AlertsPanel />);

    expect(await screen.findByText("Bu seçim için uyarı bulunmuyor.")).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent("Uyarı yok");
  });

  it("shows the error with a retry when the first load fails", async () => {
    let up = false;
    backend(() => (up ? [alert("a1")] : HttpResponse.json({ detail: "Alerts unavailable" }, { status: 503 })));
    setUrl("/tr/alerts");
    const { user } = renderWithProviders(<AlertsPanel />);

    expect(await screen.findByText("Alerts unavailable")).toBeInTheDocument();
    up = true;
    await user.click(screen.getByRole("button", { name: "Tekrar dene" }));
    expect(await screen.findByRole("heading", { name: "The bus is delayed a1" })).toBeInTheDocument();
  });

  it("marks an alert that arrives with a live update as new, and keeps the list if a refresh fails", async () => {
    let state: "first" | "second" | "down" = "first";
    backend(() =>
      state === "first"
        ? [alert("a1")]
        : state === "second"
          ? [alert("a1"), alert("a2")]
          : HttpResponse.json({ detail: "down" }, { status: 503 }),
    );
    setUrl("/tr/alerts");
    const { queryClient } = renderWithProviders(<AlertsPanel />);
    await screen.findByRole("heading", { name: "The bus is delayed a1" });
    expect(screen.queryByText("Yeni")).not.toBeInTheDocument();

    // A new alerts version from the WebSocket changes the query key; invalidating does the same refetch.
    state = "second";
    await queryClient.invalidateQueries({ queryKey: ["alerts"] });

    const fresh = (await screen.findByRole("heading", { name: "The bus is delayed a2" })).closest("li")!;
    expect(within(fresh).getByText("Yeni")).toBeInTheDocument();
    const old = screen.getByRole("heading", { name: "The bus is delayed a1" }).closest("li")!;
    expect(within(old).queryByText("Yeni")).not.toBeInTheDocument();

    state = "down";
    await queryClient.invalidateQueries({ queryKey: ["alerts"] });
    expect(await screen.findByText(/Son yenileme başarısız oldu/)).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "The bus is delayed a2" })).toBeInTheDocument();
  });
});
