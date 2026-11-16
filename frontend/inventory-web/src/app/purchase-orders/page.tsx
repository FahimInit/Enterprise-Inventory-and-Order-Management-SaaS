"use client";

import React, { useCallback, useEffect, useState } from "react";
import { apiRequest, formatCurrency, formatDateTime } from "@/lib/api-client";
import {
  PagedResponse,
  ProductDto,
  PurchaseOrderDto,
  SupplierDto,
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

export default function PurchaseOrdersPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("purchase-orders:write");
  const canApprove = hasPermission("purchase-orders:approve");
  const canReceive = hasPermission("purchase-orders:receive");

  const [data, setData] = useState<PagedResponse<PurchaseOrderDto> | null>(
    null
  );
  const [suppliers, setSuppliers] = useState<SupplierDto[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [products, setProducts] = useState<ProductDto[]>([]);
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Create PO Modal State
  const [createOpen, setCreateOpen] = useState(false);
  const [supplierId, setSupplierId] = useState("");
  const [targetWarehouseId, setTargetWarehouseId] = useState("");
  const [selectedProductId, setSelectedProductId] = useState("");
  const [orderedQty, setOrderedQty] = useState(25);
  const [unitCost, setUnitCost] = useState(100);
  const [notes, setNotes] = useState("");
  const [submitting, setSubmitting] = useState(false);

  // Goods Receipt Modal State
  const [receivingPo, setReceivingPo] = useState<PurchaseOrderDto | null>(null);
  const [receiptQuantities, setReceiptQuantities] = useState<
    Record<string, number>
  >({});

  // Cancel Confirmation
  const [cancellingPo, setCancellingPo] = useState<PurchaseOrderDto | null>(
    null
  );

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams({
        pageNumber: String(page),
        pageSize: "15"
      });
      if (statusFilter) params.set("status", statusFilter);

      const [poRes, supRes, whRes, prodRes] = await Promise.all([
        apiRequest<PagedResponse<PurchaseOrderDto>>(
          `/purchase-orders?${params.toString()}`
        ),
        apiRequest<PagedResponse<SupplierDto>>("/suppliers?pageSize=100"),
        apiRequest<WarehouseDto[]>("/warehouses"),
        apiRequest<PagedResponse<ProductDto>>("/products?pageSize=100")
      ]);
      setData(poRes);
      setSuppliers(supRes.items);
      setWarehouses(whRes);
      setProducts(prodRes.items);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to load purchase orders."
      );
    } finally {
      setLoading(false);
    }
  }, [page, statusFilter]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const openCreateModal = () => {
    setSupplierId(suppliers[0]?.id || "");
    setTargetWarehouseId(warehouses[0]?.id || "");
    const firstProd = products[0];
    setSelectedProductId(firstProd?.id || "");
    setUnitCost(firstProd?.standardCost || 50);
    setOrderedQty(25);
    setNotes("");
    setCreateOpen(true);
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await apiRequest("/purchase-orders", {
        method: "POST",
        idempotent: true,
        body: JSON.stringify({
          supplierId,
          targetWarehouseId,
          expectedDeliveryDateUtc: new Date(
            Date.now() + 7 * 86400000
          ).toISOString(),
          taxRatePercent: 0,
          notes: notes || null,
          lines: [
            {
              productId: selectedProductId,
              orderedQuantity: Number(orderedQty),
              unitCost: Number(unitCost)
            }
          ]
        })
      });
      setCreateOpen(false);
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to create purchase order."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleAction = async (poId: string, action: "submit" | "approve") => {
    setError(null);
    try {
      await apiRequest(`/purchase-orders/${poId}/${action}`, {
        method: "POST"
      });
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : `Failed to ${action} PO.`);
    }
  };

  const openReceiveModal = (po: PurchaseOrderDto) => {
    setReceivingPo(po);
    const map: Record<string, number> = {};
    po.lines.forEach((l) => {
      map[l.id] = l.remainingQuantity;
    });
    setReceiptQuantities(map);
  };

  const handleReceiveSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!receivingPo) return;
    setSubmitting(true);
    setError(null);
    try {
      const lines = Object.entries(receiptQuantities)
        .filter(([, qty]) => qty > 0)
        .map(([purchaseOrderLineId, quantityReceived]) => ({
          purchaseOrderLineId,
          quantityReceived: Number(quantityReceived)
        }));

      await apiRequest(`/purchase-orders/${receivingPo.id}/receive`, {
        method: "POST",
        idempotent: true,
        body: JSON.stringify({
          notes: "Warehouse goods receipt via dashboard",
          lines
        })
      });
      setReceivingPo(null);
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Goods receipt failed.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleConfirmCancel = async () => {
    if (!cancellingPo) return;
    setSubmitting(true);
    setError(null);
    try {
      await apiRequest(`/purchase-orders/${cancellingPo.id}/cancel`, {
        method: "POST",
        body: JSON.stringify({
          reason: "Cancelled by procurement user"
        })
      });
      setCancellingPo(null);
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to cancel PO.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="Purchase Orders & Goods Receiving"
        description="Procure inventory from suppliers, manage approval workflows, and record partial or full goods receipts into warehouse stock."
        actions={
          canWrite && (
            <button
              type="button"
              onClick={openCreateModal}
              className="rounded-md bg-slate-900 px-3.5 py-2 text-xs font-medium text-white hover:bg-slate-800"
            >
              + Create Purchase Order
            </button>
          )
        }
      />

      <ErrorBanner message={error} />

      <Card className="mb-4 p-3">
        <div className="flex items-center gap-3">
          <label className="text-xs font-medium text-slate-700">
            Filter by Status:
          </label>
          <select
            value={statusFilter}
            onChange={(e) => {
              setPage(1);
              setStatusFilter(e.target.value);
            }}
            className="rounded-md border border-slate-300 px-3 py-1.5 text-xs"
          >
            <option value="">All Statuses</option>
            <option value="Draft">Draft</option>
            <option value="Submitted">Submitted</option>
            <option value="Approved">Approved</option>
            <option value="PartiallyReceived">PartiallyReceived</option>
            <option value="Received">Received</option>
            <option value="Cancelled">Cancelled</option>
          </select>
        </div>
      </Card>

      <Card>
        {loading ? (
          <LoadingState label="Loading purchase orders..." />
        ) : !data || data.items.length === 0 ? (
          <EmptyState
            title="No purchase orders found"
            description="Create a purchase order to replenish warehouse stock from your suppliers."
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
                  <tr>
                    <th className="px-4 py-2.5">PO Number</th>
                    <th className="px-4 py-2.5">Supplier</th>
                    <th className="px-4 py-2.5">Target Warehouse</th>
                    <th className="px-4 py-2.5">Lines (Received / Ordered)</th>
                    <th className="px-4 py-2.5 text-right">Total Amount</th>
                    <th className="px-4 py-2.5">Status</th>
                    <th className="px-4 py-2.5">Created</th>
                    <th className="px-4 py-2.5 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {data.items.map((po) => (
                    <tr key={po.id} className="hover:bg-slate-50">
                      <td className="px-4 py-2.5 font-mono font-semibold text-slate-900">
                        {po.orderNumber}
                      </td>
                      <td className="px-4 py-2.5">
                        <div className="font-medium text-slate-900">
                          {po.supplierName}
                        </div>
                        <div className="text-slate-500">{po.supplierCode}</div>
                      </td>
                      <td className="px-4 py-2.5 font-mono">
                        {po.targetWarehouseCode}
                      </td>
                      <td className="px-4 py-2.5">
                        {po.lines.map((l) => (
                          <div key={l.id}>
                            <span className="font-medium text-slate-900">
                              {l.productSku}
                            </span>
                            : {l.receivedQuantity} / {l.orderedQuantity}{" "}
                            {l.unitOfMeasure} @ {formatCurrency(l.unitCost)}
                          </div>
                        ))}
                      </td>
                      <td className="px-4 py-2.5 text-right font-semibold text-slate-900">
                        {formatCurrency(po.totalAmount)}
                      </td>
                      <td className="px-4 py-2.5">
                        <Badge
                          variant={
                            po.status === "Received"
                              ? "success"
                              : po.status === "Approved" ||
                                po.status === "PartiallyReceived"
                              ? "info"
                              : po.status === "Cancelled"
                              ? "danger"
                              : "warning"
                          }
                        >
                          {po.status}
                        </Badge>
                      </td>
                      <td className="px-4 py-2.5 text-slate-500">
                        {formatDateTime(po.createdAtUtc)}
                      </td>
                      <td className="px-4 py-2.5 text-right space-x-2">
                        {po.status === "Draft" && canWrite && (
                          <button
                            type="button"
                            onClick={() => handleAction(po.id, "submit")}
                            className="rounded border border-slate-300 bg-white px-2 py-1 text-[11px] font-medium text-slate-700 hover:bg-slate-100"
                          >
                            Submit
                          </button>
                        )}
                        {(po.status === "Draft" || po.status === "Submitted") &&
                          canApprove && (
                            <button
                              type="button"
                              onClick={() => handleAction(po.id, "approve")}
                              className="rounded bg-blue-600 px-2 py-1 text-[11px] font-medium text-white hover:bg-blue-700"
                            >
                              Approve
                            </button>
                          )}
                        {(po.status === "Approved" ||
                          po.status === "PartiallyReceived") &&
                          canReceive && (
                            <button
                              type="button"
                              onClick={() => openReceiveModal(po)}
                              className="rounded bg-emerald-600 px-2.5 py-1 text-[11px] font-medium text-white hover:bg-emerald-700"
                            >
                              Receive Goods
                            </button>
                          )}
                        {(po.status === "Draft" ||
                          po.status === "Submitted" ||
                          po.status === "Approved") &&
                          canApprove && (
                            <button
                              type="button"
                              onClick={() => setCancellingPo(po)}
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

      {/* Create PO Modal */}
      <ModalDialog
        open={createOpen}
        title="Create Purchase Order"
        onClose={() => setCreateOpen(false)}
      >
        <form onSubmit={handleCreate} className="space-y-3 text-xs">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-medium text-slate-700">
                Supplier *
              </label>
              <select
                required
                value={supplierId}
                onChange={(e) => setSupplierId(e.target.value)}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              >
                <option value="">Select supplier...</option>
                {suppliers.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.code} — {s.name}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="block font-medium text-slate-700">
                Target Warehouse *
              </label>
              <select
                required
                value={targetWarehouseId}
                onChange={(e) => setTargetWarehouseId(e.target.value)}
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
          </div>

          <div>
            <label className="block font-medium text-slate-700">
              Product SKU *
            </label>
            <select
              required
              value={selectedProductId}
              onChange={(e) => {
                setSelectedProductId(e.target.value);
                const prod = products.find((p) => p.id === e.target.value);
                if (prod) setUnitCost(prod.standardCost);
              }}
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

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-medium text-slate-700">
                Ordered Quantity *
              </label>
              <input
                type="number"
                min={1}
                required
                value={orderedQty}
                onChange={(e) => setOrderedQty(Number(e.target.value))}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
            <div>
              <label className="block font-medium text-slate-700">
                Unit Cost *
              </label>
              <input
                type="number"
                step="0.01"
                min={0}
                required
                value={unitCost}
                onChange={(e) => setUnitCost(Number(e.target.value))}
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
              {submitting ? "Creating..." : "Create Draft PO"}
            </button>
          </div>
        </form>
      </ModalDialog>

      {/* Goods Receipt Modal */}
      <ModalDialog
        open={!!receivingPo}
        title={
          receivingPo
            ? `Receive Purchased Goods — ${receivingPo.orderNumber}`
            : "Receive Goods"
        }
        onClose={() => setReceivingPo(null)}
      >
        <form onSubmit={handleReceiveSubmit} className="space-y-4 text-xs">
          <p className="text-slate-600">
            Enter the physical quantity received for each line item. Partial
            receipts will transition the PO to{" "}
            <strong>PartiallyReceived</strong>.
          </p>

          <div className="space-y-2">
            {receivingPo?.lines.map((line) => (
              <div
                key={line.id}
                className="flex items-center justify-between rounded border border-slate-200 p-2.5"
              >
                <div>
                  <div className="font-semibold text-slate-900">
                    {line.productSku} — {line.productName}
                  </div>
                  <div className="text-slate-500">
                    Ordered: {line.orderedQuantity} | Already Received:{" "}
                    {line.receivedQuantity} | Remaining:{" "}
                    <strong>{line.remainingQuantity}</strong>
                  </div>
                </div>
                <input
                  type="number"
                  min={0}
                  max={line.remainingQuantity}
                  value={receiptQuantities[line.id] ?? 0}
                  onChange={(e) =>
                    setReceiptQuantities({
                      ...receiptQuantities,
                      [line.id]: Number(e.target.value)
                    })
                  }
                  className="w-24 rounded border border-slate-300 px-2 py-1 text-right font-medium"
                />
              </div>
            ))}
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={() => setReceivingPo(null)}
              className="rounded border border-slate-300 px-3 py-1.5 text-slate-700"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="rounded bg-emerald-600 px-3.5 py-1.5 font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
            >
              {submitting ? "Recording Goods Receipt..." : "Post Goods Receipt"}
            </button>
          </div>
        </form>
      </ModalDialog>

      <ConfirmDialog
        open={!!cancellingPo}
        title={`Cancel Purchase Order ${cancellingPo?.orderNumber}?`}
        description="Cancelling this Purchase Order will permanently close it. Only unreceived Purchase Orders can be cancelled."
        confirmLabel="Cancel Purchase Order"
        isSubmitting={submitting}
        onConfirm={handleConfirmCancel}
        onCancel={() => setCancellingPo(null)}
      />
    </div>
  );
}
