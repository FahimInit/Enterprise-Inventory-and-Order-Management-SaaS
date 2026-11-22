"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import {
  apiRequest,
  formatCurrency,
  formatDateTime
} from "@/lib/api-client";
import { DashboardSummaryDto } from "@/lib/types";
import {
  Badge,
  Card,
  ErrorBanner,
  LoadingState,
  PageHeader
} from "@/components/ui/enterprise-ui";

export default function DashboardPage() {
  const [summary, setSummary] = useState<DashboardSummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    setLoading(true);
    apiRequest<DashboardSummaryDto>("/reports/dashboard-summary")
      .then((data) => {
        if (active) setSummary(data);
      })
      .catch((err) => {
        if (active)
          setError(
            err instanceof Error ? err.message : "Failed to load dashboard."
          );
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, []);

  if (loading) {
    return <LoadingState label="Loading real-time operational metrics..." />;
  }

  if (error || !summary) {
    return <ErrorBanner message={error || "Unable to load dashboard summary."} />;
  }

  const currency = summary.baseCurrency || "USD";

  return (
    <div>
      <PageHeader
        title="Executive Operations Dashboard"
        description={`Authoritative inventory and order metrics for ${summary.organizationName}`}
        actions={
          <div className="flex gap-2">
            <Link
              href="/sales-orders"
              className="rounded-md bg-slate-900 px-3 py-1.5 text-xs font-medium text-white hover:bg-slate-800"
            >
              New Sales Order
            </Link>
            <Link
              href="/purchase-orders"
              className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
            >
              New Purchase Order
            </Link>
          </div>
        }
      />

      {summary.isDemoEnvironment && (
        <div className="mb-6 rounded-md border border-amber-200 bg-amber-50 px-4 py-2.5 text-xs text-amber-900">
          <strong>Demo Environment Active:</strong> You are viewing real database
          records seeded by the backend Demo Seeder (
          <code className="rounded bg-amber-100/80 px-1 py-0.5 font-mono text-[11px]">
            Seed:EnableDemoData = true
          </code>
          ). All stock movements, reservations, and orders are processed by the live
          PostgreSQL backend engine.
        </div>
      )}

      {/* Primary KPI Grid */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Card className="p-4">
          <div className="text-xs font-medium text-slate-500">
            Total Inventory Valuation
          </div>
          <div className="mt-1.5 text-2xl font-semibold text-slate-900">
            {formatCurrency(summary.totalInventoryValuation, currency)}
          </div>
          <div className="mt-1 text-xs text-slate-500">
            Across {summary.activeWarehouseCount} active warehouse(s)
          </div>
        </Card>

        <Card className="p-4">
          <div className="text-xs font-medium text-slate-500">
            Stock Balance (On-Hand / Available)
          </div>
          <div className="mt-1.5 text-2xl font-semibold text-slate-900">
            {summary.totalOnHandUnits.toLocaleString()}{" "}
            <span className="text-sm font-normal text-slate-500">
              / {summary.totalAvailableUnits.toLocaleString()} avail
            </span>
          </div>
          <div className="mt-1 text-xs text-slate-500">
            Reserved: {summary.totalReservedUnits.toLocaleString()} | In-Transit:{" "}
            {summary.totalInTransitUnits.toLocaleString()}
          </div>
        </Card>

        <Card className="p-4">
          <div className="text-xs font-medium text-slate-500">
            Open Sales & Fulfilled Revenue
          </div>
          <div className="mt-1.5 text-2xl font-semibold text-slate-900">
            {formatCurrency(summary.fulfilledSalesRevenue, currency)}
          </div>
          <div className="mt-1 text-xs text-slate-500">
            Open Sales Orders: <strong>{summary.openSalesOrderCount}</strong>
          </div>
        </Card>

        <Card className="p-4">
          <div className="text-xs font-medium text-slate-500">
            Low-Stock Alerts & Open POs
          </div>
          <div className="mt-1.5 flex items-baseline gap-2">
            <span className="text-2xl font-semibold text-slate-900">
              {summary.lowStockAlertCount}
            </span>
            <Badge
              variant={summary.lowStockAlertCount > 0 ? "warning" : "success"}
            >
              {summary.lowStockAlertCount > 0
                ? "Needs Replenishment"
                : "Healthy"}
            </Badge>
          </div>
          <div className="mt-1 text-xs text-slate-500">
            Open POs: {summary.openPurchaseOrderCount} (
            {formatCurrency(summary.openPurchaseOrderValue, currency)})
          </div>
        </Card>
      </div>

      {/* Warehouse Breakdown & Low-Stock Alerts */}
      <div className="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-2">
        <Card>
          <div className="border-b border-slate-200 px-4 py-3">
            <h2 className="text-sm font-semibold text-slate-900">
              Warehouse Stock & Valuation Breakdown
            </h2>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
                <tr>
                  <th className="px-4 py-2.5">Warehouse</th>
                  <th className="px-4 py-2.5 text-right whitespace-nowrap">SKUs</th>
                  <th className="px-4 py-2.5 text-right whitespace-nowrap">On-Hand</th>
                  <th className="px-4 py-2.5 text-right whitespace-nowrap">Reserved</th>
                  <th className="px-4 py-2.5 text-right whitespace-nowrap">Available</th>
                  <th className="px-4 py-2.5 text-right whitespace-nowrap">Valuation</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {summary.warehouseSummaries.map((wh) => (
                  <tr key={wh.warehouseId} className="hover:bg-slate-50">
                    <td className="px-4 py-2.5 font-medium text-slate-900">
                      {wh.warehouseCode} — {wh.warehouseName}
                    </td>
                    <td className="px-4 py-2.5 text-right tabular-nums">
                      {wh.distinctProductsCount}
                    </td>
                    <td className="px-4 py-2.5 text-right tabular-nums">
                      {wh.totalOnHandUnits}
                    </td>
                    <td className="px-4 py-2.5 text-right tabular-nums text-amber-700">
                      {wh.totalReservedUnits}
                    </td>
                    <td className="px-4 py-2.5 text-right tabular-nums font-semibold text-emerald-700">
                      {wh.totalAvailableUnits}
                    </td>
                    <td className="px-4 py-2.5 text-right tabular-nums font-medium">
                      {formatCurrency(wh.totalValuation, currency)}
                    </td>
                  </tr>
                ))}
                {summary.warehouseSummaries.length === 0 && (
                  <tr>
                    <td
                      colSpan={6}
                      className="px-4 py-6 text-center text-slate-500"
                    >
                      No warehouse inventory balances recorded yet.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </Card>

        <Card>
          <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3">
            <h2 className="text-sm font-semibold text-slate-900">
              Low-Stock Replenishment Alerts
            </h2>
            <Link
              href="/reports"
              className="text-xs font-medium text-blue-600 hover:underline"
            >
              View Full Report
            </Link>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
                <tr>
                  <th className="px-4 py-2.5">SKU / Product</th>
                  <th className="px-4 py-2.5">Warehouse</th>
                  <th className="px-4 py-2.5 text-right whitespace-nowrap">Available</th>
                  <th className="px-4 py-2.5 text-right whitespace-nowrap">Reorder Pt</th>
                  <th className="px-4 py-2.5 text-right whitespace-nowrap">Suggested Order</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {summary.topLowStockItems.map((item) => (
                  <tr key={item.inventoryItemId} className="hover:bg-slate-50">
                    <td className="px-4 py-2.5">
                      <div className="font-medium text-slate-900">
                        {item.productSku}
                      </div>
                      <div className="text-slate-500">{item.productName}</div>
                    </td>
                    <td className="px-4 py-2.5">{item.warehouseCode}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums font-semibold text-red-600">
                      {item.availableQuantity}
                    </td>
                    <td className="px-4 py-2.5 text-right tabular-nums">
                      {item.reorderPoint}
                    </td>
                    <td className="px-4 py-2.5 text-right tabular-nums font-medium text-blue-700">
                      +{item.suggestedReorderQuantity}
                    </td>
                  </tr>
                ))}
                {summary.topLowStockItems.length === 0 && (
                  <tr>
                    <td
                      colSpan={5}
                      className="px-4 py-6 text-center text-slate-500"
                    >
                      All stocked products are above their reorder points.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </Card>
      </div>

      {/* Recent Immutable Stock Movements */}
      <Card className="mt-6">
        <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3">
          <h2 className="text-sm font-semibold text-slate-900">
            Recent Immutable Stock Ledger Movements
          </h2>
          <Link
            href="/inventory/movements"
            className="text-xs font-medium text-blue-600 hover:underline"
          >
            View Complete Ledger
          </Link>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
              <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
                <tr>
                  <th className="px-4 py-2.5">Timestamp (UTC)</th>
                  <th className="px-4 py-2.5">Warehouse</th>
                  <th className="px-4 py-2.5">Product</th>
                  <th className="px-4 py-2.5">Type</th>
                  <th className="px-4 py-2.5 text-right whitespace-nowrap">Delta</th>
                  <th className="px-4 py-2.5 text-right whitespace-nowrap">Balance After</th>
                  <th className="px-4 py-2.5">Reference & Reason</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {summary.recentStockMovements.map((m) => (
                  <tr key={m.id} className="hover:bg-slate-50">
                    <td className="px-4 py-2.5 text-slate-500">
                      {formatDateTime(m.occurredAtUtc)}
                    </td>
                    <td className="px-4 py-2.5 font-medium">
                      {m.warehouseCode}
                    </td>
                    <td className="px-4 py-2.5">
                      <span className="font-medium text-slate-900">
                        {m.productSku}
                      </span>{" "}
                      — {m.productName}
                    </td>
                    <td className="px-4 py-2.5">
                      <Badge
                        variant={m.quantityDelta > 0 ? "success" : "warning"}
                      >
                        {m.movementType}
                      </Badge>
                    </td>
                    <td
                      className={`px-4 py-2.5 text-right tabular-nums font-semibold ${
                        m.quantityDelta > 0 ? "text-emerald-700" : "text-red-600"
                      }`}
                    >
                      {m.quantityDelta > 0
                        ? `+${m.quantityDelta}`
                        : m.quantityDelta}
                    </td>
                    <td className="px-4 py-2.5 text-right tabular-nums font-medium">
                      {m.balanceAfter}
                    </td>
                  <td className="px-4 py-2.5 text-slate-600">
                    <span className="font-medium text-slate-800">
                      [{m.referenceNumber}]
                    </span>{" "}
                    {m.reason}
                  </td>
                </tr>
              ))}
              {summary.recentStockMovements.length === 0 && (
                <tr>
                  <td
                    colSpan={7}
                    className="px-4 py-6 text-center text-slate-500"
                  >
                    No stock movements recorded yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  );
}
