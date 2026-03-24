import { Navigate, Outlet } from "react-router-dom";

export default function AuthDriverMiddleware() {
  const token = localStorage.getItem("accessToken");
  const role = localStorage.getItem("role");
  if (!token || role !== "Driver") {
    return <Navigate to="/login" />;
  }
  return <Outlet />;
}
