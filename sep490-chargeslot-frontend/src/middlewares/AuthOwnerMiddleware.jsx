import { Navigate, Outlet } from "react-router-dom";
import { useAuthStore } from "@/stores/authStore";

export default function AuthOwnerMiddleware() {
  const { token, role } = useAuthStore();
  if (!token || role !== "Owner") {
    return <Navigate to="/login" replace />;
  }
  return <Outlet />;
}
