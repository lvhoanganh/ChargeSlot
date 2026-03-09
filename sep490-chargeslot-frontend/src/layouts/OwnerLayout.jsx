import Nav from "@/components/Nav";
import OwnerNav from "@/components/OwnerNav";
import OwnerSideBar from "@/components/OwnerSideBar";
import React from "react";
import { Outlet } from "react-router-dom";

export default function OwnerLayout() {
  return (
    <div>
      <Nav />
      <OwnerSideBar />
      <div className="pt-20 ml-[20%]">
        <Outlet />
      </div>
    </div>
  );
}
