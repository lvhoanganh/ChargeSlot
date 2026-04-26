import { Navigate, Outlet } from "react-router-dom";

export default function AuthOwnerMiddleware() {
  // Check localStorage SYNC instead of Zustand store (which loads async)
  // This prevents flickering/infinite redirects on page refresh
  const token = localStorage.getItem("accessToken");
  const role = localStorage.getItem("role");
  
  // Token invalid if:
  // 1. No token exists
  // 2. Role is not Owner
  if (!token || role !== "Owner") {
    return <Navigate to="/login" replace />;
  }
  return <Outlet />;
}
