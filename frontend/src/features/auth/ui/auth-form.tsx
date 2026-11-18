"use client";

import { useId, type FormEvent, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";

export type FieldSpec = {
  name: string;
  label: string;
  type: "email" | "password";
  autoComplete: string;
  value: string;
  error?: string;
  onChange: (value: string) => void;
};

/** The card shared by the login and register forms: fields, submit button, server error and the link to the other form. */
export function AuthForm({
  title,
  description,
  fields,
  submitLabel,
  pending,
  serverError,
  onSubmit,
  footer,
}: {
  title: string;
  description: string;
  fields: FieldSpec[];
  submitLabel: string;
  pending: boolean;
  serverError?: string;
  onSubmit: () => void;
  footer: ReactNode;
}) {
  const id = useId();

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!pending) onSubmit();
  }

  return (
    <div className="flex min-h-0 flex-1 items-start justify-center overflow-y-auto p-4 sm:items-center sm:p-6">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle role="heading" aria-level={1} className="text-lg">
            {title}
          </CardTitle>
          <CardDescription>{description}</CardDescription>
        </CardHeader>
        <form onSubmit={handleSubmit} noValidate aria-label={title}>
          <CardContent className="flex flex-col gap-3">
            {fields.map((field) => {
              const inputId = `${id}-${field.name}`;
              return (
                <div key={field.name} className="flex flex-col gap-1">
                  <label htmlFor={inputId} className="text-sm font-medium">
                    {field.label}
                  </label>
                  <Input
                    id={inputId}
                    name={field.name}
                    type={field.type}
                    autoComplete={field.autoComplete}
                    value={field.value}
                    onChange={(e) => field.onChange(e.target.value)}
                    aria-invalid={field.error ? true : undefined}
                    aria-describedby={field.error ? `${inputId}-error` : undefined}
                    className="h-10 sm:h-8"
                  />
                  {field.error ? (
                    <p id={`${inputId}-error`} className="text-xs text-destructive">
                      {field.error}
                    </p>
                  ) : null}
                </div>
              );
            })}
            {serverError ? (
              <p role="alert" className="text-sm text-destructive">
                {serverError}
              </p>
            ) : null}
            <Button type="submit" disabled={pending} aria-busy={pending} className="h-10 sm:h-8">
              {submitLabel}
            </Button>
          </CardContent>
        </form>
        <CardFooter className="text-sm text-muted-foreground">
          {footer}
        </CardFooter>
      </Card>
    </div>
  );
}
