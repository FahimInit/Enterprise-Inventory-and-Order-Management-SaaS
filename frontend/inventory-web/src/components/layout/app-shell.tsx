"use client";

import React, { useEffect } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
  Activity,
  ArrowLeftRight,
  BarChart3,
  Boxes,
  Building2,
  ClipboardList,
  FolderTree,
  History,
  LayoutDashboard,
  LogOut,
  Package,
  PlusCircle,
  Settings,
  ShieldCheck,
  ShoppingCart,
  Truck,
  Users,
  Warehouse
} from "lucide-react";
import { useAuth } from "@/context/auth-context";

interface NavItem {
  href: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
}

const NAV_SECTIONS: { section: string; items: NavItem[] }[] = [
  {
    section: "Operations",
    items: [
      { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
      { href: "/inventory", label: "Inventory Overview", icon: Boxes },
      { href: "/inventory/movements", label: "Stock Movements", icon: History },
      { href: "/transfers", label: "Stock Transfers", icon: ArrowLeftRight }
    ]
  },
  {
    section: "Orders & Procurement",
    items: [
      { href: "/sales-orders", label: "Sales Orders", icon: ShoppingCart },
      { href: "/purchase-orders", label: "Purchase Orders", icon: ClipboardList },
      { href: "/suppliers", label: "Suppliers", icon: Truck }
    ]
  },
  {
    section: "Catalog & Facilities",
    items: [
      { href: "/products", label: "Products", icon: Package },
      { href: "/categories", label: "Categories", icon: FolderTree },
      { href: "/warehouses", label: "Warehouses", icon: Warehouse }
    ]
  },
  {
    section: "Intelligence & Governance",
    items: [
      { href: "/reports", label: "Reports & Analytics", icon: BarChart3 },
      { href: "/team", label: "Team Members", icon: Users },
      { href: "/roles", label: "Roles & Permissions", icon: ShieldCheck },
      { href: "/settings", label: "Organization Settings", icon: Settings },
      { href: "/audit-logs", label: "Activity Logs", icon: Activity }
    ]
  }
];

export function AppShell({ children }: { children: React.ReactNode }) {
  const { session, isLoading, switchOrganization, logout } = useAuth();
  const pathname = usePathname();
  const router = useRouter();

  const isPublicAuthRoute =
    pathname === "/login" || pathname === "/register" || pathname === "/";

  useEffect(() => {
    if (!isLoading && !session && !isPublicAuthRoute) {
      router.replace("/login");
    } else if (
      !isLoading &&
      session &&
      !session.activeTenantId &&
      pathname !== "/onboarding" &&
      !isPublicAuthRoute
    ) {
      router.replace("/onboarding");
    }
  }, [isLoading, session, pathname, isPublicAuthRoute, router]);

  if (isPublicAuthRoute) {
    return <>{children}</>;
  }

  if (isLoading || !session) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-50 text-sm text-slate-600">
        Verifying workspace session...
      </div>
    );
  }

  const isDemoWorkspace =
    session.activeOrganizationName?.toUpperCase().includes("DEMO") ?? false;

  return (
    <div className="flex min-h-screen bg-slate-50">
      {/* Sidebar */}
      <aside className="hidden w-64 shrink-0 flex-col border-r border-slate-200 bg-slate-900 text-slate-200 md:flex">
        <div className="flex h-14 items-center gap-2.5 border-b border-slate-800 px-4">
          <div className="flex h-7 w-7 items-center justify-center rounded bg-blue-600 text-xs font-bold text-white">
            EI
          </div>
          <div className="truncate">
            <div className="text-sm font-semibold text-white">
              Enterprise Inventory
            </div>
            <div className="text-[11px] text-slate-400">
              Multi-Tenant Order SaaS
            </div>
          </div>
        </div>

        {/* Workspace Switcher */}
        <div className="border-b border-slate-800 p-3">
          <label className="block text-[10px] font-semibold uppercase tracking-wider text-slate-400">
            Active Organization
          </label>
          <select
            aria-label="Active Organization Workspace"
            value={session.activeTenantId || ""}
            onChange={(e) => {
              if (e.target.value) {
                switchOrganization(e.target.value);
              }
            }}
            className="mt-1.5 w-full rounded border border-slate-700 bg-slate-800 px-2.5 py-1.5 text-xs font-medium text-white focus:border-blue-500 focus:outline-none"
          >
            {session.workspaces.map((ws) => (
              <option key={ws.organizationId} value={ws.organizationId}>
                {ws.organizationName} ({ws.roleName})
              </option>
            ))}
          </select>
          <Link
            href="/onboarding"
            className="mt-2 inline-flex items-center gap-1.5 text-xs text-slate-400 hover:text-white"
          >
            <PlusCircle className="h-3.5 w-3.5" />
            <span>New Organization Workspace</span>
          </Link>
        </div>

        {/* Navigation Links */}
        <nav className="flex-1 space-y-5 overflow-y-auto px-3 py-4">
          {NAV_SECTIONS.map((group) => (
            <div key={group.section}>
              <div className="mb-1.5 px-2 text-[10px] font-semibold uppercase tracking-wider text-slate-400">
                {group.section}
              </div>
              <div className="space-y-0.5">
                {group.items.map((item) => {
                  const Icon = item.icon;
                  const active = pathname === item.href;
                  return (
                    <Link
                      key={item.href}
                      href={item.href}
                      className={`flex items-center gap-2.5 rounded-md px-2.5 py-2 text-xs font-medium transition-colors ${
                        active
                          ? "bg-blue-600 text-white"
                          : "text-slate-300 hover:bg-slate-800 hover:text-white"
                      }`}
                    >
                      <Icon className="h-4 w-4 shrink-0" />
                      <span>{item.label}</span>
                    </Link>
                  );
                })}
              </div>
            </div>
          ))}
        </nav>

        {/* User Footer */}
        <div className="border-t border-slate-800 p-3">
          <div className="truncate text-xs font-medium text-white">
            {session.fullName}
          </div>
          <div className="truncate text-[11px] text-slate-400">
            {session.email}
          </div>
          <div className="mt-2 flex items-center justify-between">
            <span className="inline-flex items-center rounded bg-slate-800 px-2 py-0.5 text-[10px] font-medium text-slate-300">
              {session.activeRoleName || "No Role"}
            </span>
            <button
              type="button"
              onClick={logout}
              className="inline-flex items-center gap-1 rounded px-2 py-1 text-xs text-slate-400 hover:bg-slate-800 hover:text-white"
            >
              <LogOut className="h-3.5 w-3.5" />
              <span>Sign Out</span>
            </button>
          </div>
        </div>
      </aside>

      {/* Main Content Area */}
      <div className="flex flex-1 flex-col min-w-0">
        <header className="flex h-14 items-center justify-between border-b border-slate-200 bg-white px-6">
          <div className="flex items-center gap-3">
            <Building2 className="h-4 w-4 text-slate-500" />
            <span className="text-sm font-semibold text-slate-900">
              {session.activeOrganizationName || "Organization Onboarding"}
            </span>
            {isDemoWorkspace && (
              <span className="rounded border border-amber-300 bg-amber-50 px-2 py-0.5 text-[11px] font-semibold text-amber-800">
                DEMO ENVIRONMENT
              </span>
            )}
          </div>
          <div className="flex items-center gap-3 text-xs text-slate-600">
            <span>
              Permissions: <strong>{session.permissions.length}</strong>
            </span>
          </div>
        </header>

        <main className="flex-1 p-6">{children}</main>
      </div>
    </div>
  );
}
