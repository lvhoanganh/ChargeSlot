import { Navigate, Outlet } from "react-router-dom";
import { useAuthStore } from "@/stores/authStore";

export default function AuthAdminMiddleware() {
  const { token, role } = useAuthStore();
  if (!token || role !== "Admin") {
    return <Navigate to="/login" replace />;
  }
  return <Outlet />;
}
