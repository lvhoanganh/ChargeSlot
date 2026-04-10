import { Navigate, Outlet } from "react-router-dom";

/** Check if token is expired */
function isTokenExpired() {
  const expiresAt = localStorage.getItem("expiresAtUtc");
  if (!expiresAt) return false; // No expiry info, assume valid
  
  try {
    const expiryDate = new Date(expiresAt);
    const now = new Date();
    return now > expiryDate; // Token expired if now > expiry time
  } catch {
    return false; // Invalid format, assume valid
  }
}

export default function AuthDriverMiddleware() {
  // Check localStorage SYNC instead of Zustand store (which loads async)
  // This prevents flickering/infinite redirects on page refresh
  const token = localStorage.getItem("accessToken");
  const role = localStorage.getItem("role");
  
  // Token invalid if:
  // 1. No token exists
  // 2. Role is not Driver
  // 3. Token is expired
  if (!token || role !== "Driver" || isTokenExpired()) {
    return <Navigate to="/login" replace />;
  }
  return <Outlet />;
}
