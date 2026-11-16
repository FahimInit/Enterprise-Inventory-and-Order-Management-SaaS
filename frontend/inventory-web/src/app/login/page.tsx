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

const loginSchema = z.object({
  email: z.string().email("Enter a valid business email address."),
  password: z.string().min(1, "Password is required.")
});

type LoginFormValues = z.infer<typeof loginSchema>;

export default function LoginPage() {
  const { setSession } = useAuth();
  const router = useRouter();
  const [serverError, setServerError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting }
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: "",
      password: ""
    }
  });

  const onSubmit = async (values: LoginFormValues) => {
    setServerError(null);
    try {
      const response = await apiRequest<AuthResponse>("/auth/login", {
        method: "POST",
        skipAuth: true,
        body: JSON.stringify(values)
      });
      setSession(response);
      router.push(response.activeTenantId ? "/dashboard" : "/onboarding");
    } catch (err) {
      setServerError(
        err instanceof Error ? err.message : "Authentication failed."
      );
    }
  };

  const fillDemoAccount = (email: string, pass: string) => {
    setValue("email", email);
    setValue("password", pass);
  };

  return (
    <div className="flex min-h-screen flex-col justify-center bg-slate-100 py-12 sm:px-6 lg:px-8">
      <div className="sm:mx-auto sm:w-full sm:max-w-md">
        <div className="mx-auto flex h-10 w-10 items-center justify-center rounded-md bg-slate-900 text-sm font-bold text-white">
          EI
        </div>
        <h1 className="mt-4 text-center text-2xl font-semibold tracking-tight text-slate-900">
          Enterprise Inventory & Order SaaS
        </h1>
        <p className="mt-1 text-center text-sm text-slate-600">
          Sign in to your organization workspace
        </p>
      </div>

      <div className="mt-8 sm:mx-auto sm:w-full sm:max-w-md">
        <div className="rounded-lg border border-slate-200 bg-white px-6 py-8 shadow-sm sm:px-8">
          <ErrorBanner message={serverError} />

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div>
              <label className="block text-xs font-medium text-slate-700">
                Work Email
              </label>
              <input
                type="email"
                {...register("email")}
                placeholder="name@company.com"
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
                placeholder="••••••••"
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-blue-600 focus:outline-none"
              />
              {errors.password && (
                <p className="mt-1 text-xs text-red-600">
                  {errors.password.message}
                </p>
              )}
            </div>

            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50"
            >
              {isSubmitting ? "Signing in..." : "Sign In"}
            </button>
          </form>

          {/* Demo Workspace Helper (Clearly identified per Section 12) */}
          <div className="mt-6 rounded-md border border-amber-200 bg-amber-50/70 p-3">
            <div className="text-xs font-semibold text-amber-900">
              Demo Environment Accounts (When Demo Seed Enabled)
            </div>
            <div className="mt-2 flex flex-wrap gap-1.5">
              <button
                type="button"
                onClick={() =>
                  fillDemoAccount("admin@acme-demo.local", "AdminPass123!")
                }
                className="rounded border border-amber-300 bg-white px-2 py-1 text-[11px] font-medium text-slate-700 hover:bg-amber-100"
              >
                Owner (admin@acme-demo.local)
              </button>
              <button
                type="button"
                onClick={() =>
                  fillDemoAccount(
                    "warehouse@acme-demo.local",
                    "WarehousePass123!"
                  )
                }
                className="rounded border border-amber-300 bg-white px-2 py-1 text-[11px] font-medium text-slate-700 hover:bg-amber-100"
              >
                Warehouse Manager
              </button>
              <button
                type="button"
                onClick={() =>
                  fillDemoAccount("sales@acme-demo.local", "SalesPass123!")
                }
                className="rounded border border-amber-300 bg-white px-2 py-1 text-[11px] font-medium text-slate-700 hover:bg-amber-100"
              >
                Sales Rep
              </button>
            </div>
          </div>

          <div className="mt-6 border-t border-slate-200 pt-4 text-center text-xs text-slate-600">
            New business organization?{" "}
            <Link
              href="/register"
              className="font-semibold text-blue-600 hover:underline"
            >
              Create an account & workspace
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
