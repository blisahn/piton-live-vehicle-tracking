import { ApiError } from "@/shared/api/http";

export type AuthErrorKey = "invalidCredentials" | "emailTaken" | "tooManyAttempts" | "invalidInput" | "generic";

/** Which message to show for a failed login or registration. */
export function authErrorKey(error: unknown, form: "login" | "register"): AuthErrorKey {
  if (!(error instanceof ApiError)) return "generic";
  switch (error.status) {
    case 401:
      return "invalidCredentials";
    case 409:
      return "emailTaken";
    case 429:
      return "tooManyAttempts";
    case 400:
      return form === "register" ? "invalidInput" : "invalidCredentials";
    default:
      return "generic";
  }
}
