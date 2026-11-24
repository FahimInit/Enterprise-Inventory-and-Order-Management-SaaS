"use client";

import React from "react";
import Link from "next/link";
import {
  ResponsiveContainer,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  PieChart,
  Pie,
  Cell,
  LineChart,
  Line
} from "recharts";
import {
  DashboardSummaryDto,
  InventoryValuationReportDto,
  OrderStatisticsReportDto
} from "@/lib/types";
import { formatCurrency } from "@/lib/api-client";
import { Card } from "@/components/ui/enterprise-ui";

const DONUT_COLORS = [
  "#3b82f6", // blue
  "#8b5cf6", // purple
  "#ef4444", // red
  "#f97316", // orange
  "#22c55e", // emerald
  "#06b6d4"  // cyan
];

interface DarkTooltipProps {
  active?: boolean;
  payload?: Array<{
    name?: string;
    value?: number;
    color?: string;
    payload?: Record<string, unknown>;
  }>;
  label?: string;
  currency?: string;
  isCurrency?: boolean;
}

function DarkChartTooltip({
  active,
  payload,
  label,
  currency = "USD",
  isCurrency = false
}: DarkTooltipProps) {
  if (!active || !payload || payload.length === 0) return null;

  return (
    <div className="rounded-lg bg-slate-900 px-3 py-2 text-xs text-white shadow-lg border border-slate-700">
      {label && <div className="mb-1 font-semibold text-slate-200">{label}</div>}
      <div className="space-y-1">
        {payload.map((entry, idx) => (
          <div key={idx} className="flex items-center justify-between gap-3">
            <span className="flex items-center gap-1.5 text-slate-300">
              <span
                className="inline-block h-2 w-2 rounded-full"
                style={{ backgroundColor: entry.color || "#f97316" }}
              />
              {entry.name}:
            </span>
            <span className="font-semibold tabular-nums text-white">
              {isCurrency && typeof entry.value === "number"
                ? formatCurrency(entry.value, currency)
                : typeof entry.value === "number"
                ? entry.value.toLocaleString()
                : entry.value}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}

export function InventoryHealthGauge({
  summary
}: {
  summary: DashboardSummaryDto;
}) {
  // Compute an authoritative 0-100 operational health score from live stock metrics
  const totalProducts = Math.max(1, summary.activeProductCount);
  const healthyRatio = Math.max(
    0,
    (totalProducts - summary.lowStockAlertCount) / totalProducts
  );
  const availableRatio =
    summary.totalOnHandUnits > 0
      ? summary.totalAvailableUnits / summary.totalOnHandUnits
      : 1;

  // Weighted score: 65% reorder health + 35% stock availability headroom
  const rawScore = Math.round(healthyRatio * 65 + availableRatio * 35);
  const score = Math.max(0, Math.min(100, rawScore));

  const statusLabel =
    score >= 85
      ? "Excellent"
      : score >= 70
      ? "Good"
      : score >= 50
      ? "Fair"
      : "At Risk";

  const accentColor =
    score >= 70 ? "#10b981" : score >= 50 ? "#f59e0b" : "#ef4444";

  // Semi-circle arc math (radius 76, centered at 100, 96)
  const radius = 74;
  const circumference = Math.PI * radius;
  const strokeDashoffset = circumference * (1 - score / 100);

  // Needle angle: -90 deg (left, 0) to +90 deg (right, 100)
  const needleAngle = -90 + (score / 100) * 180;

  return (
    <Card className="flex h-full flex-col justify-between p-4">
      <div>
        <h3 className="text-sm font-semibold text-slate-900">
          Inventory Health
        </h3>
        <p className="text-[11px] text-slate-500">
          Live reorder &amp; availability index
        </p>
      </div>

      <div className="my-2 flex flex-col items-center justify-center">
        <svg viewBox="0 0 200 120" className="w-full max-w-[210px] overflow-visible">
          {/* Background track arc */}
          <path
            d="M 26 96 A 74 74 0 0 1 174 96"
            fill="none"
            stroke="#e2e8f0"
            strokeWidth="16"
            strokeLinecap="round"
          />
          {/* Active progress arc */}
          <path
            d="M 26 96 A 74 74 0 0 1 174 96"
            fill="none"
            stroke={accentColor}
            strokeWidth="16"
            strokeLinecap="round"
            strokeDasharray={circumference}
            strokeDashoffset={strokeDashoffset}
            className="transition-all duration-700 ease-out"
          />

          {/* Tick labels */}
          <text x="14" y="102" fontSize="9" fill="#94a3b8" textAnchor="end">
            0
          </text>
          <text x="40" y="38" fontSize="9" fill="#94a3b8" textAnchor="middle">
            25
          </text>
          <text x="100" y="14" fontSize="9" fill="#94a3b8" textAnchor="middle">
            50
          </text>
          <text x="160" y="38" fontSize="9" fill="#94a3b8" textAnchor="middle">
            75
          </text>
          <text x="186" y="102" fontSize="9" fill="#94a3b8" textAnchor="start">
            100
          </text>

          {/* Subtle pointer marker along outer rim */}
          <g transform={`rotate(${needleAngle}, 100, 96)`}>
            <rect
              x="98"
              y="14"
              width="4"
              height="18"
              rx="2"
              fill="#ffffff"
              stroke="#334155"
              strokeWidth="1.5"
            />
          </g>

          {/* Score text */}
          <text
            x="100"
            y="78"
            textAnchor="middle"
            className="fill-slate-900 text-2xl font-bold tabular-nums"
            style={{ fill: accentColor, fontSize: "26px", fontWeight: 700 }}
          >
            {score}
          </text>
          <text
            x="100"
            y="96"
            textAnchor="middle"
            style={{ fill: accentColor, fontSize: "12px", fontWeight: 600 }}
          >
            {statusLabel}
          </text>
        </svg>
      </div>

      <Link
        href="/reports"
        className="block w-full rounded-md bg-slate-100 px-3 py-2 text-center text-xs font-medium text-slate-700 transition hover:bg-slate-200 hover:text-slate-900"
      >
        More details
      </Link>
    </Card>
  );
}

export function StockActivityBarChart({
  summary
}: {
  summary: DashboardSummaryDto;
}) {
  // Build grouped bar dataset from live stock ledger movements (Delta In vs Balance After per SKU)
  const movementMap = new Map<
    string,
    { label: string; incoming: number; balance: number }
  >();

  for (const m of [...summary.recentStockMovements].reverse()) {
    const existing = movementMap.get(m.productSku) || {
      label: m.productSku,
      incoming: 0,
      balance: m.balanceAfter
    };
    if (m.quantityDelta > 0) {
      existing.incoming += m.quantityDelta;
    }
    existing.balance = m.balanceAfter;
    movementMap.set(m.productSku, existing);
  }

  const barData = Array.from(movementMap.values());

  return (
    <Card className="flex h-full flex-col p-4">
      <div className="mb-3 flex items-center justify-between">
        <div>
          <h3 className="text-sm font-semibold text-slate-900">
            Stock Received vs Current Balance
          </h3>
          <p className="text-[11px] text-slate-500">
            Recent ledger units by SKU
          </p>
        </div>
        <div className="flex items-center gap-3 text-[11px] text-slate-600">
          <span className="flex items-center gap-1.5">
            <span className="h-2.5 w-2.5 rounded-xs bg-[#1e3a5f]" />
            Received
          </span>
          <span className="flex items-center gap-1.5">
            <span className="h-2.5 w-2.5 rounded-xs bg-[#f97316]" />
            Balance
          </span>
        </div>
      </div>

      {barData.length === 0 ? (
        <div className="flex flex-1 items-center justify-center py-12 text-xs text-slate-400">
          No stock movement data available yet.
        </div>
      ) : (
        <div className="h-56 w-full">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart
              data={barData}
              margin={{ top: 8, right: 8, left: -16, bottom: 0 }}
              barGap={4}
            >
              <CartesianGrid
                strokeDasharray="3 3"
                vertical={false}
                stroke="#e2e8f0"
              />
              <XAxis
                dataKey="label"
                tick={{ fontSize: 10, fill: "#64748b" }}
                axisLine={{ stroke: "#e2e8f0" }}
                tickLine={false}
              />
              <YAxis
                tick={{ fontSize: 10, fill: "#64748b" }}
                axisLine={false}
                tickLine={false}
              />
              <Tooltip
                content={<DarkChartTooltip />}
                cursor={{ fill: "#f1f5f9" }}
              />
              <Bar
                dataKey="incoming"
                name="Received Units"
                fill="#1e3a5f"
                radius={[3, 3, 0, 0]}
                maxBarSize={24}
              />
              <Bar
                dataKey="balance"
                name="Balance After"
                fill="#f97316"
                radius={[3, 3, 0, 0]}
                maxBarSize={24}
              />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}
    </Card>
  );
}

export function ValuationDonutChart({
  valuation,
  currency
}: {
  valuation: InventoryValuationReportDto | null;
  currency: string;
}) {
  const categories = valuation?.byCategory ?? [];
  const chartData = categories.map((c, idx) => ({
    name: c.categoryName,
    value: c.totalValuation,
    units: c.totalOnHandUnits,
    color: DONUT_COLORS[idx % DONUT_COLORS.length]
  }));

  return (
    <Card className="flex h-full flex-col p-4">
      <div className="mb-2">
        <h3 className="text-sm font-semibold text-slate-900">
          Valuation by Category
        </h3>
        <p className="text-[11px] text-slate-500">
          Distribution of stocked inventory value
        </p>
      </div>

      {chartData.length === 0 ? (
        <div className="flex flex-1 items-center justify-center py-12 text-xs text-slate-400">
          No category valuation data yet.
        </div>
      ) : (
        <div className="flex flex-1 flex-col items-center gap-3 sm:flex-row">
          <div className="h-52 w-full sm:w-1/2">
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie
                  data={chartData}
                  dataKey="value"
                  nameKey="name"
                  cx="50%"
                  cy="50%"
                  innerRadius={44}
                  outerRadius={76}
                  paddingAngle={2}
                  strokeWidth={0}
                >
                  {chartData.map((entry, idx) => (
                    <Cell key={idx} fill={entry.color} />
                  ))}
                </Pie>
                <Tooltip
                  content={
                    <DarkChartTooltip currency={currency} isCurrency={true} />
                  }
                />
              </PieChart>
            </ResponsiveContainer>
          </div>

          <div className="w-full space-y-2 sm:w-1/2">
            {chartData.map((item) => (
              <div
                key={item.name}
                className="flex items-center justify-between gap-2 text-xs"
              >
                <div className="flex min-w-0 items-center gap-2">
                  <span
                    className="h-2.5 w-2.5 shrink-0 rounded-xs"
                    style={{ backgroundColor: item.color }}
                  />
                  <span className="truncate text-slate-700" title={item.name}>
                    {item.name}
                  </span>
                </div>
                <span className="shrink-0 font-semibold tabular-nums text-slate-900">
                  {formatCurrency(item.value, currency)}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}
    </Card>
  );
}

export function AllocationStackedCards({
  summary,
  orderStats,
  currency
}: {
  summary: DashboardSummaryDto;
  orderStats: OrderStatisticsReportDto | null;
  currency: string;
}) {
  const totalOnHand = Math.max(1, summary.totalOnHandUnits);
  const availPct = Math.round((summary.totalAvailableUnits / totalOnHand) * 100);
  const reservedPct = Math.max(0, 100 - availPct);

  const procurementSpend = orderStats?.totalProcurementSpend ?? 0;
  const salesValue = orderStats?.totalConfirmedAndFulfilledRevenue ?? summary.fulfilledSalesRevenue;
  const totalOrderVolume = Math.max(1, procurementSpend + salesValue);
  const procPct = Math.round((procurementSpend / totalOrderVolume) * 100);
  const salesPct = Math.max(0, 100 - procPct);

  return (
    <div className="flex h-full flex-col gap-4">
      {/* Stock Allocation Stacked Bar */}
      <Card className="flex-1 p-4">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold text-slate-900">
            Stock Allocation
          </h3>
          <span className="rounded bg-slate-900 px-2 py-0.5 text-[10px] font-semibold tabular-nums text-white">
            Reserved: {summary.totalReservedUnits.toLocaleString()}
          </span>
        </div>

        <div className="mt-3 flex h-4 w-full overflow-hidden rounded-sm bg-slate-100">
          <div
            className="bg-[#1e3a5f] transition-all duration-500"
            style={{ width: `${availPct}%` }}
            title={`Available: ${summary.totalAvailableUnits}`}
          />
          <div
            className="bg-[#f97316] transition-all duration-500"
            style={{ width: `${reservedPct}%` }}
            title={`Reserved: ${summary.totalReservedUnits}`}
          />
        </div>

        <div className="mt-2 flex items-center justify-center gap-4 text-[10px] text-slate-500">
          <span className="flex items-center gap-1">
            <span className="h-2 w-2 rounded-xs bg-[#1e3a5f]" /> Available ({availPct}%)
          </span>
          <span className="flex items-center gap-1">
            <span className="h-2 w-2 rounded-xs bg-[#f97316]" /> Reserved ({reservedPct}%)
          </span>
        </div>

        <div className="mt-3 grid grid-cols-2 gap-2 border-t border-slate-100 pt-2.5 text-xs">
          <div>
            <div className="text-[10px] text-slate-500">Available</div>
            <div className="font-semibold tabular-nums text-slate-900">
              {summary.totalAvailableUnits.toLocaleString()} units
            </div>
          </div>
          <div className="text-right">
            <div className="text-[10px] text-slate-500">In-Transit</div>
            <div className="font-semibold tabular-nums text-amber-600">
              {summary.totalInTransitUnits.toLocaleString()} units
            </div>
          </div>
        </div>
      </Card>

      {/* Procurement vs Sales Volume Stacked Bar */}
      <Card className="flex-1 p-4">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold text-slate-900">
            Order Volume Mix
          </h3>
          <span className="rounded bg-slate-100 px-2 py-0.5 text-[10px] font-semibold tabular-nums text-slate-700">
            {(orderStats?.totalPurchaseOrders ?? 0) +
              (orderStats?.totalSalesOrders ?? 0)}{" "}
            Orders
          </span>
        </div>

        <div className="mt-3 flex h-4 w-full overflow-hidden rounded-sm bg-slate-100">
          <div
            className="bg-[#1e3a5f] transition-all duration-500"
            style={{ width: `${procPct}%` }}
            title={`Procurement: ${formatCurrency(procurementSpend, currency)}`}
          />
          <div
            className="bg-[#f97316] transition-all duration-500"
            style={{ width: `${salesPct}%` }}
            title={`Sales: ${formatCurrency(salesValue, currency)}`}
          />
        </div>

        <div className="mt-2 flex items-center justify-center gap-4 text-[10px] text-slate-500">
          <span className="flex items-center gap-1">
            <span className="h-2 w-2 rounded-xs bg-[#1e3a5f]" /> Procurement
          </span>
          <span className="flex items-center gap-1">
            <span className="h-2 w-2 rounded-xs bg-[#f97316]" /> Sales
          </span>
        </div>

        <div className="mt-3 grid grid-cols-2 gap-2 border-t border-slate-100 pt-2.5 text-xs">
          <div>
            <div className="text-[10px] text-slate-500">Procurement</div>
            <div className="font-semibold tabular-nums text-slate-900">
              {formatCurrency(procurementSpend, currency)}
            </div>
          </div>
          <div className="text-right">
            <div className="text-[10px] text-slate-500">Sales Committed</div>
            <div className="font-semibold tabular-nums text-orange-600">
              {formatCurrency(salesValue, currency)}
            </div>
          </div>
        </div>
      </Card>
    </div>
  );
}

export function StockFlowLineChart({
  summary,
  currency
}: {
  summary: DashboardSummaryDto;
  currency: string;
}) {
  const ordered = [...summary.recentStockMovements].reverse();

  let incomingValue = 0;
  let outgoingValue = 0;

  const lineData = ordered.map((m, idx) => {
    const val = Math.abs(m.quantityDelta) * m.unitCost;
    if (m.quantityDelta > 0) incomingValue += val;
    else outgoingValue += val;

    return {
      step: `${m.productSku.slice(0, 7)} #${idx + 1}`,
      balanceValue: Number((m.balanceAfter * m.unitCost).toFixed(2)),
      balanceUnits: m.balanceAfter
    };
  });

  return (
    <Card className="flex h-full flex-col p-4">
      <div className="mb-3">
        <h3 className="text-sm font-semibold text-slate-900">
          Inventory Value Flow
        </h3>
        <div className="mt-2 grid grid-cols-2 gap-4 border-b border-slate-100 pb-3">
          <div>
            <div className="text-[11px] font-medium text-emerald-600">
              Incoming Stock Value
            </div>
            <div className="mt-0.5 text-lg font-bold tabular-nums text-slate-900">
              {formatCurrency(incomingValue, currency)}
            </div>
          </div>
          <div className="border-l border-slate-200 pl-4">
            <div className="text-[11px] font-medium text-orange-600">
              Outgoing Stock Value
            </div>
            <div className="mt-0.5 text-lg font-bold tabular-nums text-slate-900">
              {formatCurrency(outgoingValue, currency)}
            </div>
          </div>
        </div>
      </div>

      {lineData.length === 0 ? (
        <div className="flex flex-1 items-center justify-center py-10 text-xs text-slate-400">
          No ledger movement flow yet.
        </div>
      ) : (
        <div className="h-52 w-full">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart
              data={lineData}
              margin={{ top: 10, right: 12, left: -8, bottom: 0 }}
            >
              <CartesianGrid
                strokeDasharray="3 3"
                vertical={false}
                stroke="#e2e8f0"
              />
              <XAxis
                dataKey="step"
                tick={{ fontSize: 10, fill: "#64748b" }}
                axisLine={{ stroke: "#e2e8f0" }}
                tickLine={false}
              />
              <YAxis
                tick={{ fontSize: 10, fill: "#64748b" }}
                axisLine={false}
                tickLine={false}
                tickFormatter={(v) =>
                  v >= 1000 ? `${(v / 1000).toFixed(0)}k` : `${v}`
                }
              />
              <Tooltip
                content={
                  <DarkChartTooltip currency={currency} isCurrency={true} />
                }
              />
              <Line
                type="monotone"
                dataKey="balanceValue"
                name="Ledger Value"
                stroke="#1e293b"
                strokeWidth={2}
                dot={{ r: 4, fill: "#f97316", stroke: "#ffffff", strokeWidth: 1.5 }}
                activeDot={{ r: 6, fill: "#f97316", stroke: "#1e293b", strokeWidth: 2 }}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}
    </Card>
  );
}
