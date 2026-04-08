import OwnerNav from "@/components/OwnerNav";
import OwnerFooter from "@/components/OwnerFooter";
import React from "react";
import { Outlet } from "react-router-dom";
import OwnerKycGuard from "@/pages/owner/OwnerKycGuard";

export default function OwnerLayout() {
  return (
    <div>
      <OwnerNav />
      <OwnerKycGuard>
        <Outlet />
      </OwnerKycGuard>
      <OwnerFooter />
    </div>
  );
}
