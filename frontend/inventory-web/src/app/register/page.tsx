"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { apiRequest } from "@/lib/api-client";
import { AuthResponse } from "@/lib/types";
import { useAuth } from "@/context/auth-context";
import { ErrorBanner } from "@/components/ui/enterprise-ui";

const registerSchema = z.object({
  fullName: z.string().min(2, "Full name must be at least 2 characters."),
  email: z.string().email("Enter a valid email address."),
  password: z
    .string()
    .min(8, "Password must be at least 8 characters.")
    .regex(/[A-Z]/, "Must contain an uppercase letter.")
    .regex(/[a-z]/, "Must contain a lowercase letter.")
    .regex(/[0-9]/, "Must contain a number."),
  organizationName: z
    .string()
    .min(2, "Organization name is required to initialize your workspace."),
  baseCurrency: z.string().length(3, "Use a 3-letter currency code (e.g., USD).")
});

type RegisterFormValues = z.infer<typeof registerSchema>;

export default function RegisterPage() {
  const { setSession } = useAuth();
  const router = useRouter();
  const [serverError, setServerError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting }
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      fullName: "",
      email: "",
      password: "",
      organizationName: "",
      baseCurrency: "USD"
    }
  });

  const onSubmit = async (values: RegisterFormValues) => {
    setServerError(null);
    try {
      const response = await apiRequest<AuthResponse>("/auth/register", {
        method: "POST",
        skipAuth: true,
        body: JSON.stringify({
          ...values,
          timeZoneId: "UTC"
        })
      });
      setSession(response);
      router.push(response.activeTenantId ? "/dashboard" : "/onboarding");
    } catch (err) {
      setServerError(
        err instanceof Error ? err.message : "Registration failed."
      );
    }
  };

  return (
    <div className="flex min-h-screen flex-col justify-center bg-slate-100 py-12 sm:px-6 lg:px-8">
      <div className="sm:mx-auto sm:w-full sm:max-w-md">
        <div className="mx-auto flex h-10 w-10 items-center justify-center rounded-md bg-slate-900 text-sm font-bold text-white">
          EI
        </div>
        <h1 className="mt-4 text-center text-2xl font-semibold tracking-tight text-slate-900">
          Register Organization Account
        </h1>
        <p className="mt-1 text-center text-sm text-slate-600">
          Provision an isolated multi-tenant inventory workspace
        </p>
      </div>

      <div className="mt-8 sm:mx-auto sm:w-full sm:max-w-md">
        <div className="rounded-lg border border-slate-200 bg-white px-6 py-8 shadow-sm sm:px-8">
          <ErrorBanner message={serverError} />

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div>
              <label className="block text-xs font-medium text-slate-700">
                Full Name
              </label>
              <input
                type="text"
                {...register("fullName")}
                placeholder="Elena Vance"
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-blue-600 focus:outline-none"
              />
              {errors.fullName && (
                <p className="mt-1 text-xs text-red-600">
                  {errors.fullName.message}
                </p>
              )}
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700">
                Work Email
              </label>
              <input
                type="email"
                {...register("email")}
                placeholder="elena@company.com"
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-blue-600 focus:outline-none"
              />
              {errors.email && (
                <p className="mt-1 text-xs text-red-600">
                  {errors.email.message}
                </p>
              )}
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700">
                Password
              </label>
              <input
                type="password"
                {...register("password")}
                placeholder="At least 8 chars, upper, lower & digit"
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-blue-600 focus:outline-none"
              />
              {errors.password && (
                <p className="mt-1 text-xs text-red-600">
                  {errors.password.message}
                </p>
              )}
            </div>

            <div className="grid grid-cols-3 gap-3">
              <div className="col-span-2">
                <label className="block text-xs font-medium text-slate-700">
                  Organization Name
                </label>
                <input
                  type="text"
                  {...register("organizationName")}
                  placeholder="Acme Logistics Inc."
                  className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-blue-600 focus:outline-none"
                />
                {errors.organizationName && (
                  <p className="mt-1 text-xs text-red-600">
                    {errors.organizationName.message}
                  </p>
                )}
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-700">
                  Currency
                </label>
                <input
                  type="text"
                  maxLength={3}
                  {...register("baseCurrency")}
                  className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm uppercase focus:border-blue-600 focus:outline-none"
                />
                {errors.baseCurrency && (
                  <p className="mt-1 text-xs text-red-600">
                    {errors.baseCurrency.message}
                  </p>
                )}
              </div>
            </div>

            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50"
            >
              {isSubmitting
                ? "Provisioning Workspace..."
                : "Register & Create Workspace"}
            </button>
          </form>

          <div className="mt-6 border-t border-slate-200 pt-4 text-center text-xs text-slate-600">
            Already registered?{" "}
            <Link
              href="/login"
              className="font-semibold text-blue-600 hover:underline"
            >
              Sign in to existing workspace
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
