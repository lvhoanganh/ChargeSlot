import { NavLink } from "react-router-dom";

export default function OwnerSideBar() {
  return (
    <div className="fixed left-0 top-16 h-[calc(100vh-4rem)] w-[20%] overflow-y-auto border-r-2 bg-white">
      <div className="space-y-1 p-4">
        <NavLink
          to="/stations"
          end
          className={({ isActive }) =>
            `block rounded-xl px-4 py-3 text-sm font-medium transition ${
              isActive
                ? "bg-orange-500 text-white"
                : "text-slate-700 hover:bg-slate-100"
            }`
          }
        >
          Trạm sạc của tôi
        </NavLink>
        <NavLink
          to="/stations/add"
          className={({ isActive }) =>
            `block rounded-xl px-4 py-3 text-sm font-medium transition ${
              isActive
                ? "bg-orange-500 text-white"
                : "text-slate-700 hover:bg-slate-100"
            }`
          }
        >
          Tạo trạm sạc mới
        </NavLink>
        <NavLink
          to="/owner/booking-requests"
          className={({ isActive }) =>
            `block rounded-xl px-4 py-3 text-sm font-medium transition ${
              isActive
                ? "bg-orange-500 text-white"
                : "text-slate-700 hover:bg-slate-100"
            }`
          }
        >
          Yêu cầu Booking
        </NavLink>
      </div>
    </div>
  );
}
