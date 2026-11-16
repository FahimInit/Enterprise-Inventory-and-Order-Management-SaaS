"use client";

import React, { useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { apiRequest } from "@/lib/api-client";
import { OrganizationDto } from "@/lib/types";
import { useAuth } from "@/context/auth-context";
import { Card, ErrorBanner, PageHeader } from "@/components/ui/enterprise-ui";

const onboardingSchema = z.object({
  name: z.string().min(2, "Organization name is required."),
  slug: z.string().optional(),
  description: z.string().optional(),
  baseCurrency: z.string().length(3, "Use a 3-letter currency code (e.g. USD)."),
  timeZoneId: z.string().min(1, "Time zone is required."),
  defaultLowStockThreshold: z.coerce.number().min(0),
  defaultTaxRatePercent: z.coerce.number().min(0).max(100)
});

type OnboardingValues = z.infer<typeof onboardingSchema>;

export default function OnboardingPage() {
  const { switchOrganization } = useAuth();
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting }
  } = useForm<OnboardingValues>({
    resolver: zodResolver(onboardingSchema),
    defaultValues: {
      name: "",
      slug: "",
      description: "",
      baseCurrency: "USD",
      timeZoneId: "UTC",
      defaultLowStockThreshold: 10,
      defaultTaxRatePercent: 8.25
    }
  });

  const onSubmit = async (values: OnboardingValues) => {
    setError(null);
    try {
      const created = await apiRequest<OrganizationDto>("/organizations", {
        method: "POST",
        body: JSON.stringify(values)
      });
      await switchOrganization(created.id);
      router.push("/dashboard");
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to create organization."
      );
    }
  };

  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader
        title="Organization Workspace Onboarding"
        description="Provision a new tenant workspace with isolated products, warehouses, inventory ledger, and role permissions."
      />

      <Card className="p-6">
        <ErrorBanner message={error} />

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className="block text-xs font-medium text-slate-700">
                Organization Name *
              </label>
              <input
                type="text"
                {...register("name")}
                placeholder="Pacific Supply Chain Corp"
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
              />
              {errors.name && (
                <p className="mt-1 text-xs text-red-600">
                  {errors.name.message}
                </p>
              )}
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700">
                Workspace Slug (Optional)
              </label>
              <input
                type="text"
                {...register("slug")}
                placeholder="pacific-supply"
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
              />
            </div>
          </div>

          <div>
            <label className="block text-xs font-medium text-slate-700">
              Description
            </label>
            <textarea
              rows={2}
              {...register("description")}
              placeholder="Primary distribution and order operations workspace"
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
            />
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-4">
            <div>
              <label className="block text-xs font-medium text-slate-700">
                Base Currency
              </label>
              <input
                type="text"
                maxLength={3}
                {...register("baseCurrency")}
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm uppercase"
              />
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700">
                Time Zone
              </label>
              <input
                type="text"
                {...register("timeZoneId")}
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
              />
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700">
                Low-Stock Default
              </label>
              <input
                type="number"
                {...register("defaultLowStockThreshold")}
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
              />
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700">
                Default Tax (%)
              </label>
              <input
                type="number"
                step="0.01"
                {...register("defaultTaxRatePercent")}
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
              />
            </div>
          </div>

          <div className="flex justify-end pt-2">
            <button
              type="submit"
              disabled={isSubmitting}
              className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50"
            >
              {isSubmitting ? "Creating Workspace..." : "Create Workspace"}
            </button>
          </div>
        </form>
      </Card>
    </div>
  );
}
