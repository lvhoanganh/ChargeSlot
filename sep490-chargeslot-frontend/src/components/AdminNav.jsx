import { NavLink, useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";

export default function AdminNav() {
  const navigate = useNavigate();
  const navLinkClass = ({ isActive }) =>
    isActive
      ? "text-orange-500 font-bold"
      : "text-black hover:bg-green-500 hover:text-white px-3 py-2 rounded-md";

  return (
    <nav className="min-h-20 w-full bg-white border-b flex items-center fixed top-0 left-0 z-30">
      <div className="max-w-[95%] w-full mx-auto flex items-center justify-between">
        <NavLink
          to="/admin/manage-users"
          className="text-xl font-bold hover:text-pink-500"
        >
          CHARGE SLOT-ADMIN
        </NavLink>

        <div className="flex items-center gap-10">
          <NavLink to="/admin/manage-users" className={navLinkClass}>
            Quản lý người dùng 
          </NavLink>
          <NavLink to="/admin/view-financial-report" className={navLinkClass}>
            Báo cáo tài chính
          </NavLink>
          <NavLink to="/admin/approve-station" className={navLinkClass}>
            Duyệt trạm sạc
          </NavLink>
          <NavLink to="/admin/resolve-dispute" className={navLinkClass}>
            Giải quyết tranh chấp
          </NavLink>
        </div>

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
      </div>
    </nav>
  );
}
