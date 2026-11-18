import { screen } from "@testing-library/react";
import { delay, http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { renderWithProviders } from "@/test/render";
import { api, server } from "@/test/server";
import type { SystemStatus } from "../api/status";
import { StatusPanel } from "./status-panel";

const status = (overrides: Partial<SystemStatus> = {}): SystemStatus => ({
  serverTime: "2026-11-18T12:00:05Z",
  lastSuccessfulReadAt: "2026-11-18T12:00:02Z",
  lastReadDurationMs: 240,
  lastEntityCount: 8400,
  lastChangedCount: 650,
  lastRejectedCount: 4,
  successfulReads: 1200,
  failedReads: 3,
  rowsWrittenLastHour: 580000,
  databaseAvailable: true,
  pendingSpoolBatches: 0,
  connectedClients: 2,
  trackedVehicles: 3400,
  ...overrides,
});

/** The <dl> card that carries this label. */
const card = (label: string) => screen.getByText(label).closest("dl");

describe("StatusPanel", () => {
  it("shows the four numbers the status endpoint reports", async () => {
    server.use(http.get(api("/api/status"), () => HttpResponse.json(status())));
    renderWithProviders(<StatusPanel />);

    expect(await screen.findByText("Son bir saatte yazılan satır")).toBeInTheDocument();
    expect(card("Son bir saatte yazılan satır")).toHaveTextContent("580.000");
    expect(card("Başarısız okumalar")).toHaveTextContent("3");
    expect(card("Bağlı istemciler")).toHaveTextContent("2");
    // Measured between two server timestamps, not against the browser clock.
    expect(card("Son başarılı okuma")).toHaveTextContent("3 saniye önce");
  });

  it("shows the loading state until the first answer arrives", async () => {
    server.use(
      http.get(api("/api/status"), async () => {
        await delay("infinite");
        return HttpResponse.json(status());
      }),
    );
    renderWithProviders(<StatusPanel />);

    expect(await screen.findByText("Yükleniyor…")).toBeInTheDocument();
    expect(screen.queryByText("Son bir saatte yazılan satır")).not.toBeInTheDocument();
  });

  it("says the row count is unknown while the database is unreachable", async () => {
    server.use(
      http.get(api("/api/status"), () =>
        HttpResponse.json(status({ rowsWrittenLastHour: null, databaseAvailable: false, pendingSpoolBatches: 8 })),
      ),
    );
    renderWithProviders(<StatusPanel />);

    expect(await screen.findByText("Veritabanına ulaşılamadığı için bilinmiyor")).toBeInTheDocument();
    expect(card("Son bir saatte yazılan satır")).toHaveTextContent("—");
    expect(screen.getByText("Kısmen bozuk")).toBeInTheDocument();
  });

  it("shows the empty state before the first successful read", async () => {
    server.use(http.get(api("/api/status"), () => HttpResponse.json(status({ lastSuccessfulReadAt: null }))));
    renderWithProviders(<StatusPanel />);

    expect(await screen.findByText("Henüz başarılı okuma yok")).toBeInTheDocument();
  });

  it("tells a non-admin they may not see the page instead of showing an error", async () => {
    server.use(http.get(api("/api/status"), () => HttpResponse.json({ detail: "Forbidden" }, { status: 403 })));
    renderWithProviders(<StatusPanel />);

    expect(await screen.findByText("Bu sayfayı görme yetkiniz yok")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /tekrar/i })).not.toBeInTheDocument();
  });

  it("shows the error with a retry that loads the data once the server is back", async () => {
    let up = false;
    server.use(
      http.get(api("/api/status"), () =>
        up ? HttpResponse.json(status()) : HttpResponse.json({ detail: "Service unavailable" }, { status: 503 }),
      ),
    );
    const { user } = renderWithProviders(<StatusPanel />);

    expect(await screen.findByText("Service unavailable")).toBeInTheDocument();
    up = true;
    await user.click(screen.getByRole("button", { name: /tekrar/i }));
    expect(await screen.findByText("Son bir saatte yazılan satır")).toBeInTheDocument();
  });
});
