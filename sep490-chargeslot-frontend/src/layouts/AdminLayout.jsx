import AdminFooter from "@/components/AdminFooter";
import AdminNav from "@/components/AdminNav";
import { Outlet } from "react-router-dom";

export default function AdminLayout() {
  return (
    <div>
      <AdminNav />
      <Outlet />
      <AdminFooter />
    </div>
  );
}
