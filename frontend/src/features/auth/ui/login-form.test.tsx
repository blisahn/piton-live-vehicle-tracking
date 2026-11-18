import { screen } from "@testing-library/react";
import { delay, http, HttpResponse } from "msw";
import { afterEach, describe, expect, it } from "vitest";
import { useSessionStore } from "@/entities/session";
import { router, setUrl } from "@/test/navigation";
import { renderWithProviders } from "@/test/render";
import { api, server } from "@/test/server";
import { LoginForm } from "./login-form";

const admin = { id: "u1", email: "admin@example.com", role: "admin" as const };
const signedIn = { accessToken: "token-1", expiresAt: "2099-01-01T00:00:00Z", user: admin };

async function fillAndSubmit(user: ReturnType<typeof renderWithProviders>["user"], email: string, password: string) {
  await user.type(screen.getByLabelText("E-posta"), email);
  await user.type(screen.getByLabelText("Şifre"), password);
  await user.click(screen.getByRole("button", { name: "Giriş yap" }));
}

describe("LoginForm", () => {
  afterEach(() => useSessionStore.getState().clear());

  it("checks the fields in the browser and sends nothing while they are invalid", async () => {
    let requests = 0;
    server.use(http.post(api("/api/auth/login"), () => void requests++));
    const { user } = renderWithProviders(<LoginForm />);

    await user.click(screen.getByRole("button", { name: "Giriş yap" }));
    expect(screen.getByText("E-posta adresinizi girin.")).toBeInTheDocument();
    expect(screen.getByText("Şifrenizi girin.")).toBeInTheDocument();
    expect(screen.getByLabelText("E-posta")).toHaveAttribute("aria-invalid", "true");

    await user.type(screen.getByLabelText("E-posta"), "not-an-email");
    await user.click(screen.getByRole("button", { name: "Giriş yap" }));
    expect(screen.getByText("Geçerli bir e-posta adresi girin.")).toBeInTheDocument();
    expect(requests).toBe(0);
  });

  it("signs in, keeps the session and goes to the page the user came from", async () => {
    let body: unknown;
    server.use(
      http.post(api("/api/auth/login"), async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(signedIn);
      }),
    );
    setUrl("/tr/login?next=%2Ftr%2Falerts");
    const { user } = renderWithProviders(<LoginForm />);

    await fillAndSubmit(user, "admin@example.com", "admin12345");

    await expect.poll(() => router.replace.mock.calls.length).toBe(1);
    expect(router.replace).toHaveBeenCalledWith("/alerts");
    expect(body).toEqual({ email: "admin@example.com", password: "admin12345" });
    expect(useSessionStore.getState().user).toEqual(admin);
    expect(useSessionStore.getState().accessToken).toBe("token-1");
  });

  it("disables the button while signing in, so a double click sends one request", async () => {
    let requests = 0;
    server.use(
      http.post(api("/api/auth/login"), async () => {
        requests++;
        await delay(50);
        return HttpResponse.json(signedIn);
      }),
    );
    const { user } = renderWithProviders(<LoginForm />);

    await fillAndSubmit(user, "admin@example.com", "admin12345");
    const button = screen.getByRole("button", { name: "Giriş yapılıyor…" });
    expect(button).toBeDisabled();
    await user.click(button);

    await expect.poll(() => router.replace.mock.calls.length).toBe(1);
    expect(requests).toBe(1);
  });

  it("says the credentials are wrong on 401 and stays on the form", async () => {
    server.use(http.post(api("/api/auth/login"), () => HttpResponse.json({ detail: "Invalid" }, { status: 401 })));
    const { user } = renderWithProviders(<LoginForm />);

    await fillAndSubmit(user, "admin@example.com", "wrong-password");

    expect(await screen.findByRole("alert")).toHaveTextContent("E-posta ya da şifre hatalı.");
    expect(screen.getByRole("button", { name: "Giriş yap" })).toBeEnabled();
    expect(router.replace).not.toHaveBeenCalled();
    expect(useSessionStore.getState().user).toBeNull();
  });

  it("asks the user to wait when the server limits failed attempts (429)", async () => {
    server.use(http.post(api("/api/auth/login"), () => HttpResponse.json({ detail: "Too many" }, { status: 429 })));
    const { user } = renderWithProviders(<LoginForm />);

    await fillAndSubmit(user, "admin@example.com", "wrong-password");

    expect(await screen.findByRole("alert")).toHaveTextContent("Çok fazla deneme yapıldı.");
  });

  it("shows a general message when the server cannot be reached", async () => {
    server.use(http.post(api("/api/auth/login"), () => HttpResponse.error()));
    const { user } = renderWithProviders(<LoginForm />);

    await fillAndSubmit(user, "admin@example.com", "admin12345");

    expect(await screen.findByRole("alert")).toHaveTextContent("Bir sorun oluştu. Tekrar deneyin.");
  });
});
