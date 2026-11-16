"use client";

import React, { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { apiRequest } from "@/lib/api-client";
import { OrganizationDto } from "@/lib/types";
import { useAuth } from "@/context/auth-context";
import {
  Card,
  ErrorBanner,
  LoadingState,
  PageHeader
} from "@/components/ui/enterprise-ui";

const settingsSchema = z.object({
  name: z.string().min(2, "Organization name is required."),
  slug: z.string().min(2, "Slug is required."),
  description: z.string().optional(),
  baseCurrency: z.string().length(3, "Use a 3-letter ISO currency code."),
  timeZoneId: z.string().min(1, "Time zone is required."),
  defaultLowStockThreshold: z.coerce.number().min(0),
  defaultTaxRatePercent: z.coerce.number().min(0).max(100)
});

type SettingsFormValues = z.infer<typeof settingsSchema>;

export default function OrganizationSettingsPage() {
  const { hasPermission } = useAuth();
  const canManage = hasPermission("organization:manage");

  const [org, setOrg] = useState<OrganizationDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [savedSuccess, setSavedSuccess] = useState(false);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting }
  } = useForm<SettingsFormValues>({
    resolver: zodResolver(settingsSchema)
  });

  useEffect(() => {
    setLoading(true);
    apiRequest<OrganizationDto>("/organizations/current")
      .then((data) => {
        setOrg(data);
        reset({
          name: data.name,
          slug: data.slug,
          description: data.description || "",
          baseCurrency: data.baseCurrency,
          timeZoneId: data.timeZoneId,
          defaultLowStockThreshold: data.defaultLowStockThreshold,
          defaultTaxRatePercent: data.defaultTaxRatePercent
        });
      })
      .catch((err) => {
        setError(
          err instanceof Error
            ? err.message
            : "Failed to load organization settings."
        );
      })
      .finally(() => setLoading(false));
  }, [reset]);

  const onSubmit = async (values: SettingsFormValues) => {
    setError(null);
    setSavedSuccess(false);
    try {
      const updated = await apiRequest<OrganizationDto>(
        "/organizations/current",
        {
          method: "PUT",
          body: JSON.stringify(values)
        }
      );
      setOrg(updated);
      setSavedSuccess(true);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to update settings."
      );
    }
  };

  if (loading) {
    return <LoadingState label="Loading workspace settings..." />;
  }

  return (
    <div className="max-w-2xl">
      <PageHeader
        title="Organization Workspace Settings"
        description="Configure tenant profile, default currency, tax rate, and inventory threshold defaults."
      />

      <ErrorBanner message={error} />

      {savedSuccess && (
        <div className="mb-4 rounded-md border border-emerald-200 bg-emerald-50 p-3 text-xs text-emerald-900">
          Workspace settings saved and recorded in the audit log.
        </div>
      )}

      <Card className="p-6">
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 text-xs">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className="block font-medium text-slate-700">
                Organization Name *
              </label>
              <input
                type="text"
                disabled={!canManage}
                {...register("name")}
                className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm disabled:bg-slate-100"
              />
              {errors.name && (
                <p className="mt-1 text-red-600">{errors.name.message}</p>
              )}
            </div>

            <div>
              <label className="block font-medium text-slate-700">
                Workspace Slug *
              </label>
              <input
                type="text"
                disabled={!canManage}
                {...register("slug")}
                className="mt-1 w-full rounded border border-slate-300 px-3 py-2 font-mono text-sm disabled:bg-slate-100"
              />
            </div>
          </div>

          <div>
            <label className="block font-medium text-slate-700">
              Description
            </label>
            <textarea
              rows={2}
              disabled={!canManage}
              {...register("description")}
              className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm disabled:bg-slate-100"
            />
          </div>

          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <div>
              <label className="block font-medium text-slate-700">
                Base Currency
              </label>
              <input
                type="text"
                maxLength={3}
                disabled={!canManage}
                {...register("baseCurrency")}
                className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm uppercase disabled:bg-slate-100"
              />
            </div>

            <div>
              <label className="block font-medium text-slate-700">
                Time Zone
              </label>
              <input
                type="text"
                disabled={!canManage}
                {...register("timeZoneId")}
                className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm disabled:bg-slate-100"
              />
            </div>

            <div>
              <label className="block font-medium text-slate-700">
                Low-Stock Default
              </label>
              <input
                type="number"
                disabled={!canManage}
                {...register("defaultLowStockThreshold")}
                className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm disabled:bg-slate-100"
              />
            </div>

            <div>
              <label className="block font-medium text-slate-700">
                Default Tax (%)
              </label>
              <input
                type="number"
                step="0.01"
                disabled={!canManage}
                {...register("defaultTaxRatePercent")}
                className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm disabled:bg-slate-100"
              />
            </div>
          </div>

          {org && (
            <div className="rounded bg-slate-50 p-3 text-slate-600">
              <div>
                <strong>Tenant ID:</strong>{" "}
                <span className="font-mono">{org.id}</span>
              </div>
              <div className="mt-1">
                <strong>Active Members:</strong> {org.activeMemberCount}
              </div>
            </div>
          )}

          {canManage && (
            <div className="flex justify-end pt-2">
              <button
                type="submit"
                disabled={isSubmitting}
                className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50"
              >
                {isSubmitting ? "Saving..." : "Save Workspace Settings"}
              </button>
            </div>
          )}
        </form>
      </Card>
    </div>
  );
}
