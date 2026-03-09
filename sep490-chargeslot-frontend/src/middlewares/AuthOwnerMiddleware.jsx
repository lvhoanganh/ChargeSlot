import { Navigate, Outlet } from "react-router-dom";

export default function AuthMiddleware() {
  //cơ bản chỉ để check nếu đã đăng nhập thì không cho phép quay lại trang login hoặc register nữa
  // nếu chưa đăng nhập mà vào trang owner thì sẽ bị đẩy về login
  const token = localStorage.getItem("accessToken");
  if (!token) {
    return <Navigate to="/login" />;
  }
  return <Outlet />;
}
