import Nav from "@/components/Nav";
import React from "react";
import { Outlet } from "react-router-dom";

export default function OwnerLayout() {
  return (
    <div>
      <Nav />
      <Outlet />
    </div>
  );
}
