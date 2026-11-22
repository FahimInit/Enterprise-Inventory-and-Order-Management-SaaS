"use client";

import React, { useEffect, useState } from "react";
import { apiRequest, formatCurrency } from "@/lib/api-client";
import {
  InventoryValuationReportDto,
  LowStockAlertItemDto,
  OrderStatisticsReportDto
} from "@/lib/types";
import {
  Badge,
  Card,
  ErrorBanner,
  LoadingState,
  PageHeader
} from "@/components/ui/enterprise-ui";

export default function ReportsPage() {
  const [valuation, setValuation] =
    useState<InventoryValuationReportDto | null>(null);
  const [lowStock, setLowStock] = useState<LowStockAlertItemDto[]>([]);
  const [orderStats, setOrderStats] =
    useState<OrderStatisticsReportDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    Promise.all([
      apiRequest<InventoryValuationReportDto>("/reports/inventory-valuation"),
      apiRequest<LowStockAlertItemDto[]>("/reports/low-stock"),
      apiRequest<OrderStatisticsReportDto>("/reports/order-statistics")
    ])
      .then(([valRes, lowRes, ordRes]) => {
        setValuation(valRes);
        setLowStock(lowRes);
        setOrderStats(ordRes);
      })
      .catch((err) => {
        setError(err instanceof Error ? err.message : "Failed to load reports.");
      })
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return <LoadingState label="Generating analytical reports..." />;
  }

  const currency = valuation?.baseCurrency || "USD";

  return (
    <div className="space-y-6">
      <PageHeader
        title="Enterprise Reports & Financial Analytics"
        description="Server-aggregated inventory valuation, replenishment alerts, procurement spend, and sales gross margin analysis."
      />

      <ErrorBanner message={error} />

      {/* Order & Financial Summary Cards */}
      {orderStats && valuation && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Card className="p-4">
            <div className="text-xs font-medium text-slate-500">
              Total Stock Valuation
            </div>
            <div className="mt-1 text-2xl font-semibold text-slate-900">
              {formatCurrency(valuation.totalInventoryValuation, currency)}
            </div>
            <div className="mt-1 text-xs text-slate-500">
              On-Hand: {valuation.totalOnHandUnits} | Available:{" "}
              {valuation.totalAvailableUnits}
            </div>
          </Card>

          <Card className="p-4">
            <div className="text-xs font-medium text-slate-500">
              Procurement Spend ({orderStats.totalPurchaseOrders}{" "}
              {orderStats.totalPurchaseOrders === 1 ? "PO" : "POs"})
            </div>
            <div className="mt-1 text-2xl font-semibold text-slate-900">
              {formatCurrency(orderStats.totalProcurementSpend, currency)}
            </div>
          </Card>

          <Card className="p-4">
            <div className="text-xs font-medium text-slate-500">
              Fulfilled Sales Revenue ({orderStats.totalSalesOrders}{" "}
              {orderStats.totalSalesOrders === 1 ? "SO" : "SOs"})
            </div>
            <div className="mt-1 text-2xl font-semibold text-slate-900">
              {formatCurrency(orderStats.totalFulfilledRevenue, currency)}
            </div>
            <div className="mt-1 text-xs text-slate-500">
              Committed + Fulfilled:{" "}
              {formatCurrency(
                orderStats.totalConfirmedAndFulfilledRevenue,
                currency
              )}
            </div>
          </Card>

          <Card className="p-4">
            <div className="text-xs font-medium text-slate-500">
              Estimated Fulfilled Gross Margin
            </div>
            <div className="mt-1 text-2xl font-semibold text-emerald-700">
              {formatCurrency(orderStats.estimatedGrossMargin, currency)}
            </div>
            <div className="mt-1 text-xs text-slate-500">
              Fulfilled COGS:{" "}
              {formatCurrency(orderStats.estimatedFulfilledCogs, currency)}
            </div>
          </Card>
        </div>
      )}

      {/* Valuation by Warehouse & Category */}
      {valuation && (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
          <Card>
            <div className="border-b border-slate-200 px-4 py-3">
              <h2 className="text-sm font-semibold text-slate-900">
                Valuation by Warehouse
              </h2>
            </div>
            <table className="w-full text-left text-xs">
              <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
                <tr>
                  <th className="px-4 py-2.5">Warehouse</th>
                  <th className="px-4 py-2.5 text-right">On-Hand</th>
                  <th className="px-4 py-2.5 text-right">Reserved</th>
                  <th className="px-4 py-2.5 text-right">Valuation</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {valuation.byWarehouse.map((w) => (
                  <tr key={w.warehouseId}>
                    <td className="px-4 py-2.5 font-medium">
                      {w.warehouseCode} — {w.warehouseName}
                    </td>
                    <td className="px-4 py-2.5 text-right">
                      {w.totalOnHandUnits}
                    </td>
                    <td className="px-4 py-2.5 text-right">
                      {w.totalReservedUnits}
                    </td>
                    <td className="px-4 py-2.5 text-right font-semibold">
                      {formatCurrency(w.totalValuation, currency)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Card>

          <Card>
            <div className="border-b border-slate-200 px-4 py-3">
              <h2 className="text-sm font-semibold text-slate-900">
                Valuation by Product Category
              </h2>
            </div>
            <table className="w-full text-left text-xs">
              <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
                <tr>
                  <th className="px-4 py-2.5">Category</th>
                  <th className="px-4 py-2.5 text-right">SKUs</th>
                  <th className="px-4 py-2.5 text-right">On-Hand Units</th>
                  <th className="px-4 py-2.5 text-right">Valuation</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {valuation.byCategory.map((c) => (
                  <tr key={c.categoryId}>
                    <td className="px-4 py-2.5 font-medium">
                      {c.categoryName}
                    </td>
                    <td className="px-4 py-2.5 text-right">{c.productCount}</td>
                    <td className="px-4 py-2.5 text-right">
                      {c.totalOnHandUnits}
                    </td>
                    <td className="px-4 py-2.5 text-right font-semibold">
                      {formatCurrency(c.totalValuation, currency)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Card>
        </div>
      )}

      {/* Low Stock Report */}
      <Card>
        <div className="border-b border-slate-200 px-4 py-3">
          <h2 className="text-sm font-semibold text-slate-900">
            Low-Stock Threshold & Replenishment Report
          </h2>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
              <tr>
                <th className="px-4 py-2.5">Warehouse</th>
                <th className="px-4 py-2.5">SKU / Product</th>
                <th className="px-4 py-2.5">Category</th>
                <th className="px-4 py-2.5 text-right">On-Hand</th>
                <th className="px-4 py-2.5 text-right">Reserved</th>
                <th className="px-4 py-2.5 text-right">Available</th>
                <th className="px-4 py-2.5 text-right">Reorder Point</th>
                <th className="px-4 py-2.5 text-right">Suggested Order</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200">
              {lowStock.map((item) => (
                <tr key={item.inventoryItemId}>
                  <td className="px-4 py-2.5 font-mono font-medium">
                    {item.warehouseCode}
                  </td>
                  <td className="px-4 py-2.5 font-medium text-slate-900">
                    {item.productSku} — {item.productName}
                  </td>
                  <td className="px-4 py-2.5">{item.categoryName}</td>
                  <td className="px-4 py-2.5 text-right">
                    {item.onHandQuantity}
                  </td>
                  <td className="px-4 py-2.5 text-right">
                    {item.reservedQuantity}
                  </td>
                  <td className="px-4 py-2.5 text-right font-semibold text-red-600">
                    {item.availableQuantity}
                  </td>
                  <td className="px-4 py-2.5 text-right">{item.reorderPoint}</td>
                  <td className="px-4 py-2.5 text-right">
                    <Badge variant="warning">
                      Order +{item.suggestedReorderQuantity}
                    </Badge>
                  </td>
                </tr>
              ))}
              {lowStock.length === 0 && (
                <tr>
                  <td
                    colSpan={8}
                    className="px-4 py-6 text-center text-slate-500"
                  >
                    No products are currently at or below their reorder point.
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
