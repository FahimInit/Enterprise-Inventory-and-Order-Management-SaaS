import { AuthResponse, ProblemDetailsError } from "./types";

const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:8080/api/v1";

const STORAGE_KEY = "enterprise_inventory_session_v1";

export class ApiClientError extends Error {
  public readonly status: number;
  public readonly errorCode?: string;
  public readonly correlationId?: string;
  public readonly fieldErrors?: Record<string, string[]>;

  constructor(
    message: string,
    status: number,
    errorCode?: string,
    correlationId?: string,
    fieldErrors?: Record<string, string[]>
  ) {
    super(message);
    this.name = "ApiClientError";
    this.status = status;
    this.errorCode = errorCode;
    this.correlationId = correlationId;
    this.fieldErrors = fieldErrors;
  }
}

export function getStoredSession(): AuthResponse | null {
  if (typeof window === "undefined") {
    return null;
  }
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as AuthResponse) : null;
  } catch {
    return null;
  }
}

export function saveStoredSession(session: AuthResponse | null): void {
  if (typeof window === "undefined") {
    return;
  }
  if (!session) {
    window.localStorage.removeItem(STORAGE_KEY);
  } else {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  }
}

export function generateIdempotencyKey(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return `idem-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

interface RequestOptions extends RequestInit {
  idempotent?: boolean;
  idempotencyKey?: string;
  skipAuth?: boolean;
}

export async function apiRequest<T>(
  path: string,
  options: RequestOptions = {}
): Promise<T> {
  const session = getStoredSession();
  const headers = new Headers(options.headers || {});

  if (!headers.has("Content-Type") && options.body) {
    headers.set("Content-Type", "application/json");
  }

  if (!options.skipAuth && session?.accessToken) {
    headers.set("Authorization", `Bearer ${session.accessToken}`);
    if (session.activeTenantId) {
      headers.set("X-Tenant-Id", session.activeTenantId);
    }
  }

  if (options.idempotent) {
    headers.set(
      "Idempotency-Key",
      options.idempotencyKey || generateIdempotencyKey()
    );
  }

  const normalizedPath = path.startsWith("/") ? path : `/${path}`;
  let response = await fetch(`${API_BASE_URL}${normalizedPath}`, {
    ...options,
    headers
  });

  // Attempt token rotation once if access token expired (401)
  if (
    response.status === 401 &&
    !options.skipAuth &&
    session?.refreshToken &&
    !normalizedPath.startsWith("/auth/")
  ) {
    const refreshed = await tryRefreshSession(session.refreshToken);
    if (refreshed) {
      headers.set("Authorization", `Bearer ${refreshed.accessToken}`);
      if (refreshed.activeTenantId) {
        headers.set("X-Tenant-Id", refreshed.activeTenantId);
      }
      response = await fetch(`${API_BASE_URL}${normalizedPath}`, {
        ...options,
        headers
      });
    }
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  let parsed: unknown = null;
  if (text) {
    try {
      parsed = JSON.parse(text);
    } catch {
      parsed = null;
    }
  }

  if (!response.ok) {
    const problem = (parsed || {}) as ProblemDetailsError;
    let message =
      problem.detail ||
      problem.title ||
      `Request failed with HTTP status ${response.status}.`;

    if (problem.errors) {
      const firstFieldErrors = Object.values(problem.errors).flat();
      if (firstFieldErrors.length > 0) {
        message = firstFieldErrors.join(" ");
      }
    }

    throw new ApiClientError(
      message,
      response.status,
      problem.errorCode,
      problem.correlationId,
      problem.errors
    );
  }

  return parsed as T;
}

async function tryRefreshSession(
  refreshToken: string
): Promise<AuthResponse | null> {
  try {
    const res = await fetch(`${API_BASE_URL}/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken })
    });

    if (!res.ok) {
      saveStoredSession(null);
      return null;
    }

    const updated = (await res.json()) as AuthResponse;
    saveStoredSession(updated);
    return updated;
  } catch {
    return null;
  }
}

export function formatCurrency(amount: number, currency = "USD"): string {
  try {
    return new Intl.NumberFormat("en-US", {
      style: "currency",
      currency: currency || "USD",
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(amount || 0);
  } catch {
    return `${currency} ${(amount || 0).toFixed(2)}`;
  }
}

export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return "—";
  try {
    return new Intl.DateTimeFormat("en-US", {
      year: "numeric",
      month: "short",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit"
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}
