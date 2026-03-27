import OwnerNav from "@/components/OwnerNav";
import OwnerFooter from "@/components/OwnerFooter";
import React from "react";
import { Outlet } from "react-router-dom";

export default function OwnerLayout() {
  return (
    <div>
      <OwnerNav />
      <Outlet />
      <OwnerFooter />
    </div>
  );
}
