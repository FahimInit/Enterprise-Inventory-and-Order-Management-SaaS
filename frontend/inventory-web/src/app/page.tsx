"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/context/auth-context";

export default function HomePage() {
  const { session, isLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading) {
      if (session) {
        router.replace(session.activeTenantId ? "/dashboard" : "/onboarding");
      } else {
        router.replace("/login");
      }
    }
  }, [session, isLoading, router]);

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 text-sm text-slate-600">
      Loading Enterprise Inventory & Order Management Platform...
    </div>
  );
}
