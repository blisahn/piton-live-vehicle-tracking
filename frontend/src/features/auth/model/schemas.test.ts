import { describe, expect, it } from "vitest";
import { createRegisterSchema, fieldErrors } from "./schemas";

const schema = createRegisterSchema((key) => key);

describe("register schema", () => {
  it("accepts valid input", () => {
    expect(schema.safeParse({ email: "a@b.co", password: "12345678", confirmPassword: "12345678" }).success).toBe(true);
  });
  it("reports each field", () => {
    const result = schema.safeParse({ email: "nope", password: "123", confirmPassword: "" });
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(fieldErrors(result.error)).toMatchObject({ email: "emailInvalid", password: "passwordShort", confirmPassword: "confirmRequired" });
    }
  });
  it("requires matching passwords", () => {
    const result = schema.safeParse({ email: "a@b.co", password: "12345678", confirmPassword: "12345679" });
    expect(!result.success && fieldErrors(result.error).confirmPassword).toBe("passwordMismatch");
  });
});
