import { NavLink, useNavigate, useLocation } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { useAuthStore } from "@/stores/authStore";
import { useState, useRef, useEffect } from "react";
import NotificationBell from "@/components/NotificationBell";
import { walletApi } from "@/services/api";


const DEFAULT_AVATAR =
  "https://avatarngau.sbs/wp-content/uploads/2025/07/avatar-vo-danh-va-sach.jpg";

function getStoredAvatarDataUrl(phoneNumber) {
  if (!phoneNumber) return "";
  try {
    const map = JSON.parse(localStorage.getItem("userInfoByPhone") || "{}");
    const normalized = normalizePhoneForKey(phoneNumber);
    return map?.[normalized]?.avatarDataUrl || map?.[phoneNumber]?.avatarDataUrl || "";
  } catch {
    return "";
  }
}

function normalizePhoneForKey(rawPhone) {
  const phone = String(rawPhone || "").trim().replaceAll(" ", "");
  if (!phone) return "";
  if (phone.startsWith("+84")) return `0${phone.slice(3)}`;
  return phone;
}

const maskPhone = (phone) =>
  phone ? `**** **** ${phone.slice(-2)}` : "";

const roleLabels = {
  driver: "Tài xế",
  owner: "Chủ trạm",
  admin: "Quản trị viên",
};

