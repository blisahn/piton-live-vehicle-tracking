import { z } from "zod";

type Translate = (key: "emailRequired" | "emailInvalid" | "passwordRequired" | "passwordShort" | "passwordMismatch" | "confirmRequired") => string;

export const MIN_PASSWORD_LENGTH = 8;

const email = (t: Translate) =>
  z.string().trim().min(1, t("emailRequired")).pipe(z.email(t("emailInvalid")));

export const createLoginSchema = (t: Translate) =>
  z.object({ email: email(t), password: z.string().min(1, t("passwordRequired")) });

export const createRegisterSchema = (t: Translate) =>
  z
    .object({
      email: email(t),
      password: z.string().min(1, t("passwordRequired")).min(MIN_PASSWORD_LENGTH, t("passwordShort")),
      confirmPassword: z.string().min(1, t("confirmRequired")),
    })
    .refine((v) => v.password === v.confirmPassword, { path: ["confirmPassword"], message: t("passwordMismatch") });

/** Field name → first message, for showing next to the inputs. */
export function fieldErrors(error: z.ZodError): Record<string, string> {
  const result: Record<string, string> = {};
  for (const issue of error.issues) {
    const field = String(issue.path[0] ?? "");
    result[field] ??= issue.message;
  }
  return result;
}
