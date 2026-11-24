"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import {
  apiRequest,
  formatCurrency,
  formatDateTime
} from "@/lib/api-client";
import {
  DashboardSummaryDto,
  InventoryValuationReportDto,
  OrderStatisticsReportDto
} from "@/lib/types";
import {
  Badge,
  Card,
  ErrorBanner,
  LoadingState,
  PageHeader
} from "@/components/ui/enterprise-ui";
import {
  AllocationStackedCards,
  InventoryHealthGauge,
  StockActivityBarChart,
  StockFlowLineChart,
  ValuationDonutChart
} from "@/components/dashboard/dashboard-charts";

export default function DashboardPage() {
  const [summary, setSummary] = useState<DashboardSummaryDto | null>(null);
  const [valuation, setValuation] =
    useState<InventoryValuationReportDto | null>(null);
  const [orderStats, setOrderStats] = useState<OrderStatisticsReportDto | null>(
    null
  );
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    setLoading(true);
    Promise.allSettled([
      apiRequest<DashboardSummaryDto>("/reports/dashboard-summary"),
      apiRequest<InventoryValuationReportDto>("/reports/inventory-valuation"),
      apiRequest<OrderStatisticsReportDto>("/reports/order-statistics")
    ])
      .then(([summaryRes, valuationRes, orderStatsRes]) => {
        if (!active) return;

        if (summaryRes.status === "fulfilled") {
          setSummary(summaryRes.value);
        } else {
          setError(
            summaryRes.reason instanceof Error
              ? summaryRes.reason.message
              : "Failed to load dashboard."
          );
        }

        if (valuationRes.status === "fulfilled") {
          setValuation(valuationRes.value);
        }

        if (orderStatsRes.status === "fulfilled") {
          setOrderStats(orderStatsRes.value);
        }
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
    return (
      <ErrorBanner message={error || "Unable to load dashboard summary."} />
    );
  }

  const currency = summary.baseCurrency || "USD";

  return (
    <div className="space-y-6">
      <PageHeader
        title="Executive Operations Dashboard"
        description={`Authoritative inventory and order metrics for ${summary.organizationName}`}
        actions={
          <div className="flex flex-wrap gap-2">
            <Link
              href="/sales-orders"
              className="rounded-md bg-slate-900 px-3 py-1.5 text-xs font-medium text-white hover:bg-slate-800 whitespace-nowrap"
            >
              New Sales Order
            </Link>
            <Link
              href="/purchase-orders"
              className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 whitespace-nowrap"
            >
              New Purchase Order
            </Link>
          </div>
        }
      />

      {summary.isDemoEnvironment && (
        <div className="rounded-md border border-amber-200 bg-amber-50 px-4 py-2.5 text-xs text-amber-900">
          <strong>Demo Environment Active:</strong> You are viewing real database
          records seeded by the backend Demo Seeder (
          <code className="rounded bg-amber-100/80 px-1 py-0.5 font-mono text-[11px]">
            Seed:EnableDemoData = true
          </code>
          ). All stock movements, reservations, and orders are processed by the
          live PostgreSQL backend engine.
        </div>
      )}

      {/* Top Row: 8 Executive KPI Cards + Inventory Health Gauge */}
      <div className="grid grid-cols-1 gap-4 xl:grid-cols-12">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4 xl:col-span-9">
          <Card className="p-4">
            <div className="text-xs font-medium text-slate-500">
              Total Inventory Valuation
            </div>
            <div className="mt-1.5 text-xl font-bold tabular-nums text-slate-900">
              {formatCurrency(summary.totalInventoryValuation, currency)}
            </div>
            <div className="mt-1 text-[11px] font-medium text-emerald-600">
              Across {summary.activeWarehouseCount} active warehouse(s)
            </div>
          </Card>

          <Card className="p-4">
            <div className="text-xs font-medium text-slate-500">
              Available Stock Balance
            </div>
            <div className="mt-1.5 text-xl font-bold tabular-nums text-slate-900">
              {summary.totalAvailableUnits.toLocaleString()}{" "}
              <span className="text-xs font-normal text-slate-500">
                / {summary.totalOnHandUnits.toLocaleString()} on-hand
              </span>
            </div>
            <div className="mt-1 text-[11px] text-slate-500 tabular-nums">
              Reserved: {summary.totalReservedUnits.toLocaleString()} | In-Transit:{" "}
              {summary.totalInTransitUnits.toLocaleString()}
            </div>
          </Card>

          <Card className="p-4">
            <div className="text-xs font-medium text-slate-500">
              Procurement Spend
            </div>
            <div className="mt-1.5 text-xl font-bold tabular-nums text-slate-900">
              {formatCurrency(
                orderStats?.totalProcurementSpend ??
                  summary.openPurchaseOrderValue,
                currency
              )}
            </div>
            <div className="mt-1 text-[11px] text-slate-500 tabular-nums">
              Open POs: <strong>{summary.openPurchaseOrderCount}</strong> (
              {formatCurrency(summary.openPurchaseOrderValue, currency)})
            </div>
          </Card>

          <Card className="p-4">
            <div className="text-xs font-medium text-slate-500">
              Sales Pipeline &amp; Revenue
            </div>
            <div className="mt-1.5 text-xl font-bold tabular-nums text-slate-900">
              {formatCurrency(
                orderStats?.totalConfirmedAndFulfilledRevenue ??
                  summary.fulfilledSalesRevenue,
                currency
              )}
            </div>
            <div className="mt-1 text-[11px] font-medium text-blue-600 tabular-nums">
              Open Sales Orders: {summary.openSalesOrderCount} | Fulfilled:{" "}
              {formatCurrency(summary.fulfilledSalesRevenue, currency)}
            </div>
          </Card>

          <Card className="p-4">
            <div className="text-xs font-medium text-slate-500">
              Low-Stock Alerts
            </div>
            <div className="mt-1.5 flex items-baseline gap-2">
              <span className="text-xl font-bold tabular-nums text-slate-900">
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
            <div className="mt-1 text-[11px] text-slate-500">
              Below configured reorder point
            </div>
          </Card>

          <Card className="p-4">
            <div className="text-xs font-medium text-slate-500">
              Active Catalog SKUs
            </div>
            <div className="mt-1.5 text-xl font-bold tabular-nums text-slate-900">
              {summary.activeProductCount.toLocaleString()}
            </div>
            <div className="mt-1 text-[11px] text-slate-500">
              Across {summary.activeCategoryCount} active categories
            </div>
          </Card>

          <Card className="p-4">
            <div className="text-xs font-medium text-slate-500">
              Active Suppliers
            </div>
            <div className="mt-1.5 text-xl font-bold tabular-nums text-slate-900">
              {summary.activeSupplierCount.toLocaleString()}
            </div>
            <div className="mt-1 text-[11px] text-slate-500">
              Approved procurement vendors
            </div>
          </Card>

          <Card className="p-4">
            <div className="text-xs font-medium text-slate-500">
              Pending Stock Transfers
            </div>
            <div className="mt-1.5 text-xl font-bold tabular-nums text-slate-900">
              {summary.pendingTransferCount.toLocaleString()}
            </div>
            <div className="mt-1 text-[11px] text-slate-500">
              In-Transit units: {summary.totalInTransitUnits.toLocaleString()}
            </div>
          </Card>
        </div>

        {/* Right Column: Inventory Health Semi-Circular Gauge */}
        <div className="xl:col-span-3">
          <InventoryHealthGauge summary={summary} />
        </div>
      </div>

      {/* Middle Row: Bar Chart + Donut Chart + Stacked Allocation Bars */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-12">
        <div className="lg:col-span-5">
          <StockActivityBarChart summary={summary} />
        </div>
        <div className="lg:col-span-4">
          <ValuationDonutChart valuation={valuation} currency={currency} />
        </div>
        <div className="lg:col-span-3">
          <AllocationStackedCards
            summary={summary}
            orderStats={orderStats}
            currency={currency}
          />
        </div>
      </div>

      {/* Bottom Analytics Row: Value Flow Line Chart + Low-Stock Alerts */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-12">
        <div className="lg:col-span-5">
          <StockFlowLineChart summary={summary} currency={currency} />
        </div>

        <div className="lg:col-span-7">
          <Card className="h-full">
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
                    <th className="px-4 py-2.5 text-right whitespace-nowrap">
                      Available
                    </th>
                    <th className="px-4 py-2.5 text-right whitespace-nowrap">
                      Reorder Pt
                    </th>
                    <th className="px-4 py-2.5 text-right whitespace-nowrap">
                      Suggested Order
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {summary.topLowStockItems.map((item) => (
                    <tr
                      key={item.inventoryItemId}
                      className="hover:bg-slate-50"
                    >
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
      </div>

      {/* Warehouse Stock & Valuation Breakdown */}
      <Card>
        <div className="border-b border-slate-200 px-4 py-3">
          <h2 className="text-sm font-semibold text-slate-900">
            Warehouse Stock &amp; Valuation Breakdown
          </h2>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
              <tr>
                <th className="px-4 py-2.5">Warehouse</th>
                <th className="px-4 py-2.5 text-right whitespace-nowrap">
                  SKUs
                </th>
                <th className="px-4 py-2.5 text-right whitespace-nowrap">
                  On-Hand
                </th>
                <th className="px-4 py-2.5 text-right whitespace-nowrap">
                  Reserved
                </th>
                <th className="px-4 py-2.5 text-right whitespace-nowrap">
                  Available
                </th>
                <th className="px-4 py-2.5 text-right whitespace-nowrap">
                  Valuation
                </th>
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

      {/* Recent Immutable Stock Movements */}
      <Card>
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
                <th className="px-4 py-2.5 text-right whitespace-nowrap">
                  Delta
                </th>
                <th className="px-4 py-2.5 text-right whitespace-nowrap">
                  Balance After
                </th>
                <th className="px-4 py-2.5">Reference &amp; Reason</th>
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
