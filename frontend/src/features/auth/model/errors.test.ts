import { describe, expect, it } from "vitest";
import { ApiError } from "@/shared/api/http";
import { authErrorKey } from "./errors";

describe("authErrorKey", () => {
  it("maps server answers to messages", () => {
    expect(authErrorKey(new ApiError(401, ""), "login")).toBe("invalidCredentials");
    expect(authErrorKey(new ApiError(409, ""), "register")).toBe("emailTaken");
    expect(authErrorKey(new ApiError(429, ""), "login")).toBe("tooManyAttempts");
    expect(authErrorKey(new ApiError(400, ""), "register")).toBe("invalidInput");
    expect(authErrorKey(new ApiError(500, ""), "login")).toBe("generic");
    expect(authErrorKey(new TypeError("network"), "login")).toBe("generic");
  });
});
