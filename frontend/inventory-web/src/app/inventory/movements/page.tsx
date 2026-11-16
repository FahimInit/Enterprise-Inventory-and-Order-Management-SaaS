"use client";

import React, { useCallback, useEffect, useState } from "react";
import { apiRequest, formatCurrency, formatDateTime } from "@/lib/api-client";
import {
  PagedResponse,
  StockMovementDto,
  WarehouseDto
} from "@/lib/types";
import {
  Badge,
  Card,
  EmptyState,
  ErrorBanner,
  LoadingState,
  PageHeader,
  PaginationControls
} from "@/components/ui/enterprise-ui";

export default function StockMovementsPage() {
  const [data, setData] = useState<PagedResponse<StockMovementDto> | null>(
    null
  );
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [warehouseFilter, setWarehouseFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadMovements = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams({
        pageNumber: String(page),
        pageSize: "20"
      });
      if (search.trim()) params.set("search", search.trim());
      if (warehouseFilter) params.set("warehouseId", warehouseFilter);
      if (typeFilter) params.set("movementType", typeFilter);

      const [movRes, whRes] = await Promise.all([
        apiRequest<PagedResponse<StockMovementDto>>(
          `/inventory/movements?${params.toString()}`
        ),
        apiRequest<WarehouseDto[]>("/warehouses")
      ]);
      setData(movRes);
      setWarehouses(whRes);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to load stock movements."
      );
    } finally {
      setLoading(false);
    }
  }, [page, search, warehouseFilter, typeFilter]);

  useEffect(() => {
    loadMovements();
  }, [loadMovements]);

  return (
    <div>
      <PageHeader
        title="Immutable Stock Movement Ledger"
        description="Complete, append-only audit trail explaining every historical change to warehouse on-hand stock."
      />

      <ErrorBanner message={error} />

      <Card className="mb-4 p-3">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <input
            type="text"
            value={search}
            onChange={(e) => {
              setPage(1);
              setSearch(e.target.value);
            }}
            placeholder="Search SKU, reference number, or reason..."
            className="rounded-md border border-slate-300 px-3 py-1.5 text-xs"
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
          <select
            value={typeFilter}
            onChange={(e) => {
              setPage(1);
              setTypeFilter(e.target.value);
            }}
            className="rounded-md border border-slate-300 px-3 py-1.5 text-xs"
          >
            <option value="">All Movement Types</option>
            <option value="PurchaseReceipt">PurchaseReceipt</option>
            <option value="SalesFulfillment">SalesFulfillment</option>
            <option value="TransferOut">TransferOut</option>
            <option value="TransferIn">TransferIn</option>
            <option value="TransferReversal">TransferReversal</option>
            <option value="AdjustmentIncrease">AdjustmentIncrease</option>
            <option value="AdjustmentDecrease">AdjustmentDecrease</option>
            <option value="ReconciliationAdjustment">
              ReconciliationAdjustment
            </option>
          </select>
        </div>
      </Card>

      <Card>
        {loading ? (
          <LoadingState label="Loading stock movement ledger..." />
        ) : !data || data.items.length === 0 ? (
          <EmptyState
            title="No ledger entries match your criteria"
            description="Every goods receipt, order fulfillment, transfer, and adjustment is recorded here."
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
                  <tr>
                    <th className="px-4 py-2.5">Occurred (UTC)</th>
                    <th className="px-4 py-2.5">Warehouse</th>
                    <th className="px-4 py-2.5">SKU / Product</th>
                    <th className="px-4 py-2.5">Movement Type</th>
                    <th className="px-4 py-2.5 text-right">Before</th>
                    <th className="px-4 py-2.5 text-right">Delta</th>
                    <th className="px-4 py-2.5 text-right">After</th>
                    <th className="px-4 py-2.5 text-right">Unit Cost</th>
                    <th className="px-4 py-2.5">Reference</th>
                    <th className="px-4 py-2.5">Reason</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {data.items.map((m) => (
                    <tr key={m.id} className="hover:bg-slate-50">
                      <td className="px-4 py-2.5 text-slate-500">
                        {formatDateTime(m.occurredAtUtc)}
                      </td>
                      <td className="px-4 py-2.5 font-mono font-medium">
                        {m.warehouseCode}
                      </td>
                      <td className="px-4 py-2.5">
                        <div className="font-medium text-slate-900">
                          {m.productSku}
                        </div>
                        <div className="text-slate-500">{m.productName}</div>
                      </td>
                      <td className="px-4 py-2.5">
                        <Badge
                          variant={m.quantityDelta > 0 ? "success" : "warning"}
                        >
                          {m.movementType}
                        </Badge>
                      </td>
                      <td className="px-4 py-2.5 text-right text-slate-600">
                        {m.balanceBefore}
                      </td>
                      <td
                        className={`px-4 py-2.5 text-right font-semibold ${
                          m.quantityDelta > 0
                            ? "text-emerald-700"
                            : "text-red-600"
                        }`}
                      >
                        {m.quantityDelta > 0
                          ? `+${m.quantityDelta}`
                          : m.quantityDelta}
                      </td>
                      <td className="px-4 py-2.5 text-right font-semibold text-slate-900">
                        {m.balanceAfter}
                      </td>
                      <td className="px-4 py-2.5 text-right">
                        {formatCurrency(m.unitCost)}
                      </td>
                      <td className="px-4 py-2.5 font-mono text-slate-800">
                        {m.referenceNumber}
                      </td>
                      <td className="px-4 py-2.5 text-slate-600">{m.reason}</td>
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
    </div>
  );
}
