"use client";

import React, { useCallback, useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { apiRequest, formatCurrency } from "@/lib/api-client";
import {
  CategoryDto,
  PagedResponse,
  ProductDto,
  ProductStatus
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

const productSchema = z
  .object({
    categoryId: z.string().min(1, "Category is required."),
    sku: z.string().min(1, "SKU is required."),
    name: z.string().min(2, "Product name is required."),
    description: z.string().optional(),
    barcode: z.string().optional(),
    unitOfMeasure: z.string().min(1, "Unit of measure is required."),
    standardCost: z.coerce.number().min(0, "Cost cannot be negative."),
    unitPrice: z.coerce.number().min(0, "Price cannot be negative."),
    reorderPoint: z.coerce.number().int().min(0),
    targetStockLevel: z.coerce.number().int().min(0),
    status: z.enum(["Draft", "Active", "Archived"])
  })
  .refine((data) => data.targetStockLevel >= data.reorderPoint, {
    message: "Target stock level must be >= reorder point.",
    path: ["targetStockLevel"]
  });

type ProductFormValues = z.infer<typeof productSchema>;

export default function ProductsPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("catalog:write");

  const [data, setData] = useState<PagedResponse<ProductDto> | null>(null);
  const [categories, setCategories] = useState<CategoryDto[]>([]);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [selectedCategory, setSelectedCategory] = useState("");
  const [selectedStatus, setSelectedStatus] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [modalOpen, setModalOpen] = useState(false);
  const [editingProduct, setEditingProduct] = useState<ProductDto | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting }
  } = useForm<ProductFormValues>({
    resolver: zodResolver(productSchema)
  });

  const loadProducts = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams({
        pageNumber: String(page),
        pageSize: "15"
      });
      if (search.trim()) params.set("search", search.trim());
      if (selectedCategory) params.set("categoryId", selectedCategory);
      if (selectedStatus) params.set("status", selectedStatus);

      const [productsRes, catsRes] = await Promise.all([
        apiRequest<PagedResponse<ProductDto>>(`/products?${params.toString()}`),
        apiRequest<CategoryDto[]>("/categories")
      ]);
      setData(productsRes);
      setCategories(catsRes);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load products.");
    } finally {
      setLoading(false);
    }
  }, [page, search, selectedCategory, selectedStatus]);

  useEffect(() => {
    loadProducts();
  }, [loadProducts]);

  const openCreateModal = () => {
    setEditingProduct(null);
    reset({
      categoryId: categories[0]?.id || "",
      sku: "",
      name: "",
      description: "",
      barcode: "",
      unitOfMeasure: "EA",
      standardCost: 0,
      unitPrice: 0,
      reorderPoint: 10,
      targetStockLevel: 50,
      status: "Active"
    });
    setModalOpen(true);
  };

  const openEditModal = (product: ProductDto) => {
    setEditingProduct(product);
    reset({
      categoryId: product.categoryId,
      sku: product.sku,
      name: product.name,
      description: product.description || "",
      barcode: product.barcode || "",
      unitOfMeasure: product.unitOfMeasure,
      standardCost: product.standardCost,
      unitPrice: product.unitPrice,
      reorderPoint: product.reorderPoint,
      targetStockLevel: product.targetStockLevel,
      status: product.status as ProductStatus
    });
    setModalOpen(true);
  };

  const onSubmit = async (values: ProductFormValues) => {
    setError(null);
    try {
      if (editingProduct) {
        await apiRequest(`/products/${editingProduct.id}`, {
          method: "PUT",
          body: JSON.stringify(values)
        });
      } else {
        await apiRequest("/products", {
          method: "POST",
          body: JSON.stringify(values)
        });
      }
      setModalOpen(false);
      await loadProducts();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save product.");
    }
  };

  return (
    <div>
      <PageHeader
        title="Product Master Catalog"
        description="Manage SKUs, units of measurement, standard & moving-average costs, selling prices, and reorder thresholds."
        actions={
          canWrite && (
            <button
              type="button"
              onClick={openCreateModal}
              className="rounded-md bg-slate-900 px-3.5 py-2 text-xs font-medium text-white hover:bg-slate-800"
            >
              + Add Product SKU
            </button>
          )
        }
      />

      <ErrorBanner message={error} />

      <Card className="mb-4 p-3">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-4">
          <input
            type="text"
            value={search}
            onChange={(e) => {
              setPage(1);
              setSearch(e.target.value);
            }}
            placeholder="Search SKU, name, or barcode..."
            className="rounded-md border border-slate-300 px-3 py-1.5 text-xs sm:col-span-2"
          />
          <select
            value={selectedCategory}
            onChange={(e) => {
              setPage(1);
              setSelectedCategory(e.target.value);
            }}
            className="rounded-md border border-slate-300 px-3 py-1.5 text-xs"
          >
            <option value="">All Categories</option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
          <select
            value={selectedStatus}
            onChange={(e) => {
              setPage(1);
              setSelectedStatus(e.target.value);
            }}
            className="rounded-md border border-slate-300 px-3 py-1.5 text-xs"
          >
            <option value="">All Statuses</option>
            <option value="Active">Active</option>
            <option value="Draft">Draft</option>
            <option value="Archived">Archived</option>
          </select>
        </div>
      </Card>

      <Card>
        {loading ? (
          <LoadingState label="Loading product catalog..." />
        ) : !data || data.items.length === 0 ? (
          <EmptyState
            title="No products found"
            description="Create a product category and add your first SKU to begin tracking inventory."
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
                  <tr>
                    <th className="px-4 py-2.5">SKU</th>
                    <th className="px-4 py-2.5">Product Name</th>
                    <th className="px-4 py-2.5">Category</th>
                    <th className="px-4 py-2.5">UoM</th>
                    <th className="px-4 py-2.5 text-right">Std Cost</th>
                    <th className="px-4 py-2.5 text-right">Avg Cost</th>
                    <th className="px-4 py-2.5 text-right">Unit Price</th>
                    <th className="px-4 py-2.5 text-right">
                      Reorder / Target
                    </th>
                    <th className="px-4 py-2.5">Status</th>
                    {canWrite && (
                      <th className="px-4 py-2.5 text-right">Actions</th>
                    )}
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {data.items.map((p) => (
                    <tr key={p.id} className="hover:bg-slate-50">
                      <td className="px-4 py-2.5 font-mono font-semibold text-slate-900">
                        {p.sku}
                      </td>
                      <td className="px-4 py-2.5">
                        <div className="font-medium text-slate-900">
                          {p.name}
                        </div>
                        {p.barcode && (
                          <div className="text-[11px] text-slate-500">
                            UPC: {p.barcode}
                          </div>
                        )}
                      </td>
                      <td className="px-4 py-2.5 text-slate-600">
                        {p.categoryName}
                      </td>
                      <td className="px-4 py-2.5">{p.unitOfMeasure}</td>
                      <td className="px-4 py-2.5 text-right">
                        {formatCurrency(p.standardCost)}
                      </td>
                      <td className="px-4 py-2.5 text-right font-medium text-slate-800">
                        {formatCurrency(p.movingAverageCost)}
                      </td>
                      <td className="px-4 py-2.5 text-right font-semibold text-slate-900">
                        {formatCurrency(p.unitPrice)}
                      </td>
                      <td className="px-4 py-2.5 text-right">
                        {p.reorderPoint} / {p.targetStockLevel}
                      </td>
                      <td className="px-4 py-2.5">
                        <Badge
                          variant={
                            p.status === "Active"
                              ? "success"
                              : p.status === "Draft"
                              ? "warning"
                              : "neutral"
                          }
                        >
                          {p.status}
                        </Badge>
                      </td>
                      {canWrite && (
                        <td className="px-4 py-2.5 text-right">
                          <button
                            type="button"
                            onClick={() => openEditModal(p)}
                            className="font-medium text-blue-600 hover:underline"
                          >
                            Edit
                          </button>
                        </td>
                      )}
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

      <ModalDialog
        open={modalOpen}
        title={editingProduct ? `Edit Product ${editingProduct.sku}` : "Create New Product SKU"}
        onClose={() => setModalOpen(false)}
      >
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-3 text-xs">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-medium text-slate-700">SKU *</label>
              <input
                type="text"
                {...register("sku")}
                placeholder="IND-PLC-400"
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5 uppercase"
              />
              {errors.sku && (
                <p className="mt-1 text-red-600">{errors.sku.message}</p>
              )}
            </div>

            <div>
              <label className="block font-medium text-slate-700">
                Category *
              </label>
              <select
                {...register("categoryId")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              >
                <option value="">Select category...</option>
                {categories.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
              {errors.categoryId && (
                <p className="mt-1 text-red-600">{errors.categoryId.message}</p>
              )}
            </div>
          </div>

          <div>
            <label className="block font-medium text-slate-700">
              Product Name *
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

          <div>
            <label className="block font-medium text-slate-700">
              Description
            </label>
            <textarea
              rows={2}
              {...register("description")}
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            />
          </div>

          <div className="grid grid-cols-3 gap-3">
            <div>
              <label className="block font-medium text-slate-700">
                Unit of Measure *
              </label>
              <input
                type="text"
                {...register("unitOfMeasure")}
                placeholder="EA, BOX, KG"
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5 uppercase"
              />
            </div>
            <div>
              <label className="block font-medium text-slate-700">
                Barcode / UPC
              </label>
              <input
                type="text"
                {...register("barcode")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
            <div>
              <label className="block font-medium text-slate-700">Status</label>
              <select
                {...register("status")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              >
                <option value="Active">Active</option>
                <option value="Draft">Draft</option>
                <option value="Archived">Archived</option>
              </select>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-medium text-slate-700">
                Standard Cost *
              </label>
              <input
                type="number"
                step="0.01"
                {...register("standardCost")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
            <div>
              <label className="block font-medium text-slate-700">
                Selling Unit Price *
              </label>
              <input
                type="number"
                step="0.01"
                {...register("unitPrice")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-medium text-slate-700">
                Reorder Point *
              </label>
              <input
                type="number"
                {...register("reorderPoint")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
            <div>
              <label className="block font-medium text-slate-700">
                Target Stock Level *
              </label>
              <input
                type="number"
                {...register("targetStockLevel")}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
              {errors.targetStockLevel && (
                <p className="mt-1 text-red-600">
                  {errors.targetStockLevel.message}
                </p>
              )}
            </div>
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
              className="rounded bg-slate-900 px-3.5 py-1.5 font-medium text-white hover:bg-slate-800 disabled:opacity-50"
            >
              {isSubmitting ? "Saving..." : "Save Product"}
            </button>
          </div>
        </form>
      </ModalDialog>
    </div>
  );
}
