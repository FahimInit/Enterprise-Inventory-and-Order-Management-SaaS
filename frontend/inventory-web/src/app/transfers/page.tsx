"use client";

import React, { useCallback, useEffect, useState } from "react";
import { apiRequest, formatDateTime } from "@/lib/api-client";
import {
  PagedResponse,
  ProductDto,
  StockTransferDto,
  WarehouseDto
} from "@/lib/types";
import { useAuth } from "@/context/auth-context";
import {
  Badge,
  Card,
  ConfirmDialog,
  EmptyState,
  ErrorBanner,
  LoadingState,
  ModalDialog,
  PageHeader,
  PaginationControls
} from "@/components/ui/enterprise-ui";

export default function StockTransfersPage() {
  const { hasPermission } = useAuth();
  const canCreate = hasPermission("transfers:create");
  const canDispatch = hasPermission("transfers:dispatch");
  const canReceive = hasPermission("transfers:receive");

  const [data, setData] = useState<PagedResponse<StockTransferDto> | null>(
    null
  );
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [products, setProducts] = useState<ProductDto[]>([]);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Create Modal
  const [createOpen, setCreateOpen] = useState(false);
  const [sourceWarehouseId, setSourceWarehouseId] = useState("");
  const [destinationWarehouseId, setDestinationWarehouseId] = useState("");
  const [productId, setProductId] = useState("");
  const [quantity, setQuantity] = useState(10);
  const [notes, setNotes] = useState("");
  const [submitting, setSubmitting] = useState(false);

  // Cancel Confirmation
  const [cancellingTransfer, setCancellingTransfer] =
    useState<StockTransferDto | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [trfRes, whRes, prodRes] = await Promise.all([
        apiRequest<PagedResponse<StockTransferDto>>(
          `/stock-transfers?pageNumber=${page}&pageSize=15`
        ),
        apiRequest<WarehouseDto[]>("/warehouses"),
        apiRequest<PagedResponse<ProductDto>>("/products?pageSize=100")
      ]);
      setData(trfRes);
      setWarehouses(whRes);
      setProducts(prodRes.items);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to load stock transfers."
      );
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const openCreateModal = () => {
    setSourceWarehouseId(warehouses[0]?.id || "");
    setDestinationWarehouseId(warehouses[1]?.id || "");
    setProductId(products[0]?.id || "");
    setQuantity(10);
    setNotes("");
    setCreateOpen(true);
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await apiRequest("/stock-transfers", {
        method: "POST",
        idempotent: true,
        body: JSON.stringify({
          sourceWarehouseId,
          destinationWarehouseId,
          notes: notes || null,
          lines: [{ productId, quantity: Number(quantity) }]
        })
      });
      setCreateOpen(false);
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to create stock transfer."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleDispatch = async (id: string) => {
    setError(null);
    try {
      await apiRequest(`/stock-transfers/${id}/dispatch`, {
        method: "POST",
        idempotent: true
      });
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to dispatch transfer."
      );
    }
  };

  const handleReceive = async (id: string) => {
    setError(null);
    try {
      await apiRequest(`/stock-transfers/${id}/receive`, {
        method: "POST",
        idempotent: true
      });
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to receive transfer."
      );
    }
  };

  const handleConfirmCancel = async () => {
    if (!cancellingTransfer) return;
    setSubmitting(true);
    setError(null);
    try {
      await apiRequest(`/stock-transfers/${cancellingTransfer.id}/cancel`, {
        method: "POST",
        idempotent: true,
        body: JSON.stringify({
          reason: "Cancelled by warehouse operator via dashboard"
        })
      });
      setCancellingTransfer(null);
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to cancel transfer."
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="Inter-Warehouse Stock Transfers"
        description="Coordinate stock movement between warehouses with explicit In-Transit tracking and reversal protection."
        actions={
          canCreate && (
            <button
              type="button"
              onClick={openCreateModal}
              className="rounded-md bg-slate-900 px-3.5 py-2 text-xs font-medium text-white hover:bg-slate-800"
            >
              + New Stock Transfer
            </button>
          )
        }
      />

      <ErrorBanner message={error} />

      <Card>
        {loading ? (
          <LoadingState label="Loading inter-warehouse transfers..." />
        ) : !data || data.items.length === 0 ? (
          <EmptyState
            title="No stock transfers recorded"
            description="Create a transfer request to move inventory between two warehouses."
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
                  <tr>
                    <th className="px-4 py-2.5">Transfer #</th>
                    <th className="px-4 py-2.5">Source Warehouse</th>
                    <th className="px-4 py-2.5">Destination Warehouse</th>
                    <th className="px-4 py-2.5">Items</th>
                    <th className="px-4 py-2.5 text-right">Total Units</th>
                    <th className="px-4 py-2.5">Status</th>
                    <th className="px-4 py-2.5">Created</th>
                    <th className="px-4 py-2.5 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {data.items.map((t) => (
                    <tr key={t.id} className="hover:bg-slate-50">
                      <td className="px-4 py-2.5 font-mono font-semibold text-slate-900">
                        {t.transferNumber}
                      </td>
                      <td className="px-4 py-2.5">
                        <span className="font-medium">
                          {t.sourceWarehouseCode}
                        </span>{" "}
                        — {t.sourceWarehouseName}
                      </td>
                      <td className="px-4 py-2.5">
                        <span className="font-medium">
                          {t.destinationWarehouseCode}
                        </span>{" "}
                        — {t.destinationWarehouseName}
                      </td>
                      <td className="px-4 py-2.5">
                        {t.lines.map((l) => (
                          <div key={l.id}>
                            <span className="font-medium text-slate-900">
                              {l.productSku}
                            </span>{" "}
                            × {l.quantity}
                          </div>
                        ))}
                      </td>
                      <td className="px-4 py-2.5 text-right font-semibold">
                        {t.totalQuantity}
                      </td>
                      <td className="px-4 py-2.5">
                        <Badge
                          variant={
                            t.status === "Received"
                              ? "success"
                              : t.status === "InTransit"
                              ? "info"
                              : t.status === "Cancelled"
                              ? "danger"
                              : "warning"
                          }
                        >
                          {t.status}
                        </Badge>
                      </td>
                      <td className="px-4 py-2.5 text-slate-500">
                        {formatDateTime(t.createdAtUtc)}
                      </td>
                      <td className="px-4 py-2.5 text-right space-x-2">
                        {t.status === "Requested" && canDispatch && (
                          <button
                            type="button"
                            onClick={() => handleDispatch(t.id)}
                            className="rounded bg-blue-600 px-2.5 py-1 text-[11px] font-medium text-white hover:bg-blue-700"
                          >
                            Dispatch
                          </button>
                        )}
                        {t.status === "InTransit" && canReceive && (
                          <button
                            type="button"
                            onClick={() => handleReceive(t.id)}
                            className="rounded bg-emerald-600 px-2.5 py-1 text-[11px] font-medium text-white hover:bg-emerald-700"
                          >
                            Receive Goods
                          </button>
                        )}
                        {(t.status === "Requested" ||
                          t.status === "InTransit") &&
                          canDispatch && (
                            <button
                              type="button"
                              onClick={() => setCancellingTransfer(t)}
                              className="font-medium text-red-600 hover:underline"
                            >
                              Cancel
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

      <ModalDialog
        open={createOpen}
        title="Create Inter-Warehouse Stock Transfer"
        onClose={() => setCreateOpen(false)}
      >
        <form onSubmit={handleCreate} className="space-y-3 text-xs">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-medium text-slate-700">
                Source Warehouse *
              </label>
              <select
                required
                value={sourceWarehouseId}
                onChange={(e) => setSourceWarehouseId(e.target.value)}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              >
                <option value="">Select source...</option>
                {warehouses.map((w) => (
                  <option key={w.id} value={w.id}>
                    {w.code} — {w.name}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="block font-medium text-slate-700">
                Destination Warehouse *
              </label>
              <select
                required
                value={destinationWarehouseId}
                onChange={(e) => setDestinationWarehouseId(e.target.value)}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              >
                <option value="">Select destination...</option>
                {warehouses.map((w) => (
                  <option key={w.id} value={w.id}>
                    {w.code} — {w.name}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="grid grid-cols-3 gap-3">
            <div className="col-span-2">
              <label className="block font-medium text-slate-700">
                Product SKU *
              </label>
              <select
                required
                value={productId}
                onChange={(e) => setProductId(e.target.value)}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              >
                <option value="">Select product...</option>
                {products.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.sku} — {p.name}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="block font-medium text-slate-700">
                Quantity *
              </label>
              <input
                type="number"
                min={1}
                required
                value={quantity}
                onChange={(e) => setQuantity(Number(e.target.value))}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
          </div>

          <div>
            <label className="block font-medium text-slate-700">Notes</label>
            <textarea
              rows={2}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            />
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={() => setCreateOpen(false)}
              className="rounded border border-slate-300 px-3 py-1.5 text-slate-700"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="rounded bg-slate-900 px-3.5 py-1.5 font-medium text-white hover:bg-slate-800 disabled:opacity-50"
            >
              {submitting ? "Creating..." : "Create Transfer Request"}
            </button>
          </div>
        </form>
      </ModalDialog>

      <ConfirmDialog
        open={!!cancellingTransfer}
        title={`Cancel Transfer ${cancellingTransfer?.transferNumber}?`}
        description={
          cancellingTransfer?.status === "InTransit"
            ? "This transfer is currently In-Transit. Cancelling it will atomically reverse the in-transit balance and restore the on-hand stock at the source warehouse."
            : "Are you sure you want to cancel this stock transfer request?"
        }
        confirmLabel="Confirm Cancellation"
        isSubmitting={submitting}
        onConfirm={handleConfirmCancel}
        onCancel={() => setCancellingTransfer(null)}
      />
    </div>
  );
}
