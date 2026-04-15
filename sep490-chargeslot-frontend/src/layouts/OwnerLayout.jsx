import OwnerNav from "@/components/OwnerNav";
import OwnerFooter from "@/components/OwnerFooter";
import React from "react";
import { Outlet } from "react-router-dom";
import OwnerKycGuard from "@/pages/owner/OwnerKycGuard";

export default function OwnerLayout() {
  return (
    <div style={{ display: "flex", flexDirection: "column", minHeight: "100vh" }}>
      <OwnerNav />
      <main style={{ flex: 1 }}>
        <OwnerKycGuard>
          <Outlet />
        </OwnerKycGuard>
      </main>
      <OwnerFooter />
    </div>
  );
}
