"use client";

import { useTranslations } from "next-intl";
import { useSearchParams } from "next/navigation";
import { useState } from "react";
import { login, safeNextPath } from "@/entities/session";
import { Link, useRouter } from "@/i18n/navigation";
import { authErrorKey } from "../model/errors";
import { createLoginSchema, fieldErrors } from "../model/schemas";
import { AuthForm } from "./auth-form";

export function LoginForm() {
  const t = useTranslations("auth");
  const router = useRouter();
  const nextParam = useSearchParams().get("next");
  const [values, setValues] = useState({ email: "", password: "" });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [serverError, setServerError] = useState<string>();
  const [pending, setPending] = useState(false);

  async function submit() {
    const parsed = createLoginSchema((key) => t(`validation.${key}`)).safeParse(values);
    if (!parsed.success) {
      setErrors(fieldErrors(parsed.error));
      return;
    }
    setErrors({});
    setServerError(undefined);
    setPending(true);
    try {
      await login(parsed.data);
      router.replace(safeNextPath(nextParam));
    } catch (error) {
      setServerError(t(`errors.${authErrorKey(error, "login")}`));
      setPending(false);
    }
  }

  return (
    <AuthForm
      title={t("login.title")}
      description={t("login.description")}
      submitLabel={pending ? t("login.submitting") : t("login.submit")}
      pending={pending}
      serverError={serverError}
      onSubmit={() => void submit()}
      fields={[
        { name: "email", label: t("fields.email"), type: "email", autoComplete: "email", value: values.email, error: errors.email, onChange: (email) => setValues((v) => ({ ...v, email })) },
        { name: "password", label: t("fields.password"), type: "password", autoComplete: "current-password", value: values.password, error: errors.password, onChange: (password) => setValues((v) => ({ ...v, password })) },
      ]}
      footer={
        <p>
          {t("login.noAccount")}{" "}
          <Link href={nextParam ? `/register?next=${encodeURIComponent(nextParam)}` : "/register"} className="font-medium text-foreground underline">
            {t("login.toRegister")}
          </Link>
        </p>
      }
    />
  );
}
