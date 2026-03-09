import { NavLink } from "react-router-dom";

export default function OwnerSideBar() {
  return (
    <div className="w-[20%] h-[calc(100vh-5rem)] border-r-2 fixed top-20 left-0 overflow-y-auto">
      <NavLink to="/stations" className="block px-4 py-2 hover:bg-gray-700">
        Trạm sạc của tôi
      </NavLink>
    </div>
  );
}
