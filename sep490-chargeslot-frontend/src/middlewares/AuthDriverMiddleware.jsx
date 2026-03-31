import { Navigate, Outlet } from "react-router-dom";
import { useAuthStore } from "@/stores/authStore";

export default function AuthDriverMiddleware() {
  const { token, role } = useAuthStore();
  if (!token || role !== "Driver") {
    return <Navigate to="/login" replace />;
  }
  return <Outlet />;
}
