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
import { ShieldCheck, ArrowRight, UserCheck, KeyRound, Building2, Eye, EyeOff } from "lucide-react";

const loginSchema = z.object({
  email: z.string().email("Enter a valid business email address."),
  password: z.string().min(1, "Password is required.")
});

type LoginFormValues = z.infer<typeof loginSchema>;

interface DemoCredential {
  role: string;
  name: string;
  email: string;
  pass: string;
  color: string;
  description: string;
}

const DEMO_ACCOUNTS: DemoCredential[] = [
  {
    role: "Owner / Administrator",
    name: "Elena Vance",
    email: "admin@acme-demo.local",
    pass: "AdminPass123!",
    color: "bg-purple-100 text-purple-800 border-purple-200",
    description: "Full administrative access: organization settings, user management, reports & audits."
  },
  {
    role: "Warehouse Manager",
    name: "Marcus Chen",
    email: "warehouse@acme-demo.local",
    pass: "WarehousePass123!",
    color: "bg-blue-100 text-blue-800 border-blue-200",
    description: "Warehouses, bin locations, goods receipts, stock reconciliation & order fulfillment."
  },
  {
    role: "Sales Representative",
    name: "Sarah Jenkins",
    email: "sales@acme-demo.local",
    pass: "SalesPass123!",
    color: "bg-emerald-100 text-emerald-800 border-emerald-200",
    description: "Customer sales orders, stock reservations, order confirmation & dispatch tracking."
  },
  {
    role: "Procurement Officer",
    name: "David Ross",
    email: "procurement@acme-demo.local",
    pass: "ProcurePass123!",
    color: "bg-amber-100 text-amber-800 border-amber-200",
    description: "Supplier management, purchase order generation, PO approvals & incoming shipments."
  },
  {
    role: "Inventory Clerk",
    name: "Amina Patel",
    email: "clerk@acme-demo.local",
    pass: "ClerkPass123!",
    color: "bg-cyan-100 text-cyan-800 border-cyan-200",
    description: "Physical cycle counts, stock adjustments, barcode scanning & warehouse transfers."
  },
  {
    role: "Compliance Auditor",
    name: "Lucas Meyer",
    email: "auditor@acme-demo.local",
    pass: "AuditorPass123!",
    color: "bg-slate-100 text-slate-800 border-slate-200",
    description: "Read-only access to immutable inventory ledgers, system audit logs & analytics."
  }
];

