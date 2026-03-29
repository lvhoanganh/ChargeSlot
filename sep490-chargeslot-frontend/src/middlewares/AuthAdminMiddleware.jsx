import { Navigate, Outlet } from "react-router-dom";

export default function AuthAdminMiddleware() {
  const token = localStorage.getItem("accessToken");
  const role = localStorage.getItem("role");
  if (!token || role !== "Admin") {
    return <Navigate to="/login" />;
  }
  return <Outlet />;
}
