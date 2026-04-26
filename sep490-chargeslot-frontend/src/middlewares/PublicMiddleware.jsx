import { Navigate, Outlet } from "react-router-dom";

export default function PublicMiddleware() {
  // Check localStorage SYNC instead of Zustand store (which loads async)
  // This prevents flickering/infinite redirects on page refresh
  const token = localStorage.getItem("accessToken");
  const role = localStorage.getItem("role");
  
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
