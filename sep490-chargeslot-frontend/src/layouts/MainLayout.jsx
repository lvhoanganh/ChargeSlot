import Footer from "@/components/Footer";
import Nav from "@/components/Nav";
import { Outlet, Navigate } from "react-router-dom";

export default function MainLayout() {
  const role = localStorage.getItem("role");

  if (role === "Owner") {
    return <Navigate to="/owner/dashboard" replace />;
  }
  
  if (role === "Admin") {
    return <Navigate to="/admin/dashboard" replace />;
  }

  return (
    <div>
      <Nav />
      <Outlet />
      <Footer />
    </div>
  );
}
