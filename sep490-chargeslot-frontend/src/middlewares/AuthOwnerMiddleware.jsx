import { Navigate, Outlet } from "react-router-dom";

export default function AuthMiddleware() {
  const token = localStorage.getItem("accessToken");
  const role = localStorage.getItem("role");
  if (!token || role !== "Owner") {
    return <Navigate to="/login" />;
  }
  return <Outlet />;
}
