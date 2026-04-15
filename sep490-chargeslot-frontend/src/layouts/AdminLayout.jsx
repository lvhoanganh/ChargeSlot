import AdminFooter from "@/components/AdminFooter";
import AdminNav from "@/components/AdminNav";
import { Outlet } from "react-router-dom";

export default function AdminLayout() {
  return (
    <div style={{ display: "flex", flexDirection: "column", minHeight: "100vh" }}>
      <AdminNav />
      <main style={{ flex: 1 }}>
        <Outlet />
      </main>
      <AdminFooter />
    </div>
  );
}
