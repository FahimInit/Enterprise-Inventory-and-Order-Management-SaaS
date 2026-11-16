"use client";

import React, { useCallback, useEffect, useState } from "react";
import { apiRequest, formatDateTime } from "@/lib/api-client";
import { AuditLogDto, PagedResponse } from "@/lib/types";
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

export default function AuditLogsPage() {
  const [data, setData] = useState<PagedResponse<AuditLogDto> | null>(null);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [entityType, setEntityType] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedLog, setSelectedLog] = useState<AuditLogDto | null>(null);

  const loadLogs = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams({
        pageNumber: String(page),
        pageSize: "20"
      });
      if (search.trim()) params.set("search", search.trim());
      if (entityType) params.set("entityType", entityType);

      const res = await apiRequest<PagedResponse<AuditLogDto>>(
        `/audit-logs?${params.toString()}`
      );
      setData(res);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to load audit logs."
      );
    } finally {
      setLoading(false);
    }
  }, [page, search, entityType]);

  useEffect(() => {
    loadLogs();
  }, [loadLogs]);

  return (
    <div>
      <PageHeader
        title="Immutable Audit & Activity Logs"
        description="Tamper-protected operational audit trail recording actor, tenant, entity, and before/after state snapshots."
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
            placeholder="Search summary, actor email, or action..."
            className="rounded-md border border-slate-300 px-3 py-1.5 text-xs sm:col-span-2"
          />
          <select
            value={entityType}
            onChange={(e) => {
              setPage(1);
              setEntityType(e.target.value);
            }}
            className="rounded-md border border-slate-300 px-3 py-1.5 text-xs"
          >
            <option value="">All Entity Types</option>
            <option value="Organization">Organization</option>
            <option value="OrganizationMember">OrganizationMember</option>
            <option value="OrganizationRole">OrganizationRole</option>
            <option value="Product">Product</option>
            <option value="Category">Category</option>
            <option value="Warehouse">Warehouse</option>
            <option value="InventoryItem">InventoryItem</option>
            <option value="PurchaseOrder">PurchaseOrder</option>
            <option value="SalesOrder">SalesOrder</option>
            <option value="StockTransfer">StockTransfer</option>
          </select>
        </div>
      </Card>

      <Card>
        {loading ? (
          <LoadingState label="Loading compliance audit trail..." />
        ) : !data || data.items.length === 0 ? (
          <EmptyState
            title="No audit events match your filter"
            description="Business mutations are automatically recorded here."
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
                  <tr>
                    <th className="px-4 py-2.5">Occurred (UTC)</th>
                    <th className="px-4 py-2.5">Actor</th>
                    <th className="px-4 py-2.5">Action</th>
                    <th className="px-4 py-2.5">Entity</th>
                    <th className="px-4 py-2.5">Summary</th>
                    <th className="px-4 py-2.5 text-right">Details</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {data.items.map((log) => (
                    <tr key={log.id} className="hover:bg-slate-50">
                      <td className="px-4 py-2.5 text-slate-500">
                        {formatDateTime(log.occurredAtUtc)}
                      </td>
                      <td className="px-4 py-2.5 font-medium text-slate-800">
                        {log.actorEmail || "System"}
                      </td>
                      <td className="px-4 py-2.5">
                        <Badge variant="info">{log.action}</Badge>
                      </td>
                      <td className="px-4 py-2.5 font-mono text-slate-600">
                        {log.entityType}
                      </td>
                      <td className="px-4 py-2.5 text-slate-800">
                        {log.summary}
                      </td>
                      <td className="px-4 py-2.5 text-right">
                        {(log.beforeStateJson || log.afterStateJson) && (
                          <button
                            type="button"
                            onClick={() => setSelectedLog(log)}
                            className="font-medium text-blue-600 hover:underline"
                          >
                            Inspect JSON
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
        open={!!selectedLog}
        title={selectedLog ? `Audit Event: ${selectedLog.action}` : "Audit Event"}
        onClose={() => setSelectedLog(null)}
      >
        {selectedLog && (
          <div className="space-y-3 text-xs">
            <div className="rounded bg-slate-50 p-2.5 text-slate-700">
              <div>
                <strong>Summary:</strong> {selectedLog.summary}
              </div>
              <div className="mt-1">
                <strong>Entity ID:</strong>{" "}
                <span className="font-mono">{selectedLog.entityId}</span>
              </div>
              <div className="mt-1">
                <strong>Correlation ID:</strong>{" "}
                <span className="font-mono">
                  {selectedLog.correlationId || "—"}
                </span>
              </div>
            </div>

            {selectedLog.beforeStateJson && (
              <div>
                <div className="font-semibold text-slate-700">
                  Before State Snapshot:
                </div>
                <pre className="mt-1 overflow-x-auto rounded bg-slate-900 p-2.5 text-[11px] text-slate-100">
                  {selectedLog.beforeStateJson}
                </pre>
              </div>
            )}

            {selectedLog.afterStateJson && (
              <div>
                <div className="font-semibold text-slate-700">
                  After State Snapshot:
                </div>
                <pre className="mt-1 overflow-x-auto rounded bg-slate-900 p-2.5 text-[11px] text-slate-100">
                  {selectedLog.afterStateJson}
                </pre>
              </div>
            )}
          </div>
        )}
      </ModalDialog>
    </div>
  );
}
