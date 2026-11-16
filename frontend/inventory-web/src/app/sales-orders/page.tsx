"use client";

import React, { useCallback, useEffect, useState } from "react";
import { apiRequest, formatCurrency, formatDateTime } from "@/lib/api-client";
import {
  PagedResponse,
  ProductDto,
  SalesOrderDto,
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

export default function SalesOrdersPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("sales-orders:write");
  const canConfirm = hasPermission("sales-orders:confirm");
  const canFulfill = hasPermission("sales-orders:fulfill");
  const canCancel = hasPermission("sales-orders:cancel");

  const [data, setData] = useState<PagedResponse<SalesOrderDto> | null>(null);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [products, setProducts] = useState<ProductDto[]>([]);
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Create SO Modal State
  const [createOpen, setCreateOpen] = useState(false);
  const [customerName, setCustomerName] = useState("");
  const [customerEmail, setCustomerEmail] = useState("");
  const [shippingAddress, setShippingAddress] = useState("");
  const [fulfillmentWarehouseId, setFulfillmentWarehouseId] = useState("");
  const [selectedProductId, setSelectedProductId] = useState("");
  const [quantity, setQuantity] = useState(5);
  const [confirmImmediately, setConfirmImmediately] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  // Cancel Confirmation
  const [cancellingOrder, setCancellingOrder] =
    useState<SalesOrderDto | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams({
        pageNumber: String(page),
        pageSize: "15"
      });
      if (statusFilter) params.set("status", statusFilter);

      const [soRes, whRes, prodRes] = await Promise.all([
        apiRequest<PagedResponse<SalesOrderDto>>(
          `/sales-orders?${params.toString()}`
        ),
        apiRequest<WarehouseDto[]>("/warehouses"),
        apiRequest<PagedResponse<ProductDto>>("/products?pageSize=100")
      ]);
      setData(soRes);
      setWarehouses(whRes);
      setProducts(prodRes.items);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to load sales orders."
      );
    } finally {
      setLoading(false);
    }
  }, [page, statusFilter]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const openCreateModal = () => {
    setCustomerName("");
    setCustomerEmail("");
    setShippingAddress("");
    setFulfillmentWarehouseId(warehouses[0]?.id || "");
    setSelectedProductId(products[0]?.id || "");
    setQuantity(5);
    setConfirmImmediately(true);
    setCreateOpen(true);
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await apiRequest("/sales-orders", {
        method: "POST",
        idempotent: true,
        body: JSON.stringify({
          customerName,
          customerEmail: customerEmail || null,
          customerPhone: null,
          shippingAddress: shippingAddress || null,
          fulfillmentWarehouseId,
          taxRatePercent: null,
          discountAmount: 0,
          notes: null,
          confirmImmediately,
          lines: [{ productId: selectedProductId, quantity: Number(quantity) }]
        })
      });
      setCreateOpen(false);
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to create sales order."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleStateAction = async (
    orderId: string,
    action: "confirm" | "process" | "fulfill"
  ) => {
    setError(null);
    try {
      await apiRequest(`/sales-orders/${orderId}/${action}`, {
        method: "POST",
        idempotent: action !== "process"
      });
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : `Failed to ${action} sales order.`
      );
    }
  };

  const handleConfirmCancel = async () => {
    if (!cancellingOrder) return;
    setSubmitting(true);
    setError(null);
    try {
      await apiRequest(`/sales-orders/${cancellingOrder.id}/cancel`, {
        method: "POST",
        idempotent: true,
        body: JSON.stringify({
          reason: "Cancelled via sales order dashboard"
        })
      });
      setCancellingOrder(null);
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to cancel sales order."
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="Customer Sales Orders & Stock Reservations"
        description="All order totals are calculated on the backend. Confirming an order atomically locks and reserves available warehouse stock."
        actions={
          canWrite && (
            <button
              type="button"
              onClick={openCreateModal}
              className="rounded-md bg-slate-900 px-3.5 py-2 text-xs font-medium text-white hover:bg-slate-800"
            >
              + Create Sales Order
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
            <option value="Pending">Pending</option>
            <option value="Confirmed">Confirmed (Reserved)</option>
            <option value="Processing">Processing</option>
            <option value="Fulfilled">Fulfilled</option>
            <option value="Cancelled">Cancelled</option>
          </select>
        </div>
      </Card>

      <Card>
        {loading ? (
          <LoadingState label="Loading sales orders..." />
        ) : !data || data.items.length === 0 ? (
          <EmptyState
            title="No sales orders found"
            description="Create a sales order to reserve inventory and fulfill customer demand."
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
                  <tr>
                    <th className="px-4 py-2.5">Order #</th>
                    <th className="px-4 py-2.5">Customer</th>
                    <th className="px-4 py-2.5">Warehouse</th>
                    <th className="px-4 py-2.5">Line Items</th>
                    <th className="px-4 py-2.5 text-right">Subtotal / Tax</th>
                    <th className="px-4 py-2.5 text-right">Total</th>
                    <th className="px-4 py-2.5">Status</th>
                    <th className="px-4 py-2.5">Created</th>
                    <th className="px-4 py-2.5 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {data.items.map((so) => (
                    <tr key={so.id} className="hover:bg-slate-50">
                      <td className="px-4 py-2.5 font-mono font-semibold text-slate-900">
                        {so.orderNumber}
                      </td>
                      <td className="px-4 py-2.5">
                        <div className="font-medium text-slate-900">
                          {so.customerName}
                        </div>
                        {so.customerEmail && (
                          <div className="text-slate-500">
                            {so.customerEmail}
                          </div>
                        )}
                      </td>
                      <td className="px-4 py-2.5 font-mono">
                        {so.fulfillmentWarehouseCode}
                      </td>
                      <td className="px-4 py-2.5">
                        {so.lines.map((l) => (
                          <div key={l.id}>
                            <span className="font-medium text-slate-900">
                              {l.productSku}
                            </span>{" "}
                            × {l.quantity} @ {formatCurrency(l.unitPrice)}
                          </div>
                        ))}
                      </td>
                      <td className="px-4 py-2.5 text-right text-slate-600">
                        {formatCurrency(so.subtotalAmount)} +{" "}
                        {formatCurrency(so.taxAmount)}
                      </td>
                      <td className="px-4 py-2.5 text-right font-semibold text-slate-900">
                        {formatCurrency(so.totalAmount)}
                      </td>
                      <td className="px-4 py-2.5">
                        <Badge
                          variant={
                            so.status === "Fulfilled"
                              ? "success"
                              : so.status === "Confirmed" ||
                                so.status === "Processing"
                              ? "info"
                              : so.status === "Cancelled"
                              ? "danger"
                              : "warning"
                          }
                        >
                          {so.status}
                        </Badge>
                      </td>
                      <td className="px-4 py-2.5 text-slate-500">
                        {formatDateTime(so.createdAtUtc)}
                      </td>
                      <td className="px-4 py-2.5 text-right space-x-2">
                        {(so.status === "Draft" || so.status === "Pending") &&
                          canConfirm && (
                            <button
                              type="button"
                              onClick={() =>
                                handleStateAction(so.id, "confirm")
                              }
                              className="rounded bg-blue-600 px-2.5 py-1 text-[11px] font-medium text-white hover:bg-blue-700"
                            >
                              Confirm & Reserve
                            </button>
                          )}
                        {so.status === "Confirmed" && canFulfill && (
                          <button
                            type="button"
                            onClick={() => handleStateAction(so.id, "process")}
                            className="rounded border border-slate-300 bg-white px-2 py-1 text-[11px] font-medium text-slate-700 hover:bg-slate-100"
                          >
                            Start Picking
                          </button>
                        )}
                        {(so.status === "Confirmed" ||
                          so.status === "Processing") &&
                          canFulfill && (
                            <button
                              type="button"
                              onClick={() =>
                                handleStateAction(so.id, "fulfill")
                              }
                              className="rounded bg-emerald-600 px-2.5 py-1 text-[11px] font-medium text-white hover:bg-emerald-700"
                            >
                              Fulfill Order
                            </button>
                          )}
                        {so.status !== "Fulfilled" &&
                          so.status !== "Cancelled" &&
                          canCancel && (
                            <button
                              type="button"
                              onClick={() => setCancellingOrder(so)}
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

      {/* Create Sales Order Modal */}
      <ModalDialog
        open={createOpen}
        title="Create Customer Sales Order"
        onClose={() => setCreateOpen(false)}
      >
        <form onSubmit={handleCreate} className="space-y-3 text-xs">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block font-medium text-slate-700">
                Customer Name *
              </label>
              <input
                type="text"
                required
                value={customerName}
                onChange={(e) => setCustomerName(e.target.value)}
                placeholder="Midwest Robotics Corp"
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
            <div>
              <label className="block font-medium text-slate-700">
                Customer Email
              </label>
              <input
                type="email"
                value={customerEmail}
                onChange={(e) => setCustomerEmail(e.target.value)}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              />
            </div>
          </div>

          <div>
            <label className="block font-medium text-slate-700">
              Fulfillment Warehouse *
            </label>
            <select
              required
              value={fulfillmentWarehouseId}
              onChange={(e) => setFulfillmentWarehouseId(e.target.value)}
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

          <div className="grid grid-cols-3 gap-3">
            <div className="col-span-2">
              <label className="block font-medium text-slate-700">
                Product SKU (Authoritative Server Price) *
              </label>
              <select
                required
                value={selectedProductId}
                onChange={(e) => setSelectedProductId(e.target.value)}
                className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
              >
                <option value="">Select product...</option>
                {products.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.sku} — {p.name} ({formatCurrency(p.unitPrice)})
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
            <label className="block font-medium text-slate-700">
              Shipping Address
            </label>
            <input
              type="text"
              value={shippingAddress}
              onChange={(e) => setShippingAddress(e.target.value)}
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            />
          </div>

          <div className="flex items-center gap-2 pt-1">
            <input
              type="checkbox"
              id="confirmImmediate"
              checked={confirmImmediately}
              onChange={(e) => setConfirmImmediately(e.target.checked)}
            />
            <label
              htmlFor="confirmImmediate"
              className="font-medium text-slate-700"
            >
              Confirm immediately & reserve available warehouse stock
            </label>
          </div>

          <div className="flex justify-end gap-2 pt-3">
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
              {submitting ? "Processing..." : "Create Sales Order"}
            </button>
          </div>
        </form>
      </ModalDialog>

      <ConfirmDialog
        open={!!cancellingOrder}
        title={`Cancel Sales Order ${cancellingOrder?.orderNumber}?`}
        description="If this order holds active stock reservations, cancelling it will atomically release the reserved stock back to available warehouse inventory."
        confirmLabel="Cancel Order & Release Stock"
        isSubmitting={submitting}
        onConfirm={handleConfirmCancel}
        onCancel={() => setCancellingOrder(null)}
      />
    </div>
  );
}
