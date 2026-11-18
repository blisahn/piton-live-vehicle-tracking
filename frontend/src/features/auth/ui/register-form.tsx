"use client";

import { useTranslations } from "next-intl";
import { useSearchParams } from "next/navigation";
import { useState } from "react";
import { register, safeNextPath } from "@/entities/session";
import { Link, useRouter } from "@/i18n/navigation";
import { authErrorKey } from "../model/errors";
import { createRegisterSchema, fieldErrors } from "../model/schemas";
import { AuthForm } from "./auth-form";

export function RegisterForm() {
  const t = useTranslations("auth");
  const router = useRouter();
  const nextParam = useSearchParams().get("next");
  const [values, setValues] = useState({ email: "", password: "", confirmPassword: "" });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [serverError, setServerError] = useState<string>();
  const [pending, setPending] = useState(false);

  async function submit() {
    const parsed = createRegisterSchema((key) => t(`validation.${key}`)).safeParse(values);
    if (!parsed.success) {
      setErrors(fieldErrors(parsed.error));
      return;
    }
    setErrors({});
    setServerError(undefined);
    setPending(true);
    try {
      await register({ email: parsed.data.email, password: parsed.data.password });
      router.replace(safeNextPath(nextParam));
    } catch (error) {
      setServerError(t(`errors.${authErrorKey(error, "register")}`));
      setPending(false);
    }
  }

  const set = (field: keyof typeof values) => (value: string) => setValues((v) => ({ ...v, [field]: value }));

  return (
    <AuthForm
      title={t("register.title")}
      description={t("register.description")}
      submitLabel={pending ? t("register.submitting") : t("register.submit")}
      pending={pending}
      serverError={serverError}
      onSubmit={() => void submit()}
      fields={[
        { name: "email", label: t("fields.email"), type: "email", autoComplete: "email", value: values.email, error: errors.email, onChange: set("email") },
        { name: "password", label: t("fields.password"), type: "password", autoComplete: "new-password", value: values.password, error: errors.password, onChange: set("password") },
        { name: "confirmPassword", label: t("fields.confirmPassword"), type: "password", autoComplete: "new-password", value: values.confirmPassword, error: errors.confirmPassword, onChange: set("confirmPassword") },
      ]}
      footer={
        <p>
          {t("register.haveAccount")}{" "}
          <Link href={nextParam ? `/login?next=${encodeURIComponent(nextParam)}` : "/login"} className="font-medium text-foreground underline">
            {t("register.toLogin")}
          </Link>
        </p>
      }
    />
  );
}
