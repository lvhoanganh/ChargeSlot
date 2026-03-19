import { Navigate, Outlet } from "react-router-dom";
export default function PublicMiddleware() {
  const token = localStorage.getItem("accessToken");
  if (token) {
    const role = localStorage.getItem("role");
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
