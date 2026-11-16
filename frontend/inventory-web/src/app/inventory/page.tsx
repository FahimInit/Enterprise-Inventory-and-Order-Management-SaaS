"use client";

import React, { useCallback, useEffect, useState } from "react";
import { apiRequest, formatCurrency, formatDateTime } from "@/lib/api-client";
import {
  InventoryBalanceDto,
  PagedResponse,
  ProductDto,
  ReconciliationResultDto,
  WarehouseDto
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

export default function InventoryOverviewPage() {
  const { hasPermission } = useAuth();
  const canAdjust = hasPermission("inventory:adjust");
  const canReconcile = hasPermission("inventory:reconcile");

  const [data, setData] = useState<PagedResponse<InventoryBalanceDto> | null>(
    null
  );
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [products, setProducts] = useState<ProductDto[]>([]);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [warehouseFilter, setWarehouseFilter] = useState("");
  const [lowStockOnly, setLowStockOnly] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reconcileNotice, setReconcileNotice] = useState<string | null>(null);

  // Adjustment Modal State
  const [adjOpen, setAdjOpen] = useState(false);
  const [adjWarehouseId, setAdjWarehouseId] = useState("");
  const [adjProductId, setAdjProductId] = useState("");
  const [adjDelta, setAdjDelta] = useState(0);
  const [adjReason, setAdjReason] = useState("");
  const [adjNotes, setAdjNotes] = useState("");
  const [submitting, setSubmitting] = useState(false);

  // Reconciliation Modal State
  const [recOpen, setRecOpen] = useState(false);
  const [recWarehouseId, setRecWarehouseId] = useState("");
  const [recProductId, setRecProductId] = useState("");
  const [recCountedQty, setRecCountedQty] = useState(0);
  const [recReason, setRecReason] = useState("Scheduled physical cycle count");

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams({
        pageNumber: String(page),
        pageSize: "15"
      });
      if (search.trim()) params.set("search", search.trim());
      if (warehouseFilter) params.set("warehouseId", warehouseFilter);
      if (lowStockOnly) params.set("lowStockOnly", "true");

      const [invRes, whRes, prodRes] = await Promise.all([
        apiRequest<PagedResponse<InventoryBalanceDto>>(
          `/inventory?${params.toString()}`
        ),
        apiRequest<WarehouseDto[]>("/warehouses"),
        apiRequest<PagedResponse<ProductDto>>("/products?pageSize=100")
      ]);
      setData(invRes);
      setWarehouses(whRes);
      setProducts(prodRes.items);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to load inventory."
      );
    } finally {
      setLoading(false);
    }
  }, [page, search, warehouseFilter, lowStockOnly]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const openAdjustmentModal = (row?: InventoryBalanceDto) => {
    setAdjWarehouseId(row?.warehouseId || warehouses[0]?.id || "");
    setAdjProductId(row?.productId || products[0]?.id || "");
    setAdjDelta(0);
    setAdjReason("");
    setAdjNotes("");
    setAdjOpen(true);
  };

  const openReconcileModal = (row: InventoryBalanceDto) => {
    setRecWarehouseId(row.warehouseId);
    setRecProductId(row.productId);
    setRecCountedQty(row.onHandQuantity);
    setRecReason("Scheduled physical cycle count");
    setRecOpen(true);
  };

  const handleAdjustmentSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await apiRequest("/inventory/adjustments", {
        method: "POST",
        idempotent: true,
        body: JSON.stringify({
          warehouseId: adjWarehouseId,
          productId: adjProductId,
          quantityDelta: Number(adjDelta),
          reason: adjReason,
          notes: adjNotes || null
        })
      });
      setAdjOpen(false);
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Stock adjustment failed.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleReconcileSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const result = await apiRequest<ReconciliationResultDto>(
        "/inventory/reconcile",
        {
          method: "POST",
          idempotent: true,
          body: JSON.stringify({
            warehouseId: recWarehouseId,
            productId: recProductId,
            countedPhysicalQuantity: Number(recCountedQty),
            reason: recReason
          })
        }
      );
      setRecOpen(false);
      setReconcileNotice(
        `Reconciliation complete for ${result.productSku}: System Before=${result.systemQuantityBefore}, Ledger Sum=${result.ledgerCalculatedQuantity}, Physical Count=${result.countedQuantity}, Variance=${result.varianceQuantity}.`
      );
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Inventory reconciliation failed."
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="Real-Time Inventory Balances"
        description="Authoritative stock positions per warehouse: Available Stock = On-Hand Stock - Reserved Stock."
        actions={
          canAdjust && (
            <button
              type="button"
              onClick={() => openAdjustmentModal()}
              className="rounded-md bg-slate-900 px-3.5 py-2 text-xs font-medium text-white hover:bg-slate-800"
            >
              + Record Stock Adjustment
            </button>
          )
        }
      />

      <ErrorBanner message={error} />

      {reconcileNotice && (
        <div className="mb-4 rounded-md border border-emerald-200 bg-emerald-50 p-3 text-xs text-emerald-900">
          {reconcileNotice}
        </div>
      )}

      <Card className="mb-4 p-3">
        <div className="flex flex-wrap items-center gap-3">
          <input
            type="text"
            value={search}
            onChange={(e) => {
              setPage(1);
              setSearch(e.target.value);
            }}
            placeholder="Filter by SKU, product name, or warehouse..."
            className="min-w-[240px] flex-1 rounded-md border border-slate-300 px-3 py-1.5 text-xs"
          />
          <select
            value={warehouseFilter}
            onChange={(e) => {
              setPage(1);
              setWarehouseFilter(e.target.value);
            }}
            className="rounded-md border border-slate-300 px-3 py-1.5 text-xs"
          >
            <option value="">All Warehouses</option>
            {warehouses.map((w) => (
              <option key={w.id} value={w.id}>
                {w.code} — {w.name}
              </option>
            ))}
          </select>
          <label className="inline-flex items-center gap-1.5 text-xs font-medium text-slate-700">
            <input
              type="checkbox"
              checked={lowStockOnly}
              onChange={(e) => {
                setPage(1);
                setLowStockOnly(e.target.checked);
              }}
            />
            Low-Stock Items Only
          </label>
        </div>
      </Card>

      <Card>
        {loading ? (
          <LoadingState label="Loading inventory balances..." />
        ) : !data || data.items.length === 0 ? (
          <EmptyState
            title="No inventory balances found"
            description="Receive a Purchase Order or record an initial stock adjustment to populate warehouse stock."
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
                  <tr>
                    <th className="px-4 py-2.5">Warehouse</th>
                    <th className="px-4 py-2.5">SKU / Product</th>
                    <th className="px-4 py-2.5 text-right">On-Hand</th>
                    <th className="px-4 py-2.5 text-right">Reserved</th>
                    <th className="px-4 py-2.5 text-right">Available</th>
                    <th className="px-4 py-2.5 text-right">In-Transit</th>
                    <th className="px-4 py-2.5 text-right">Avg Cost</th>
                    <th className="px-4 py-2.5 text-right">Total Value</th>
                    <th className="px-4 py-2.5">Health</th>
                    <th className="px-4 py-2.5">Last Counted</th>
                    <th className="px-4 py-2.5 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {data.items.map((row) => (
                    <tr key={row.inventoryItemId} className="hover:bg-slate-50">
                      <td className="px-4 py-2.5 font-mono font-semibold text-slate-900">
                        {row.warehouseCode}
                      </td>
                      <td className="px-4 py-2.5">
                        <div className="font-medium text-slate-900">
                          {row.productSku}
                        </div>
                        <div className="text-slate-500">{row.productName}</div>
                      </td>
                      <td className="px-4 py-2.5 text-right font-medium">
                        {row.onHandQuantity} {row.unitOfMeasure}
                      </td>
                      <td className="px-4 py-2.5 text-right text-amber-700">
                        {row.reservedQuantity}
                      </td>
                      <td className="px-4 py-2.5 text-right font-semibold text-emerald-700">
                        {row.availableQuantity}
                      </td>
                      <td className="px-4 py-2.5 text-right text-blue-700">
                        {row.inTransitQuantity}
                      </td>
                      <td className="px-4 py-2.5 text-right">
                        {formatCurrency(row.movingAverageCost)}
                      </td>
                      <td className="px-4 py-2.5 text-right font-medium">
                        {formatCurrency(row.totalInventoryValue)}
                      </td>
                      <td className="px-4 py-2.5">
                        <Badge variant={row.isLowStock ? "warning" : "success"}>
                          {row.isLowStock
                            ? `Low (<= ${row.reorderPoint})`
                            : "Optimal"}
                        </Badge>
                      </td>
                      <td className="px-4 py-2.5 text-slate-500">
                        {formatDateTime(row.lastCountedAtUtc)}
                      </td>
                      <td className="px-4 py-2.5 text-right space-x-2">
                        {canAdjust && (
                          <button
                            type="button"
                            onClick={() => openAdjustmentModal(row)}
                            className="font-medium text-blue-600 hover:underline"
                          >
                            Adjust
                          </button>
                        )}
                        {canReconcile && (
                          <button
                            type="button"
                            onClick={() => openReconcileModal(row)}
                            className="font-medium text-slate-700 hover:underline"
                          >
                            Reconcile
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

      {/* Controlled Stock Adjustment Modal */}
      <ModalDialog
        open={adjOpen}
        title="Controlled Stock Adjustment"
        onClose={() => setAdjOpen(false)}
      >
        <form onSubmit={handleAdjustmentSubmit} className="space-y-3 text-xs">
          <div>
            <label className="block font-medium text-slate-700">
              Warehouse *
            </label>
            <select
              required
              value={adjWarehouseId}
              onChange={(e) => setAdjWarehouseId(e.target.value)}
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            >
              <option value="">Select warehouse...</option>
              {warehouses.map((w) => (
                <option key={w.id} value={w.id}>
                  {w.code} — {w.name}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="block font-medium text-slate-700">
              Product SKU *
            </label>
            <select
              required
              value={adjProductId}
              onChange={(e) => setAdjProductId(e.target.value)}
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
              Quantity Delta (+ to add, - to deduct) *
            </label>
            <input
              type="number"
              required
              value={adjDelta}
              onChange={(e) => setAdjDelta(Number(e.target.value))}
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            />
          </div>

          <div>
            <label className="block font-medium text-slate-700">
              Business Reason *
            </label>
            <input
              type="text"
              required
              value={adjReason}
              onChange={(e) => setAdjReason(e.target.value)}
              placeholder="Damaged in handling / Initial seed / Shrinkage"
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            />
          </div>

          <div>
            <label className="block font-medium text-slate-700">
              Reference Notes
            </label>
            <textarea
              rows={2}
              value={adjNotes}
              onChange={(e) => setAdjNotes(e.target.value)}
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            />
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={() => setAdjOpen(false)}
              className="rounded border border-slate-300 px-3 py-1.5 text-slate-700"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="rounded bg-slate-900 px-3.5 py-1.5 font-medium text-white hover:bg-slate-800 disabled:opacity-50"
            >
              {submitting ? "Recording Ledger Entry..." : "Commit Adjustment"}
            </button>
          </div>
        </form>
      </ModalDialog>

      {/* Physical Cycle Count Reconciliation Modal */}
      <ModalDialog
        open={recOpen}
        title="Inventory Cycle Count & Ledger Reconciliation"
        onClose={() => setRecOpen(false)}
      >
        <form onSubmit={handleReconcileSubmit} className="space-y-3 text-xs">
          <p className="text-slate-600">
            Verifies materialized balance against the sum of historical ledger
            movements and reconciles any variance with the physical count.
          </p>

          <div>
            <label className="block font-medium text-slate-700">
              Counted Physical Quantity *
            </label>
            <input
              type="number"
              min={0}
              required
              value={recCountedQty}
              onChange={(e) => setRecCountedQty(Number(e.target.value))}
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            />
          </div>

          <div>
            <label className="block font-medium text-slate-700">
              Reconciliation Reason *
            </label>
            <input
              type="text"
              required
              value={recReason}
              onChange={(e) => setRecReason(e.target.value)}
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            />
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={() => setRecOpen(false)}
              className="rounded border border-slate-300 px-3 py-1.5 text-slate-700"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="rounded bg-slate-900 px-3.5 py-1.5 font-medium text-white hover:bg-slate-800 disabled:opacity-50"
            >
              {submitting ? "Reconciling..." : "Run Reconciliation"}
            </button>
          </div>
        </form>
      </ModalDialog>
    </div>
  );
}
