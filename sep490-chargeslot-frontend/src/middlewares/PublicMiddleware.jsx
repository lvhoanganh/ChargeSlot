import { Navigate, Outlet } from "react-router-dom";
import { useAuthStore } from "@/stores/authStore";

export default function PublicMiddleware() {
  const { token, role } = useAuthStore();
  if (token) {
    if (role === "Admin") {
      return <Navigate to="/admin/manage-users" replace />;
    }
    if (role === "Owner") {
      return <Navigate to="/stations" replace />;
    }
    return <Navigate to="/" replace />;
  }
  return <Outlet />;
}
