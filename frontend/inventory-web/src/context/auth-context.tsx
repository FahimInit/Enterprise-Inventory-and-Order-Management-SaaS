"use client";

import React, { createContext, useContext, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AuthResponse } from "@/lib/types";
import {
  AUTH_BROADCAST_CHANNEL,
  AuthBroadcastMessage,
  STORAGE_KEY,
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

    let channel: BroadcastChannel | null = null;
    if (typeof BroadcastChannel !== "undefined") {
      channel = new BroadcastChannel(AUTH_BROADCAST_CHANNEL);
      channel.onmessage = (event: MessageEvent<AuthBroadcastMessage>) => {
        const message = event.data;
        if (!message) return;

        if (message.type === "LOGOUT") {
          setSessionState(null);
          router.replace("/login");
        } else if (message.type === "SESSION_UPDATED") {
          setSessionState(message.session);
        }
      };
    }

    // Storage event fallback for browsers without BroadcastChannel support
    const handleStorageChange = (event: StorageEvent) => {
      if (event.key !== STORAGE_KEY) return;

      if (!event.newValue) {
        setSessionState(null);
        router.replace("/login");
      } else {
        try {
          setSessionState(JSON.parse(event.newValue) as AuthResponse);
        } catch {
          setSessionState(null);
          router.replace("/login");
        }
      }
    };

    window.addEventListener("storage", handleStorageChange);

    return () => {
      channel?.close();
      window.removeEventListener("storage", handleStorageChange);
    };
  }, [router]);

  const setSession = (next: AuthResponse | null) => {
    saveStoredSession(next, { broadcast: true });
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