export default function LoginPage() {
  const { setSession } = useAuth();
  const router = useRouter();
  const [serverError, setServerError] = useState<string | null>(null);
  const [selectedRole, setSelectedRole] = useState<string | null>(null);
  const [showPassword, setShowPassword] = useState(false);

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

  const fillDemoAccount = (acc: DemoCredential) => {
    setValue("email", acc.email, { shouldValidate: true });
    setValue("password", acc.pass, { shouldValidate: true });
    setSelectedRole(acc.role);
    setServerError(null);
  };

  return (
    <div className="flex min-h-screen flex-col justify-center bg-slate-50 py-12 sm:px-6 lg:px-8">
      <div className="sm:mx-auto sm:w-full sm:max-w-4xl">
        <div className="flex items-center justify-center gap-2">
          <div className="flex h-11 w-11 items-center justify-center rounded-lg bg-slate-900 text-base font-bold text-white shadow-sm">
            EI
          </div>
          <span className="text-xl font-bold tracking-tight text-slate-900">
            Enterprise Inventory &amp; Order SaaS
          </span>
        </div>
        <h1 className="mt-3 text-center text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl">
          Sign in to your organization workspace
        </h1>
        <p className="mt-1 text-center text-sm text-slate-600">
          Multi-tenant inventory, warehouse fulfillment & order orchestration
        </p>
      </div>

      <div className="mt-8 sm:mx-auto sm:w-full sm:max-w-4xl px-4 sm:px-0">
        <div className="grid grid-cols-1 gap-8 lg:grid-cols-12">
          {/* Main Login Form Column */}
          <div className="lg:col-span-5">
            <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8">
              <div className="mb-5 flex items-center justify-between border-b border-slate-100 pb-4">
                <div className="flex items-center gap-2 text-sm font-semibold text-slate-800">
                  <KeyRound className="h-4 w-4 text-slate-500" />
                  Account Authentication
                </div>
                {selectedRole && (
                  <span className="inline-flex items-center rounded-full bg-blue-50 px-2 py-0.5 text-[10px] font-medium text-blue-700">
                    Auto-filled: {selectedRole}
                  </span>
                )}
              </div>

              <ErrorBanner message={serverError} />

              <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
                <div>
                  <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
                    Work Email
                  </label>
                  <input
                    type="email"
                    {...register("email")}
                    placeholder="name@company.com"
                    className="mt-1.5 w-full rounded-md border border-slate-300 px-3.5 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-slate-900 focus:outline-none focus:ring-1 focus:ring-slate-900"
                  />
                  {errors.email && (
                    <p className="mt-1 text-xs text-red-600 font-medium">
                      {errors.email.message}
                    </p>
                  )}
                </div>

                <div>
                  <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
                    Password
                  </label>
                  <div className="relative mt-1.5">
                    <input
                      type={showPassword ? "text" : "password"}
                      {...register("password")}
                      placeholder="••••••••••••"
                      className="w-full rounded-md border border-slate-300 py-2 pl-3.5 pr-10 text-sm text-slate-900 placeholder-slate-400 focus:border-slate-900 focus:outline-none focus:ring-1 focus:ring-slate-900"
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword((prev) => !prev)}
                      aria-label={showPassword ? "Hide password" : "Show password"}
                      className="absolute inset-y-0 right-0 flex items-center pr-3 text-slate-400 hover:text-slate-700 focus:outline-none"
                    >
                      {showPassword ? (
                        <EyeOff className="h-4 w-4" />
                      ) : (
                        <Eye className="h-4 w-4" />
                      )}
                    </button>
                  </div>
                  {errors.password && (
                    <p className="mt-1 text-xs text-red-600 font-medium">
                      {errors.password.message}
                    </p>
                  )}
                </div>

                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="mt-2 flex w-full items-center justify-center gap-2 rounded-md bg-slate-900 px-4 py-2.5 text-sm font-medium text-white transition hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:ring-offset-2 disabled:opacity-50"
                >
                  {isSubmitting ? "Authenticating..." : "Sign In to Workspace"}
                  {!isSubmitting && <ArrowRight className="h-4 w-4" />}
                </button>
              </form>

              <div className="mt-6 border-t border-slate-100 pt-4 text-center text-xs text-slate-600">
                Registering a new organization?{" "}
                <Link
                  href="/register"
                  className="font-semibold text-blue-600 hover:text-blue-700 hover:underline"
                >
                  Create workspace
                </Link>
              </div>
            </div>

            {/* Quick Demo Environment Note */}
            <div className="mt-4 flex items-start gap-2.5 rounded-lg border border-slate-200 bg-white p-3.5 text-xs text-slate-600 shadow-sm">
              <Building2 className="mt-0.5 h-4 w-4 shrink-0 text-slate-400" />
              <div>
                <span className="font-semibold text-slate-800">Demo Workspace:</span>{" "}
                <code className="rounded bg-slate-100 px-1 py-0.5 text-slate-700 font-mono">
                  Acme Industrial Supply [DEMO]
                </code>
                <div className="mt-1 text-[11px] text-slate-500">
                  Select any credential on the right to immediately test role-based access control and tenant isolation.
                </div>
              </div>
            </div>
          </div>

          {/* Sample Login Credentials Column */}
          <div className="lg:col-span-7">
            <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm sm:p-7">
              <div className="mb-4 flex items-center justify-between">
                <div>
                  <h2 className="text-sm font-bold uppercase tracking-wider text-slate-900 flex items-center gap-2">
                    <ShieldCheck className="h-4 w-4 text-blue-600" />
                    Sample Login Credentials
                  </h2>
                  <p className="mt-0.5 text-xs text-slate-500">
                    Click any role below to pre-fill the form with verified demo credentials:
                  </p>
                </div>
                <span className="hidden shrink-0 whitespace-nowrap sm:inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-medium text-emerald-700 border border-emerald-200">
                  <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
                  Ready to test
                </span>
              </div>

              <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
                {DEMO_ACCOUNTS.map((acc) => {
                  const isSelected = selectedRole === acc.role;
                  return (
                    <div
                      key={acc.email}
                      className={`group relative flex flex-col justify-between rounded-lg border p-3 text-left transition ${
                        isSelected
                          ? "border-blue-500 bg-blue-50/40 ring-1 ring-blue-500"
                          : "border-slate-200 bg-slate-50/50 hover:border-slate-300 hover:bg-slate-50"
                      }`}
                    >
                      <div>
                        <div className="flex items-center justify-between gap-1">
                          <span
                            className={`inline-block rounded border px-2 py-0.5 text-[10px] font-semibold tracking-wide ${acc.color}`}
                          >
                            {acc.role}
                          </span>
                          <span className="text-[11px] font-medium text-slate-600">
                            {acc.name}
                          </span>
                        </div>

                        <div className="mt-2.5 space-y-1 font-mono text-[11px]">
                          <div className="flex items-center justify-between gap-2 text-slate-600">
                            <span className="shrink-0 text-[10px] font-sans font-medium text-slate-600 uppercase">
                              Email:
                            </span>
                            <span className="text-slate-800 font-medium truncate" title={acc.email}>
                              {acc.email}
                            </span>
                          </div>
                          <div className="flex items-center justify-between gap-2 text-slate-600">
                            <span className="shrink-0 text-[10px] font-sans font-medium text-slate-600 uppercase">
                              Password:
                            </span>
                            <span className="text-slate-800 font-semibold">
                              {acc.pass}
                            </span>
                          </div>
                        </div>

                        <p className="mt-2 text-[10.5px] leading-relaxed text-slate-500 line-clamp-2">
                          {acc.description}
                        </p>
                      </div>

                      <button
                        type="button"
                        onClick={() => fillDemoAccount(acc)}
                        className={`mt-3 flex w-full items-center justify-center gap-1.5 rounded-md px-2.5 py-1.5 text-xs font-semibold transition ${
                          isSelected
                            ? "bg-blue-600 text-white hover:bg-blue-700 shadow-sm"
                            : "bg-white text-slate-700 border border-slate-300 hover:bg-slate-100 hover:text-slate-900"
                        }`}
                      >
                        <UserCheck className="h-3.5 w-3.5" />
                        {isSelected ? "Selected (Ready to Sign In)" : "Use These Credentials"}
                      </button>
                    </div>
                  );
                })}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
