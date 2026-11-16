"use client";

import React, { useCallback, useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { apiRequest } from "@/lib/api-client";
import { WarehouseDto, WarehouseLocationDto } from "@/lib/types";
import { useAuth } from "@/context/auth-context";
import {
  Badge,
  Card,
  EmptyState,
  ErrorBanner,
  LoadingState,
  ModalDialog,
  PageHeader
} from "@/components/ui/enterprise-ui";

const warehouseSchema = z.object({
  code: z.string().min(1, "Warehouse code is required."),
  name: z.string().min(2, "Warehouse name is required."),
  addressLine1: z.string().optional(),
  city: z.string().optional(),
  stateOrProvince: z.string().optional(),
  postalCode: z.string().optional(),
  country: z.string().optional(),
  isDefault: z.boolean(),
  isActive: z.boolean()
});

type WarehouseFormValues = z.infer<typeof warehouseSchema>;

export default function WarehousesPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("warehouses:write");

  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<WarehouseDto | null>(null);

  const [selectedWarehouse, setSelectedWarehouse] =
    useState<WarehouseDto | null>(null);
  const [locations, setLocations] = useState<WarehouseLocationDto[]>([]);
  const [locCode, setLocCode] = useState("");
  const [locName, setLocName] = useState("");
  const [locZone, setLocZone] = useState("GENERAL");

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting }
  } = useForm<WarehouseFormValues>({
    resolver: zodResolver(warehouseSchema)
  });

  const loadWarehouses = useCallback(async () => {
    setLoading(true);
    try {
      const res = await apiRequest<WarehouseDto[]>("/warehouses");
      setWarehouses(res);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to load warehouses."
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadWarehouses();
  }, [loadWarehouses]);

  const openLocationsModal = async (wh: WarehouseDto) => {
    setSelectedWarehouse(wh);
    try {
      const res = await apiRequest<WarehouseLocationDto[]>(
        `/warehouses/${wh.id}/locations`
      );
      setLocations(res);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to load bin locations."
      );
    }
  };

  const handleAddLocation = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedWarehouse || !locCode.trim() || !locName.trim()) return;
    try {
      await apiRequest(`/warehouses/${selectedWarehouse.id}/locations`, {
        method: "POST",
        body: JSON.stringify({
          code: locCode,
          name: locName,
          zone: locZone,
          isPickable: true,
          isReceivable: true,
          isActive: true
        })
      });
      setLocCode("");
      setLocName("");
      await openLocationsModal(selectedWarehouse);
      await loadWarehouses();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to add location.");
    }
  };

  const openCreate = () => {
    setEditing(null);
    reset({
      code: "",
      name: "",
      addressLine1: "",
      city: "",
      stateOrProvince: "",
      postalCode: "",
      country: "USA",
      isDefault: false,
      isActive: true
    });
    setModalOpen(true);
  };

  const openEdit = (wh: WarehouseDto) => {
    setEditing(wh);
    reset({
      code: wh.code,
      name: wh.name,
      addressLine1: wh.addressLine1 || "",
      city: wh.city || "",
      stateOrProvince: wh.stateOrProvince || "",
      postalCode: wh.postalCode || "",
      country: wh.country || "",
      isDefault: wh.isDefault,
      isActive: wh.isActive
    });
    setModalOpen(true);
  };

  const onSubmit = async (values: WarehouseFormValues) => {
    setError(null);
    try {
      if (editing) {
        await apiRequest(`/warehouses/${editing.id}`, {
          method: "PUT",
          body: JSON.stringify(values)
        });
      } else {
        await apiRequest("/warehouses", {
          method: "POST",
          body: JSON.stringify(values)
        });
      }
      setModalOpen(false);
      await loadWarehouses();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save warehouse.");
    }
  };

  return (
    <div>
      <PageHeader
        title="Warehouses & Storage Locations"
        description="Manage physical distribution centers, regional hubs, and internal bin/aisle storage locations."
        actions={
          canWrite && (
            <button
              type="button"
              onClick={openCreate}
              className="rounded-md bg-slate-900 px-3.5 py-2 text-xs font-medium text-white hover:bg-slate-800"
            >
              + Add Warehouse
            </button>
          )
        }
      />

      <ErrorBanner message={error} />

      <Card>
        {loading ? (
          <LoadingState label="Loading warehouses..." />
        ) : warehouses.length === 0 ? (
          <EmptyState
            title="No warehouses configured"
            description="Create a warehouse facility to start receiving and storing inventory."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
                <tr>
                  <th className="px-4 py-2.5">Code</th>
                  <th className="px-4 py-2.5">Warehouse Name</th>
                  <th className="px-4 py-2.5">Address / City</th>
                  <th className="px-4 py-2.5">Manager</th>
                  <th className="px-4 py-2.5 text-right">Bin Locations</th>
                  <th className="px-4 py-2.5">Status</th>
                  <th className="px-4 py-2.5 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {warehouses.map((wh) => (
                  <tr key={wh.id} className="hover:bg-slate-50">
                    <td className="px-4 py-2.5 font-mono font-semibold text-slate-900">
                      {wh.code}
                      {wh.isDefault && (
                        <span className="ml-2 rounded bg-blue-50 px-1.5 py-0.5 font-sans text-[10px] font-medium text-blue-700">
                          Default
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-2.5 font-medium text-slate-900">
                      {wh.name}
                    </td>
                    <td className="px-4 py-2.5 text-slate-600">
                      {[wh.addressLine1, wh.city, wh.stateOrProvince, wh.country]
                        .filter(Boolean)
                        .join(", ") || "—"}
                    </td>
                    <td className="px-4 py-2.5 text-slate-600">
                      {wh.managerName || "Unassigned"}
                    </td>
                    <td className="px-4 py-2.5 text-right font-medium">
                      {wh.locationCount}
                    </td>
                    <td className="px-4 py-2.5">
                      <Badge variant={wh.isActive ? "success" : "neutral"}>
                        {wh.isActive ? "Active" : "Inactive"}
                      </Badge>
                    </td>
                    <td className="px-4 py-2.5 text-right space-x-3">
                      <button
                        type="button"
                        onClick={() => openLocationsModal(wh)}
                        className="font-medium text-slate-700 hover:underline"
                      >
                        Bins ({wh.locationCount})
                      </button>
                      {canWrite && (
                        <button
                          type="button"
                          onClick={() => openEdit(wh)}
                          className="font-medium text-blue-600 hover:underline"
                        >
                          Edit
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {/* Create/Edit Warehouse Modal */}
      <ModalDialog
        open={modalOpen}
        title={editing ? `Edit Warehouse ${editing.code}` : "Create Warehouse"}
        onClose={() => setModalOpen(false)}
      >
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-3 text-xs">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-medium text-slate-700">
                Warehouse Code *
              </label>
              <input
                type="text"
                {...register("code")}
                placeholder="WH-EAST"
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5 uppercase"
              />
              {errors.code && (
                <p className="mt-1 text-red-600">{errors.code.message}</p>
              )}
            </div>
            <div>
              <label className="block font-medium text-slate-700">
                Warehouse Name *
              </label>
              <input
                type="text"
                {...register("name")}
                placeholder="East Coast Hub"
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
              {errors.name && (
                <p className="mt-1 text-red-600">{errors.name.message}</p>
              )}
            </div>
          </div>

          <div>
            <label className="block font-medium text-slate-700">
              Street Address
            </label>
            <input
              type="text"
              {...register("addressLine1")}
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-medium text-slate-700">City</label>
              <input
                type="text"
                {...register("city")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
            <div>
              <label className="block font-medium text-slate-700">
                State / Province
              </label>
              <input
                type="text"
                {...register("stateOrProvince")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-medium text-slate-700">
                Postal Code
              </label>
              <input
                type="text"
                {...register("postalCode")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
            <div>
              <label className="block font-medium text-slate-700">
                Country
              </label>
              <input
                type="text"
                {...register("country")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
          </div>

          <div className="flex items-center gap-4 pt-1">
            <label className="inline-flex items-center gap-1.5 font-medium text-slate-700">
              <input type="checkbox" {...register("isDefault")} />
              Default Warehouse
            </label>
            <label className="inline-flex items-center gap-1.5 font-medium text-slate-700">
              <input type="checkbox" {...register("isActive")} />
              Active
            </label>
          </div>

          <div className="flex justify-end gap-2 pt-3">
            <button
              type="button"
              onClick={() => setModalOpen(false)}
              className="rounded border border-slate-300 px-3 py-1.5 text-slate-700"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSubmitting}
              className="rounded bg-slate-900 px-3.5 py-1.5 font-medium text-white hover:bg-slate-800"
            >
              Save Warehouse
            </button>
          </div>
        </form>
      </ModalDialog>

      {/* Bin Locations Modal */}
      <ModalDialog
        open={!!selectedWarehouse}
        title={
          selectedWarehouse
            ? `Storage Bin Locations — ${selectedWarehouse.code}`
            : "Locations"
        }
        onClose={() => setSelectedWarehouse(null)}
      >
        <div className="space-y-4 text-xs">
          {canWrite && (
            <form
              onSubmit={handleAddLocation}
              className="grid grid-cols-3 gap-2 rounded border border-slate-200 bg-slate-50 p-3"
            >
              <input
                type="text"
                required
                value={locCode}
                onChange={(e) => setLocCode(e.target.value)}
                placeholder="Bin Code (A01-R01)"
                className="rounded border border-slate-300 px-2 py-1.5 uppercase"
              />
              <input
                type="text"
                required
                value={locName}
                onChange={(e) => setLocName(e.target.value)}
                placeholder="Location Name"
                className="rounded border border-slate-300 px-2 py-1.5"
              />
              <div className="flex gap-1.5">
                <input
                  type="text"
                  value={locZone}
                  onChange={(e) => setLocZone(e.target.value)}
                  placeholder="Zone"
                  className="w-full rounded border border-slate-300 px-2 py-1.5 uppercase"
                />
                <button
                  type="submit"
                  className="shrink-0 rounded bg-slate-900 px-2.5 py-1.5 font-medium text-white"
                >
                  Add
                </button>
              </div>
            </form>
          )}

          <div className="max-h-64 overflow-y-auto border border-slate-200 rounded">
            <table className="w-full text-left text-xs">
              <thead className="border-b border-slate-200 bg-slate-50">
                <tr>
                  <th className="px-3 py-2">Bin Code</th>
                  <th className="px-3 py-2">Name</th>
                  <th className="px-3 py-2">Zone</th>
                  <th className="px-3 py-2">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {locations.map((loc) => (
                  <tr key={loc.id}>
                    <td className="px-3 py-2 font-mono font-semibold">
                      {loc.code}
                    </td>
                    <td className="px-3 py-2">{loc.name}</td>
                    <td className="px-3 py-2">{loc.zone}</td>
                    <td className="px-3 py-2">
                      <Badge variant={loc.isActive ? "success" : "neutral"}>
                        {loc.isActive ? "Active" : "Inactive"}
                      </Badge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </ModalDialog>
    </div>
  );
}
