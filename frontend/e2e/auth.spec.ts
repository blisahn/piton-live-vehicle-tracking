import { expect, test as anonymous } from "@playwright/test";
import { API_URL, test } from "./fixtures";

// A fresh context: no cookie, nobody signed in.
// (uses plain Playwright test, not the signed-in fixture)
anonymous.describe("anonymous visitor", () => {
  anonymous.use({ storageState: { cookies: [], origins: [] } });

  anonymous("is sent to the login screen and, after registering, sees the map but no admin features", async ({ page }) => {
    await page.goto("/tr");
    await expect(page).toHaveURL(/\/tr\/login/);

    await page.getByRole("link", { name: "Kayıt ol" }).click();
    await expect(page).toHaveURL(/\/tr\/register/);

    const email = `e2e-${Date.now()}-${Math.floor(Math.random() * 1e6)}@example.com`;
    await page.getByLabel("E-posta").fill(email);
    await page.getByLabel("Şifre", { exact: true }).fill("e2e-password-1");
    await page.getByLabel("Şifre (tekrar)").fill("e2e-password-1");
    await page.getByRole("button", { name: "Kayıt ol" }).click();

    await expect(page).toHaveURL(/\/tr\/?$/);
    await expect(page.getByRole("application")).toBeVisible();
    await expect(page.getByRole("link", { name: /Sistem durumu|Durum/ })).toHaveCount(0);

    await page.goto("/tr/status");
    await expect(page.getByText("Bu sayfayı görme yetkiniz yok")).toBeVisible();

    await page.getByRole("button", { name: "Çıkış" }).click();
    await expect(page).toHaveURL(/\/tr\/login/);
  });

  anonymous("wrong credentials show an error and stay on the login screen", async ({ page }) => {
    await page.goto("/en/login");
    await page.getByLabel("E-mail").fill("nobody@example.com");
    await page.getByLabel("Password").fill("wrong-password");
    await page.getByRole("button", { name: "Log in" }).click();
    // Next.js adds its own role="alert" route announcer, so look for our message by its text.
    await expect(page.getByRole("alert").filter({ hasText: "The e-mail or password is wrong." })).toBeVisible();
  });

  anonymous("can open Swagger, while the endpoints it describes stay protected", async ({ request }) => {
    expect((await request.get(`${API_URL}/swagger/index.html`)).status()).toBe(200);
    expect((await request.get(`${API_URL}/swagger/v1/swagger.json`)).status()).toBe(200);
    expect((await request.get(`${API_URL}/api/vehicles`)).status()).toBe(401);
  });
});

test("an admin sees the system status", async ({ page }) => {
  await page.goto("/en/status");
  await expect(page.getByRole("link", { name: /System status/ })).toBeVisible();
  await expect(page.getByText("You are not allowed to view this page")).toHaveCount(0);
  await expect(page.getByText(/Last successful read|Healthy|Degraded|Down/).first()).toBeVisible();
});
