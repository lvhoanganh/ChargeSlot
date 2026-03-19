import OwnerNav from "@/components/OwnerNav";
import React from "react";
import { Outlet } from "react-router-dom";

export default function OwnerLayout() {
  return (
    <div>
      <OwnerNav />
      <Outlet />
    </div>
  );
}
