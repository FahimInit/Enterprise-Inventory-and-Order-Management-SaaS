"use client";

import React, { useCallback, useEffect, useState } from "react";
import { apiRequest, formatDateTime } from "@/lib/api-client";
import { OrganizationMemberDto, OrganizationRoleDto } from "@/lib/types";
import { useAuth } from "@/context/auth-context";
import {
  Badge,
  Card,
  ConfirmDialog,
  ErrorBanner,
  LoadingState,
  ModalDialog,
  PageHeader
} from "@/components/ui/enterprise-ui";

export default function TeamMembersPage() {
  const { hasPermission } = useAuth();
  const canManage = hasPermission("members:manage");

  const [members, setMembers] = useState<OrganizationMemberDto[]>([]);
  const [roles, setRoles] = useState<OrganizationRoleDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [inviteOpen, setInviteOpen] = useState(false);
  const [email, setEmail] = useState("");
  const [fullName, setFullName] = useState("");
  const [roleId, setRoleId] = useState("");
  const [initialPassword, setInitialPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const [deactivatingMember, setDeactivatingMember] =
    useState<OrganizationMemberDto | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [memRes, roleRes] = await Promise.all([
        apiRequest<OrganizationMemberDto[]>("/organizations/members"),
        apiRequest<OrganizationRoleDto[]>("/organizations/roles")
      ]);
      setMembers(memRes);
      setRoles(roleRes);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to load team members."
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const openInviteModal = () => {
    setEmail("");
    setFullName("");
    setRoleId(roles[0]?.id || "");
    setInitialPassword("MemberPass123!");
    setInviteOpen(true);
  };

  const handleInvite = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await apiRequest("/organizations/members/invite", {
        method: "POST",
        body: JSON.stringify({
          email,
          fullName,
          roleId,
          initialPassword: initialPassword || null
        })
      });
      setInviteOpen(false);
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to add member.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleRoleChange = async (memberId: string, newRoleId: string) => {
    setError(null);
    try {
      await apiRequest(`/organizations/members/${memberId}/role`, {
        method: "PUT",
        body: JSON.stringify({ roleId: newRoleId })
      });
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to update member role."
      );
    }
  };

  const handleConfirmDeactivate = async () => {
    if (!deactivatingMember) return;
    setSubmitting(true);
    setError(null);
    try {
      await apiRequest(`/organizations/members/${deactivatingMember.id}`, {
        method: "DELETE"
      });
      setDeactivatingMember(null);
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to deactivate member."
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="Workspace Team Members"
        description="Manage employee memberships, role assignments, and workspace access within your organization."
        actions={
          canManage && (
            <button
              type="button"
              onClick={openInviteModal}
              className="rounded-md bg-slate-900 px-3.5 py-2 text-xs font-medium text-white hover:bg-slate-800"
            >
              + Add / Invite Member
            </button>
          )
        }
      />

      <ErrorBanner message={error} />

      <Card>
        {loading ? (
          <LoadingState label="Loading workspace members..." />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="border-b border-slate-200 bg-slate-50 text-slate-600">
                <tr>
                  <th className="px-4 py-2.5">Full Name</th>
                  <th className="px-4 py-2.5">Email</th>
                  <th className="px-4 py-2.5">Assigned Role</th>
                  <th className="px-4 py-2.5">Status</th>
                  <th className="px-4 py-2.5">Joined</th>
                  {canManage && (
                    <th className="px-4 py-2.5 text-right">Actions</th>
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {members.map((m) => (
                  <tr key={m.id} className="hover:bg-slate-50">
                    <td className="px-4 py-2.5 font-semibold text-slate-900">
                      {m.fullName}
                      {m.isOwner && (
                        <span className="ml-2 rounded bg-blue-50 px-1.5 py-0.5 text-[10px] font-medium text-blue-700">
                          Owner
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-2.5 text-slate-600">{m.email}</td>
                    <td className="px-4 py-2.5">
                      {canManage && !m.isOwner ? (
                        <select
                          value={m.roleId}
                          onChange={(e) =>
                            handleRoleChange(m.id, e.target.value)
                          }
                          className="rounded border border-slate-300 px-2 py-1 text-xs"
                        >
                          {roles.map((r) => (
                            <option key={r.id} value={r.id}>
                              {r.name}
                            </option>
                          ))}
                        </select>
                      ) : (
                        <span className="font-medium">{m.roleName}</span>
                      )}
                    </td>
                    <td className="px-4 py-2.5">
                      <Badge variant={m.isActive ? "success" : "neutral"}>
                        {m.isActive ? "Active" : "Inactive"}
                      </Badge>
                    </td>
                    <td className="px-4 py-2.5 text-slate-500">
                      {formatDateTime(m.joinedAtUtc)}
                    </td>
                    {canManage && (
                      <td className="px-4 py-2.5 text-right">
                        {m.isActive && !m.isOwner && (
                          <button
                            type="button"
                            onClick={() => setDeactivatingMember(m)}
                            className="font-medium text-red-600 hover:underline"
                          >
                            Deactivate
                          </button>
                        )}
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <ModalDialog
        open={inviteOpen}
        title="Add or Invite Workspace Member"
        onClose={() => setInviteOpen(false)}
      >
        <form onSubmit={handleInvite} className="space-y-3 text-xs">
          <div>
            <label className="block font-medium text-slate-700">
              Full Name *
            </label>
            <input
              type="text"
              required
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            />
          </div>

          <div>
            <label className="block font-medium text-slate-700">
              Work Email *
            </label>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            />
          </div>

          <div>
            <label className="block font-medium text-slate-700">
              Workspace Role *
            </label>
            <select
              required
              value={roleId}
              onChange={(e) => setRoleId(e.target.value)}
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            >
              {roles.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="block font-medium text-slate-700">
              Initial Password (for new user accounts)
            </label>
            <input
              type="text"
              value={initialPassword}
              onChange={(e) => setInitialPassword(e.target.value)}
              className="mt-1 w-full rounded border border-slate-300 px-2.5 py-1.5"
            />
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={() => setInviteOpen(false)}
              className="rounded border border-slate-300 px-3 py-1.5 text-slate-700"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="rounded bg-slate-900 px-3.5 py-1.5 font-medium text-white hover:bg-slate-800"
            >
              Add Member
            </button>
          </div>
        </form>
      </ModalDialog>

      <ConfirmDialog
        open={!!deactivatingMember}
        title={`Deactivate ${deactivatingMember?.fullName}?`}
        description="Deactivating this member immediately revokes their active workspace refresh tokens and blocks access to this organization."
        confirmLabel="Deactivate Member"
        isSubmitting={submitting}
        onConfirm={handleConfirmDeactivate}
        onCancel={() => setDeactivatingMember(null)}
      />
    </div>
  );
}
