"use client";

import React, { useCallback, useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { apiRequest, formatDateTime } from "@/lib/api-client";
import { CategoryDto } from "@/lib/types";
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

const categorySchema = z.object({
  name: z.string().min(2, "Category name is required."),
  slug: z.string().optional(),
  description: z.string().optional(),
  parentCategoryId: z.string().optional(),
  isActive: z.boolean()
});

type CategoryFormValues = z.infer<typeof categorySchema>;

export default function CategoriesPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("catalog:write");

  const [categories, setCategories] = useState<CategoryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<CategoryDto | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting }
  } = useForm<CategoryFormValues>({
    resolver: zodResolver(categorySchema)
  });

  const loadCategories = useCallback(async () => {
    setLoading(true);
    try {
      const res = await apiRequest<CategoryDto[]>("/categories");
      setCategories(res);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to load categories."
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadCategories();
  }, [loadCategories]);

  const openCreate = () => {
    setEditing(null);
    reset({
      name: "",
      slug: "",
      description: "",
      parentCategoryId: "",
      isActive: true
    });
    setModalOpen(true);
  };

  const openEdit = (cat: CategoryDto) => {
    setEditing(cat);
    reset({
      name: cat.name,
      slug: cat.slug,
      description: cat.description || "",
      parentCategoryId: cat.parentCategoryId || "",
      isActive: cat.isActive
    });
    setModalOpen(true);
  };

  const onSubmit = async (values: CategoryFormValues) => {
    setError(null);
    try {
      const payload = {
        ...values,
        parentCategoryId: values.parentCategoryId || null
      };
      if (editing) {
        await apiRequest(`/categories/${editing.id}`, {
          method: "PUT",
          body: JSON.stringify(payload)
        });
      } else {
        await apiRequest("/categories", {
          method: "POST",
          body: JSON.stringify(payload)
        });
      }
      setModalOpen(false);
      await loadCategories();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save category.");
    }
  };

  return (
    <div>
      <PageHeader
        title="Product Categories"
        description="Organize your product catalog into structured categories for reporting and valuation."
        actions={
          canWrite && (
            <button
              type="button"
              onClick={openCreate}
              className="rounded-md bg-slate-900 px-3.5 py-2 text-xs font-medium text-white hover:bg-slate-800"
            >
              + New Category
            </button>
          )
        }
      />

      <ErrorBanner message={error} />

      <Card>
        {loading ? (
          <LoadingState label="Loading categories..." />
        ) : categories.length === 0 ? (
          <EmptyState
            title="No categories defined"
            description="Create your first product category to classify SKUs."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
                <tr>
                  <th className="px-4 py-2.5">Category Name</th>
                  <th className="px-4 py-2.5">Slug</th>
                  <th className="px-4 py-2.5">Parent Category</th>
                  <th className="px-4 py-2.5">Description</th>
                  <th className="px-4 py-2.5 text-right">Products</th>
                  <th className="px-4 py-2.5">Status</th>
                  <th className="px-4 py-2.5">Created</th>
                  {canWrite && (
                    <th className="px-4 py-2.5 text-right">Actions</th>
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {categories.map((cat) => (
                  <tr key={cat.id} className="hover:bg-slate-50">
                    <td className="px-4 py-2.5 font-semibold text-slate-900">
                      {cat.name}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-slate-600">
                      {cat.slug}
                    </td>
                    <td className="px-4 py-2.5 text-slate-600">
                      {cat.parentCategoryName || "—"}
                    </td>
                    <td className="px-4 py-2.5 text-slate-600">
                      {cat.description || "—"}
                    </td>
                    <td className="px-4 py-2.5 text-right font-medium">
                      {cat.productCount}
                    </td>
                    <td className="px-4 py-2.5">
                      <Badge variant={cat.isActive ? "success" : "neutral"}>
                        {cat.isActive ? "Active" : "Inactive"}
                      </Badge>
                    </td>
                    <td className="px-4 py-2.5 text-slate-500">
                      {formatDateTime(cat.createdAtUtc)}
                    </td>
                    {canWrite && (
                      <td className="px-4 py-2.5 text-right">
                        <button
                          type="button"
                          onClick={() => openEdit(cat)}
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
        )}
      </Card>

      <ModalDialog
        open={modalOpen}
        title={editing ? `Edit Category: ${editing.name}` : "Create Category"}
        onClose={() => setModalOpen(false)}
      >
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-3 text-xs">
          <div>
            <label className="block font-medium text-slate-700">
              Category Name *
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
              Slug (Optional)
            </label>
            <input
              type="text"
              {...register("slug")}
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            />
          </div>

          <div>
            <label className="block font-medium text-slate-700">
              Parent Category
            </label>
            <select
              {...register("parentCategoryId")}
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            >
              <option value="">None (Top-Level Category)</option>
              {categories
                .filter((c) => c.id !== editing?.id)
                .map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
            </select>
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

          <div className="flex items-center gap-2 pt-1">
            <input
              type="checkbox"
              id="catActive"
              {...register("isActive")}
              className="h-4 w-4 rounded border-slate-300"
            />
            <label htmlFor="catActive" className="font-medium text-slate-700">
              Category is Active
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
              className="rounded bg-slate-900 px-3.5 py-1.5 font-medium text-white hover:bg-slate-800 disabled:opacity-50"
            >
              {isSubmitting ? "Saving..." : "Save Category"}
            </button>
          </div>
        </form>
      </ModalDialog>
    </div>
  );
}
