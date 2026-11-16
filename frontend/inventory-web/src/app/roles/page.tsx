"use client";

import React, { useCallback, useEffect, useState } from "react";
import { apiRequest } from "@/lib/api-client";
import { OrganizationRoleDto } from "@/lib/types";
import { useAuth } from "@/context/auth-context";
import {
  Badge,
  Card,
  ErrorBanner,
  LoadingState,
  ModalDialog,
  PageHeader
} from "@/components/ui/enterprise-ui";

export default function RolesAndPermissionsPage() {
  const { hasPermission } = useAuth();
  const canManage = hasPermission("roles:manage");

  const [roles, setRoles] = useState<OrganizationRoleDto[]>([]);
  const [availablePermissions, setAvailablePermissions] = useState<string[]>(
    []
  );
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [modalOpen, setModalOpen] = useState(false);
  const [roleName, setRoleName] = useState("");
  const [roleDescription, setRoleDescription] = useState("");
  const [selectedPermissions, setSelectedPermissions] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);

  const loadRoles = useCallback(async () => {
    setLoading(true);
    try {
      const [rolesRes, permsRes] = await Promise.all([
        apiRequest<OrganizationRoleDto[]>("/organizations/roles"),
        apiRequest<string[]>("/organizations/permissions")
      ]);
      setRoles(rolesRes);
      setAvailablePermissions(permsRes);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load roles.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadRoles();
  }, [loadRoles]);

  const togglePermission = (perm: string) => {
    setSelectedPermissions((prev) =>
      prev.includes(perm) ? prev.filter((p) => p !== perm) : [...prev, perm]
    );
  };

  const handleCreateRole = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await apiRequest("/organizations/roles", {
        method: "POST",
        body: JSON.stringify({
          name: roleName,
          description: roleDescription || null,
          permissions: selectedPermissions
        })
      });
      setModalOpen(false);
      await loadRoles();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create role.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="Roles & Granular Permission Policies"
        description="Inspect built-in RBAC/PBAC roles and define custom permission sets for your organization."
        actions={
          canManage && (
            <button
              type="button"
              onClick={() => {
                setRoleName("");
                setRoleDescription("");
                setSelectedPermissions(["catalog:read", "inventory:read"]);
                setModalOpen(true);
              }}
              className="rounded-md bg-slate-900 px-3.5 py-2 text-xs font-medium text-white hover:bg-slate-800"
            >
              + Create Custom Role
            </button>
          )
        }
      />

      <ErrorBanner message={error} />

      {loading ? (
        <LoadingState label="Loading role permission definitions..." />
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          {roles.map((role) => (
            <Card key={role.id} className="p-4">
              <div className="flex items-start justify-between">
                <div>
                  <div className="flex items-center gap-2">
                    <h2 className="text-sm font-semibold text-slate-900">
                      {role.name}
                    </h2>
                    <Badge variant={role.isSystemRole ? "info" : "neutral"}>
                      {role.isSystemRole ? "System Role" : "Custom Role"}
                    </Badge>
                  </div>
                  {role.description && (
                    <p className="mt-1 text-xs text-slate-600">
                      {role.description}
                    </p>
                  )}
                </div>
                <span className="text-xs text-slate-500">
                  {role.assignedMemberCount} member(s)
                </span>
              </div>

              <div className="mt-3 flex flex-wrap gap-1.5 border-t border-slate-100 pt-3">
                {role.permissions.map((perm) => (
                  <span
                    key={perm}
                    className="rounded bg-slate-100 px-2 py-0.5 font-mono text-[11px] text-slate-700"
                  >
                    {perm}
                  </span>
                ))}
              </div>
            </Card>
          ))}
        </div>
      )}

      <ModalDialog
        open={modalOpen}
        title="Create Custom Organization Role"
        onClose={() => setModalOpen(false)}
      >
        <form onSubmit={handleCreateRole} className="space-y-3 text-xs">
          <div>
            <label className="block font-medium text-slate-700">
              Role Name *
            </label>
            <input
              type="text"
              required
              value={roleName}
              onChange={(e) => setRoleName(e.target.value)}
              placeholder="Regional Inventory Supervisor"
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            />
          </div>

          <div>
            <label className="block font-medium text-slate-700">
              Description
            </label>
            <input
              type="text"
              value={roleDescription}
              onChange={(e) => setRoleDescription(e.target.value)}
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            />
          </div>

          <div>
            <label className="block font-medium text-slate-700">
              Assigned Permissions ({selectedPermissions.length} selected) *
            </label>
            <div className="mt-1.5 grid max-h-56 grid-cols-2 gap-1.5 overflow-y-auto rounded border border-slate-200 p-2.5">
              {availablePermissions.map((perm) => (
                <label
                  key={perm}
                  className="inline-flex items-center gap-1.5 font-mono text-[11px] text-slate-700"
                >
                  <input
                    type="checkbox"
                    checked={selectedPermissions.includes(perm)}
                    onChange={() => togglePermission(perm)}
                  />
                  {perm}
                </label>
              ))}
            </div>
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={() => setModalOpen(false)}
              className="rounded border border-slate-300 px-3 py-1.5 text-slate-700"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting || selectedPermissions.length === 0}
              className="rounded bg-slate-900 px-3.5 py-1.5 font-medium text-white hover:bg-slate-800 disabled:opacity-50"
            >
              Create Role
            </button>
          </div>
        </form>
      </ModalDialog>
    </div>
  );
}
