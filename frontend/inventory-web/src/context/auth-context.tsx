"use client";

import React, { createContext, useContext, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AuthResponse } from "@/lib/types";
import {
  apiRequest,
  getStoredSession,
  saveStoredSession
} from "@/lib/api-client";

interface AuthContextValue {
  session: AuthResponse | null;
  isLoading: boolean;
  setSession: (session: AuthResponse | null) => void;
  hasPermission: (permission: string) => boolean;
  switchOrganization: (organizationId: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [session, setSessionState] = useState<AuthResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const router = useRouter();

  useEffect(() => {
    const stored = getStoredSession();
    setSessionState(stored);
    setIsLoading(false);
  }, []);

  const setSession = (next: AuthResponse | null) => {
    saveStoredSession(next);
    setSessionState(next);
  };

  const hasPermission = (permission: string): boolean => {
    if (!session) return false;
    return session.permissions.some(
      (p) => p.toLowerCase() === permission.toLowerCase()
    );
  };

  const switchOrganization = async (organizationId: string) => {
    const updated = await apiRequest<AuthResponse>("/auth/switch-tenant", {
      method: "POST",
      body: JSON.stringify({ organizationId })
    });
    setSession(updated);
    router.refresh();
  };

  const logout = async () => {
    try {
      if (session?.refreshToken) {
        await apiRequest("/auth/logout", {
          method: "POST",
          keepalive: true,
          body: JSON.stringify({ refreshToken: session.refreshToken })
        });
      }
    } catch {
      // Ignore network errors during logout
    } finally {
      setSession(null);
      router.push("/login");
    }
  };

  return (
    <AuthContext.Provider
      value={{
        session,
        isLoading,
        setSession,
        hasPermission,
        switchOrganization,
        logout
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error("useAuth must be used within an AuthProvider.");
  }
  return ctx;
}