export default function OwnerNav() {
  const { logout, phoneNumber, role, token } = useAuthStore();
  const navigate = useNavigate();
  const location = useLocation();
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const dropdownRef = useRef(null);
  const [walletBalance, setWalletBalance] = useState(null);

  const normalizedRole = (role || "").toLowerCase();
  const avatarSrc = getStoredAvatarDataUrl(phoneNumber) || DEFAULT_AVATAR;
  const profilePath = "/owner/owner-profile";

  useEffect(() => {
    function handleClickOutside(e) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
        setDropdownOpen(false);
      }
    }
    if (dropdownOpen) {
      document.addEventListener("mousedown", handleClickOutside);
    }
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [dropdownOpen]);

  // Fetch wallet balance
  useEffect(() => {
    if (token) {
      walletApi.getWallet()
        .then(w => setWalletBalance(w?.availableBalance ?? null))
        .catch(() => setWalletBalance(null));
    }
  }, [token]);

  const navLinkClass = ({ isActive }) =>
    isActive
      ? "text-orange-500 font-bold"
      : "text-black hover:bg-green-500 hover:text-white px-3 py-2 rounded-md";

  return (
    <nav className="min-h-20 w-full bg-white border-b flex items-center fixed top-0 left-0 z-30">
      <div className="max-w-[95%] w-full mx-auto flex items-center justify-between">
        <NavLink to="/stations" className="text-xl font-bold hover:text-pink-500">
          CHARGE SLOT
        </NavLink>
        <div className="flex items-center gap-10">
          <NavLink to="/stations" className={navLinkClass}>
            Trang chủ
          </NavLink>
          {/* <NavLink to="/service" className={navLinkClass}>
            Sản phẩm dịch vụ
          </NavLink>
          <NavLink to="/news" className={navLinkClass}>
            Tin tức
          </NavLink>
          <NavLink to="/about" className={navLinkClass}>
            Về ChargeSlot
          </NavLink> */}
          <NavLink to="/owner/booking-requests" className={navLinkClass}>
            Booking
          </NavLink>
          <NavLink to="/owner/active-sessions" className={navLinkClass}>
            Phiên sạc
          </NavLink>
          <NavLink to="/owner/booking-requests" className={({ isActive }) =>
            location.pathname.includes("/dispute")
              ? "text-orange-500 font-bold"
              : isActive ? navLinkClass({ isActive: false }) : navLinkClass({ isActive: false })
          }>
            Khiếu nại
          </NavLink>
          <NavLink to="/stations" className={navLinkClass}>
            Danh sách trạm
          </NavLink>
          <NavLink to="/owner/reviews" className={navLinkClass}>
            Đánh giá
          </NavLink>
        </div>

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
              Đăng ký
            </Button>
          </div>
        )}

        {token && (
          <div className="flex items-center gap-3">
            <NotificationBell />
            <div className="relative" ref={dropdownRef}>
              <button
                onClick={() => setDropdownOpen((prev) => !prev)}
                className="flex items-center gap-2 cursor-pointer group focus:outline-none"
                aria-label="Menu hồ sơ"
              >
                <img
                  src={avatarSrc}
                  alt="Avatar"
                  className={`w-10 h-10 rounded-full object-cover border-2 transition-all duration-300 ${dropdownOpen
                    ? "border-orange-500 shadow-lg shadow-orange-200"
                    : "border-gray-200 group-hover:border-orange-400"
                    }`}
                />
                <svg
                  className={`w-4 h-4 text-gray-500 transition-transform duration-300 ${dropdownOpen ? "rotate-180" : ""
                    }`}
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                </svg>
              </button>

              <div
                className={`absolute right-0 top-full mt-3 w-72 transition-all duration-300 origin-top-right ${dropdownOpen
                  ? "opacity-100 scale-100 translate-y-0"
                  : "opacity-0 scale-95 -translate-y-2 pointer-events-none"
                  }`}
              >
                <div
                  className="rounded-xl shadow-2xl border border-gray-100 overflow-hidden"
                  style={{
                    background: "rgba(255, 255, 255, 0.95)",
                    backdropFilter: "blur(20px)",
                    WebkitBackdropFilter: "blur(20px)",
                  }}
                >
                  <div
                    className="px-5 py-4 flex items-center gap-3"
                    style={{
                      background: "linear-gradient(135deg, #ff7e29 0%, #f97316 100%)",
                    }}
                  >
                    <img
                      src={avatarSrc}
                      alt="Avatar"
                      className="w-12 h-12 rounded-full object-cover border-2 border-white/60 shadow-md"
                    />
                    <div className="min-w-0 flex-1">
                      <p className="text-white font-semibold text-sm truncate">
                        {maskPhone(phoneNumber) || "Người dùng"}
                      </p>
                      <span className="inline-block mt-0.5 px-2 py-0.5 text-xs font-medium rounded-full bg-white/20 text-white">
                        {roleLabels[normalizedRole] || normalizedRole}
                      </span>
                    </div>
                  </div>

                  <div className="px-5 py-3 space-y-2 border-b border-gray-100">
                    <div className="flex items-center gap-2 text-sm">
                      <svg className="w-4 h-4 text-gray-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
                      </svg>
                      <span className="text-gray-600">{maskPhone(phoneNumber) || "—"}</span>
                    </div>
                    <div className="flex items-center gap-2 text-sm">
                      <svg className="w-4 h-4 text-gray-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                      </svg>
                      <span className="text-gray-600">{roleLabels[normalizedRole] || normalizedRole}</span>
                    </div>
                  </div>

                    {/* Wallet balance */}
                    {walletBalance !== null && (
                      <div className="px-5 py-2.5 border-b border-gray-100">
                        <button
                          onClick={() => { setDropdownOpen(false); navigate("/owner/wallet"); }}
                          className="w-full flex items-center gap-3 text-sm text-gray-700 hover:bg-orange-50 hover:text-orange-600 transition-colors rounded-lg px-0 py-1 cursor-pointer"
                          style={{ background: "none", border: "none" }}
                        >
                          <svg className="w-4 h-4 text-orange-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z" />
                          </svg>
                          <span>Ví tiền</span>
                          <span className="ml-auto font-bold text-orange-600">{walletBalance.toLocaleString("vi-VN")}đ</span>
                        </button>
                      </div>
                    )}

                  <div className="py-1">
                    <button
                      onClick={() => {
                        setDropdownOpen(false);
                        navigate(profilePath);
                      }}
                      className="w-full px-5 py-2.5 text-left text-sm text-gray-700 hover:bg-orange-50 hover:text-orange-600 transition-colors flex items-center gap-3 cursor-pointer"
                    >
                      <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                      </svg>
                      Xem hồ sơ
                    </button>
                  </div>
                  <div className="border-t border-gray-100 py-1">
                    <button
                      onClick={() => {
                        setDropdownOpen(false);
                        logout();
                        navigate("/login");
                      }}
                      className="w-full px-5 py-2.5 text-left text-sm text-red-500 hover:bg-red-50 transition-colors flex items-center gap-3 cursor-pointer"
                    >
                      <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
                      </svg>
                      Đăng xuất
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </nav>
  );
}
