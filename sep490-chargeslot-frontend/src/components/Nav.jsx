import { NavLink, useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { useAuthStore } from "@/stores/authStore";
export default function Nav() {
  const { logout } = useAuthStore();
  const token = localStorage.getItem("accessToken");
  const role = localStorage.getItem("role");
  const navigate = useNavigate();
  const navLinkClass = ({ isActive }) =>
    isActive
      ? "text-orange-500 font-bold"
      : "text-black hover:bg-green-500 hover:text-white px-3 py-2 rounded-md";
  return (
    <nav className="min-h-20 w-full bg-white border-b flex items-center fixed top-0 left-0 z-30">
      <div className="max-w-[95%] w-full mx-auto flex items-center justify-between">
        <NavLink to="/" className="text-xl font-bold hover:text-pink-500">
          CHARGE SLOT
        </NavLink>
        <div className="flex items-center gap-10">
          <NavLink to="/" className={navLinkClass}>
            Trang chủ
          </NavLink>
          <NavLink to="/service" className={navLinkClass}>
            Sản phẩm dịch vụ
          </NavLink>
          <NavLink to="/news" className={navLinkClass}>
            Tin tức
          </NavLink>
          <NavLink to="/about" className={navLinkClass}>
            Về ChargeSlot
          </NavLink>
        </div>

        {/* nếu chưa đăng nhập thì hiện 2 btn này */}
        {!token && (
          <div className="flex gap-2">
            <Button
              className="bg-blue-500 cursor-pointer hover:bg-green-500"
              onClick={() => navigate("/login")}
            >
              Đăng nhập
            </Button>
            <Button
              className="bg-blue-500 cursor-pointer hover:bg-green-500"
              onClick={() => navigate("/register")}
            >
              Đăng kí
            </Button>
          </div>
        )}

        {/* nếu đã đăng nhập và có role là Owner thì hiện 2 btn này */}
        {token && role === "Owner" && (
          <div className="flex gap-2">
            <Button
              className="bg-blue-500 cursor-pointer hover:bg-green-500"
              onClick={() => navigate("/stations")}
            >
              Quản lý trạm sạc
            </Button>
            <Button
              className="bg-blue-500 cursor-pointer hover:bg-green-500"
              onClick={() => {
                logout();
                navigate("/login");
              }}
            >
              Đăng xuất
            </Button>
          </div>
        )}

        {/* nếu đã đăng nhập và có role là Owner thì hiện 2 btn này */}
        {token && role === "Driver" && (
          <div className="flex gap-2">
            {/* <Button
              className="bg-blue-500 cursor-pointer hover:bg-green-500"
              onClick={() => navigate("/stations")}
            >
              Quản lý trạm sạc
            </Button> */}
            <Button
              className="bg-blue-500 cursor-pointer hover:bg-green-500"
              onClick={() => {
                logout();
                navigate("/login");
              }}
            >
              Đăng xuất
            </Button>
          </div>
        )}
      </div>
    </nav>
  );
}
