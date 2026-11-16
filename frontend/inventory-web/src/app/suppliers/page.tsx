"use client";

import React, { useCallback, useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { apiRequest, formatCurrency } from "@/lib/api-client";
import {
  PagedResponse,
  ProductDto,
  SupplierDetailsDto,
  SupplierDto
} from "@/lib/types";
import { useAuth } from "@/context/auth-context";
import {
  Badge,
  Card,
  EmptyState,
  ErrorBanner,
  LoadingState,
  ModalDialog,
  PageHeader,
  PaginationControls
} from "@/components/ui/enterprise-ui";

const supplierSchema = z.object({
  code: z.string().min(1, "Supplier code is required."),
  name: z.string().min(2, "Supplier name is required."),
  contactName: z.string().optional(),
  email: z.string().email("Invalid email").optional().or(z.literal("")),
  phone: z.string().optional(),
  addressLine1: z.string().optional(),
  city: z.string().optional(),
  country: z.string().optional(),
  paymentTerms: z.string().min(1, "Payment terms required."),
  leadTimeDays: z.coerce.number().int().min(0),
  isActive: z.boolean(),
  notes: z.string().optional()
});

type SupplierFormValues = z.infer<typeof supplierSchema>;

export default function SuppliersPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("suppliers:write");

  const [data, setData] = useState<PagedResponse<SupplierDto> | null>(null);
  const [products, setProducts] = useState<ProductDto[]>([]);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<SupplierDto | null>(null);

  const [details, setDetails] = useState<SupplierDetailsDto | null>(null);
  const [linkProductId, setLinkProductId] = useState("");
  const [linkCost, setLinkCost] = useState(0);
  const [linkMoq, setLinkMoq] = useState(1);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting }
  } = useForm<SupplierFormValues>({
    resolver: zodResolver(supplierSchema)
  });

  const loadSuppliers = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams({
        pageNumber: String(page),
        pageSize: "15"
      });
      if (search.trim()) params.set("search", search.trim());

      const [supRes, prodRes] = await Promise.all([
        apiRequest<PagedResponse<SupplierDto>>(`/suppliers?${params.toString()}`),
        apiRequest<PagedResponse<ProductDto>>("/products?pageSize=100")
      ]);
      setData(supRes);
      setProducts(prodRes.items);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load suppliers.");
    } finally {
      setLoading(false);
    }
  }, [page, search]);

  useEffect(() => {
    loadSuppliers();
  }, [loadSuppliers]);

  const openCreate = () => {
    setEditing(null);
    reset({
      code: "",
      name: "",
      contactName: "",
      email: "",
      phone: "",
      addressLine1: "",
      city: "",
      country: "",
      paymentTerms: "Net 30",
      leadTimeDays: 7,
      isActive: true,
      notes: ""
    });
    setModalOpen(true);
  };

  const openEdit = (sup: SupplierDto) => {
    setEditing(sup);
    reset({
      code: sup.code,
      name: sup.name,
      contactName: sup.contactName || "",
      email: sup.email || "",
      phone: sup.phone || "",
      addressLine1: sup.addressLine1 || "",
      city: sup.city || "",
      country: sup.country || "",
      paymentTerms: sup.paymentTerms,
      leadTimeDays: sup.leadTimeDays,
      isActive: sup.isActive,
      notes: sup.notes || ""
    });
    setModalOpen(true);
  };

  const openCatalogModal = async (supplierId: string) => {
    try {
      const res = await apiRequest<SupplierDetailsDto>(`/suppliers/${supplierId}`);
      setDetails(res);
      setLinkProductId(products[0]?.id || "");
      setLinkCost(products[0]?.standardCost || 10);
      setLinkMoq(1);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to load supplier catalog."
      );
    }
  };

  const handleLinkProduct = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!details || !linkProductId) return;
    try {
      await apiRequest(`/suppliers/${details.supplier.id}/products`, {
        method: "POST",
        body: JSON.stringify({
          productId: linkProductId,
          agreedUnitCost: Number(linkCost),
          minimumOrderQuantity: Number(linkMoq),
          leadTimeDays: details.supplier.leadTimeDays,
          isPreferred: true
        })
      });
      await openCatalogModal(details.supplier.id);
      await loadSuppliers();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to link product.");
    }
  };

  const onSubmit = async (values: SupplierFormValues) => {
    setError(null);
    try {
      if (editing) {
        await apiRequest(`/suppliers/${editing.id}`, {
          method: "PUT",
          body: JSON.stringify(values)
        });
      } else {
        await apiRequest("/suppliers", {
          method: "POST",
          body: JSON.stringify(values)
        });
      }
      setModalOpen(false);
      await loadSuppliers();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save supplier.");
    }
  };

  return (
    <div>
      <PageHeader
        title="Supplier Directory & Sourcing Terms"
        description="Manage vendor contacts, payment terms, lead times, and contracted product pricing."
        actions={
          canWrite && (
            <button
              type="button"
              onClick={openCreate}
              className="rounded-md bg-slate-900 px-3.5 py-2 text-xs font-medium text-white hover:bg-slate-800"
            >
              + Add Supplier
            </button>
          )
        }
      />

      <ErrorBanner message={error} />

      <Card className="mb-4 p-3">
        <input
          type="text"
          value={search}
          onChange={(e) => {
            setPage(1);
            setSearch(e.target.value);
          }}
          placeholder="Search supplier code, company name, or contact..."
          className="w-full max-w-md rounded-md border border-slate-300 px-3 py-1.5 text-xs"
        />
      </Card>

      <Card>
        {loading ? (
          <LoadingState label="Loading suppliers..." />
        ) : !data || data.items.length === 0 ? (
          <EmptyState
            title="No suppliers registered"
            description="Add your vendors and link their supplied products to streamline Purchase Orders."
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
                  <tr>
                    <th className="px-4 py-2.5">Code</th>
                    <th className="px-4 py-2.5">Supplier Name</th>
                    <th className="px-4 py-2.5">Contact</th>
                    <th className="px-4 py-2.5">Location</th>
                    <th className="px-4 py-2.5">Terms / Lead Time</th>
                    <th className="px-4 py-2.5 text-right">Linked SKUs</th>
                    <th className="px-4 py-2.5">Status</th>
                    <th className="px-4 py-2.5 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {data.items.map((s) => (
                    <tr key={s.id} className="hover:bg-slate-50">
                      <td className="px-4 py-2.5 font-mono font-semibold text-slate-900">
                        {s.code}
                      </td>
                      <td className="px-4 py-2.5 font-medium text-slate-900">
                        {s.name}
                      </td>
                      <td className="px-4 py-2.5">
                        <div>{s.contactName || "—"}</div>
                        {s.email && (
                          <div className="text-slate-500">{s.email}</div>
                        )}
                      </td>
                      <td className="px-4 py-2.5 text-slate-600">
                        {[s.city, s.country].filter(Boolean).join(", ") || "—"}
                      </td>
                      <td className="px-4 py-2.5">
                        {s.paymentTerms} ({s.leadTimeDays}d)
                      </td>
                      <td className="px-4 py-2.5 text-right font-medium">
                        {s.linkedProductCount}
                      </td>
                      <td className="px-4 py-2.5">
                        <Badge variant={s.isActive ? "success" : "neutral"}>
                          {s.isActive ? "Active" : "Inactive"}
                        </Badge>
                      </td>
                      <td className="px-4 py-2.5 text-right space-x-3">
                        <button
                          type="button"
                          onClick={() => openCatalogModal(s.id)}
                          className="font-medium text-slate-700 hover:underline"
                        >
                          Sourced SKUs ({s.linkedProductCount})
                        </button>
                        {canWrite && (
                          <button
                            type="button"
                            onClick={() => openEdit(s)}
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
            <PaginationControls
              pageNumber={data.pageNumber}
              totalPages={data.totalPages}
              totalCount={data.totalCount}
              onPageChange={setPage}
            />
          </>
        )}
      </Card>

      {/* Create/Edit Supplier Modal */}
      <ModalDialog
        open={modalOpen}
        title={editing ? `Edit Supplier ${editing.code}` : "Create Supplier"}
        onClose={() => setModalOpen(false)}
      >
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-3 text-xs">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-medium text-slate-700">
                Supplier Code *
              </label>
              <input
                type="text"
                {...register("code")}
                placeholder="SUP-001"
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5 uppercase"
              />
              {errors.code && (
                <p className="mt-1 text-red-600">{errors.code.message}</p>
              )}
            </div>
            <div>
              <label className="block font-medium text-slate-700">
                Company Name *
              </label>
              <input
                type="text"
                {...register("name")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
              {errors.name && (
                <p className="mt-1 text-red-600">{errors.name.message}</p>
              )}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-medium text-slate-700">
                Contact Name
              </label>
              <input
                type="text"
                {...register("contactName")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
            <div>
              <label className="block font-medium text-slate-700">Email</label>
              <input
                type="email"
                {...register("email")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
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
                Country
              </label>
              <input
                type="text"
                {...register("country")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-medium text-slate-700">
                Payment Terms
              </label>
              <input
                type="text"
                {...register("paymentTerms")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
            <div>
              <label className="block font-medium text-slate-700">
                Lead Time (Days)
              </label>
              <input
                type="number"
                {...register("leadTimeDays")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
          </div>

          <div className="flex items-center gap-2 pt-1">
            <input type="checkbox" id="supActive" {...register("isActive")} />
            <label htmlFor="supActive" className="font-medium text-slate-700">
              Supplier is Active
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
              Save Supplier
            </button>
          </div>
        </form>
      </ModalDialog>

      {/* Supplier Linked Products Modal */}
      <ModalDialog
        open={!!details}
        title={
          details
            ? `Sourced Products — ${details.supplier.name}`
            : "Supplier Products"
        }
        onClose={() => setDetails(null)}
      >
        <div className="space-y-4 text-xs">
          {canWrite && (
            <form
              onSubmit={handleLinkProduct}
              className="grid grid-cols-4 gap-2 rounded border border-slate-200 bg-slate-50 p-3"
            >
              <select
                value={linkProductId}
                onChange={(e) => setLinkProductId(e.target.value)}
                className="col-span-2 rounded border border-slate-300 px-2 py-1.5"
              >
                {products.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.sku} — {p.name}
                  </option>
                ))}
              </select>
              <input
                type="number"
                step="0.01"
                value={linkCost}
                onChange={(e) => setLinkCost(Number(e.target.value))}
                placeholder="Agreed Cost"
                className="rounded border border-slate-300 px-2 py-1.5"
              />
              <div className="flex gap-1">
                <input
                  type="number"
                  min={1}
                  value={linkMoq}
                  onChange={(e) => setLinkMoq(Number(e.target.value))}
                  placeholder="MOQ"
                  className="w-full rounded border border-slate-300 px-2 py-1.5"
                />
                <button
                  type="submit"
                  className="shrink-0 rounded bg-slate-900 px-2.5 py-1.5 font-medium text-white"
                >
                  Link
                </button>
              </div>
            </form>
          )}

          <div className="max-h-60 overflow-y-auto rounded border border-slate-200">
            <table className="w-full text-left text-xs">
              <thead className="border-b border-slate-200 bg-slate-50">
                <tr>
                  <th className="px-3 py-2">SKU</th>
                  <th className="px-3 py-2">Product</th>
                  <th className="px-3 py-2 text-right">Agreed Cost</th>
                  <th className="px-3 py-2 text-right">MOQ</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {details?.products.map((sp) => (
                  <tr key={sp.id}>
                    <td className="px-3 py-2 font-mono font-semibold">
                      {sp.productSku}
                    </td>
                    <td className="px-3 py-2">{sp.productName}</td>
                    <td className="px-3 py-2 text-right font-medium">
                      {formatCurrency(sp.agreedUnitCost)}
                    </td>
                    <td className="px-3 py-2 text-right">
                      {sp.minimumOrderQuantity}
                    </td>
                  </tr>
                ))}
                {details?.products.length === 0 && (
                  <tr>
                    <td
                      colSpan={4}
                      className="px-3 py-4 text-center text-slate-500"
                    >
                      No products linked to this supplier yet.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      </ModalDialog>
    </div>
  );
}
