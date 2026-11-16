import type { Metadata } from "next";
import "./globals.css";
import { AuthProvider } from "@/context/auth-context";
import { AppShell } from "@/components/layout/app-shell";

export const metadata: Metadata = {
  title: "Enterprise Inventory & Order Management SaaS",
  description:
    "Multi-tenant enterprise inventory, warehouse, procurement, and sales order processing platform."
};

export default function RootLayout({
  children
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body>
        <AuthProvider>
          <AppShell>{children}</AppShell>
        </AuthProvider>
      </body>
    </html>
  );
}
