import { NavLink, useNavigate, useLocation } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { useAuthStore } from "@/stores/authStore";
import { useState, useRef, useEffect } from "react";
import { walletApi, chargingApi, bookingApi } from "@/services/api";
import NotificationBell from "@/components/NotificationBell";
import ChargeSlotLogo from "@/components/ChargeSlotLogo";


const DEFAULT_AVATAR =
  "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'%3E%3Ccircle cx='50' cy='50' r='50' fill='%23f97316'/%3E%3Ccircle cx='50' cy='38' r='16' fill='%23fff'/%3E%3Cellipse cx='50' cy='75' rx='28' ry='20' fill='%23fff'/%3E%3C/svg%3E";

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

export default function Nav() {
  const { logout, phoneNumber, role, token } = useAuthStore();
  const navigate = useNavigate();
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const [moreOpen, setMoreOpen] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [mobileMoreOpen, setMobileMoreOpen] = useState(false);
  const [toast, setToast] = useState(false);
  const [toastMsg, setToastMsg] = useState("");
  const [walletBalance, setWalletBalance] = useState(null);
  const location = useLocation();

  const isCheckinPage = location.pathname.startsWith("/driver/scan-qr")
    || location.pathname.startsWith("/driver/check-in")
    || location.pathname.startsWith("/driver/charging");
  const dropdownRef = useRef(null);
  const moreRef = useRef(null);
  const mobileMenuRef = useRef(null);
  const toastTimer = useRef(null);

  const normalizedRole = (role || "").toLowerCase();
  const avatarSrc = getStoredAvatarDataUrl(phoneNumber) || DEFAULT_AVATAR;

  const profilePath =
    normalizedRole === "owner"
      ? "/owner/owner-profile"
      : normalizedRole === "driver"
        ? "/driver/driver-profile"
        : "/";

  useEffect(() => {
    function handleClickOutside(e) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
        setDropdownOpen(false);
      }
      if (moreRef.current && !moreRef.current.contains(e.target)) {
        setMoreOpen(false);
      }
    }
    if (dropdownOpen || moreOpen) {
      document.addEventListener("mousedown", handleClickOutside);
    }
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [dropdownOpen, moreOpen]);

  // Lock body scroll when mobile menu is open
  useEffect(() => {
    if (mobileMenuOpen) {
      document.body.style.overflow = "hidden";
    } else {
      document.body.style.overflow = "";
    }
    return () => { document.body.style.overflow = ""; };
  }, [mobileMenuOpen]);

  // Fetch wallet balance cho driver — re-fetch khi mở dropdown
  useEffect(() => {
    if (token && normalizedRole === "driver") {
      walletApi.getWallet()
        .then(w => setWalletBalance(w?.availableBalance ?? null))
        .catch(() => setWalletBalance(null));
    }
  }, [token, normalizedRole, dropdownOpen]);

  // Check for active charging session
  const [activeSession, setActiveSession] = useState(null);
  const [sessionElapsed, setSessionElapsed] = useState(0);

  useEffect(() => {
    if (!token || normalizedRole !== "driver") return;
    function checkSession() {
      const uId = localStorage.getItem("userId") || "guest";
      const key = `activeChargingBooking_${uId}`;
      const bookingId = localStorage.getItem(key);
      if (bookingId) {
        chargingApi.getByBookingId(Number(bookingId))
          .then(data => {
            if (data && !data.actualEndTime) {
              setActiveSession(data);
            } else {
              localStorage.removeItem(key);
              setActiveSession(null);
            }
          })
          .catch(() => setActiveSession(null));
      } else {
        // Fallback: check bookings for active session
        bookingApi.getDriverBookings()
          .then(bookings => {
            const list = Array.isArray(bookings) ? bookings : (bookings?.items ?? []);
            const active = list.find(b => b.status === "CheckedIn" || b.status === "InProgress");
            if (active) {
              localStorage.setItem(key, String(active.id));
              chargingApi.getByBookingId(active.id)
                .then(data => {
                  if (data && !data.actualEndTime) setActiveSession(data);
                  else setActiveSession(null);
                })
                .catch(() => setActiveSession(null));
            } else {
              setActiveSession(null);
            }
          })
          .catch(() => setActiveSession(null));
      }
    }
    checkSession();
    const interval = setInterval(checkSession, 15000);
    return () => clearInterval(interval);
  }, [token, normalizedRole]);

  const [sessionWait, setSessionWait] = useState(0);
  const [isEarlyNav, setIsEarlyNav] = useState(false);

  // Timer for active session banner
  useEffect(() => {
    if (!activeSession) return;
    const interval = setInterval(() => {
      let scheduledMs = 0;
      if (activeSession.bookingStartTime) {
        const t = String(activeSession.bookingStartTime).replace("Z", "");
        let parsedMs = new Date(t).getTime();
        if (isNaN(parsedMs)) parsedMs = new Date(t + "+07:00").getTime();
        if (!isNaN(parsedMs)) scheduledMs = parsedMs;
      }

      if (scheduledMs > Date.now()) {
        // Chờ đến giờ sạc
        const diff = Math.floor((scheduledMs - Date.now()) / 1000);
        setIsEarlyNav(true);
        setSessionWait(diff);
      } else {
        // Đang sạc -> Đổi thành đếm ngược (Remaining Time)
        let endMs = Date.now();
        if (activeSession.bookingEndTime) {
          const t = String(activeSession.bookingEndTime).replace("Z", "");
          let parsedMs = new Date(t).getTime();
          if (isNaN(parsedMs)) parsedMs = new Date(t + "+07:00").getTime();
          if (!isNaN(parsedMs)) endMs = parsedMs;
        }
        
        setSessionElapsed(Math.max(0, Math.floor((endMs - Date.now()) / 1000)));
        setIsEarlyNav(false);
      }
    }, 1000);
    return () => clearInterval(interval);
  }, [activeSession]);

  function requireLogin(featureName, redirectTo) {
    if (!token) {
      setToastMsg(`Bạn phải đăng nhập vào hệ thống mới có thể ${featureName}`);
      setToast(true);
      clearTimeout(toastTimer.current);
      toastTimer.current = setTimeout(() => setToast(false), 3500);
      return;
    }
    if (redirectTo) navigate(redirectTo);
  }

  // Check if a path is active for "Khác" dropdown highlight
  const moreSubPaths = ["/driver/favorites", "/driver/loyalty", "/driver/chat-list", "/driver/reviews"];
  const isMoreActive = moreSubPaths.some(p => location.pathname.startsWith(p))
    || location.pathname.includes("/dispute");

  // Helper: nav item with icon
  function NavItem({ to, icon, label, onClick, isActive: forceActive }) {
    if (onClick) {
      return (
        <button
          onClick={onClick}
          className={`nav-item group ${forceActive ? "nav-item--active" : ""}`}
        >
          <span className="nav-item__icon">{icon}</span>
          <span className="nav-item__label">{label}</span>
        </button>
      );
    }
    return (
      <NavLink
        to={to}
        className={({ isActive }) =>
          `nav-item group ${isActive || forceActive ? "nav-item--active" : ""}`
        }
      >
        <span className="nav-item__icon">{icon}</span>
        <span className="nav-item__label">{label}</span>
      </NavLink>
    );
  }

  // "More" dropdown items
  const moreItems = [
    {
      icon: "❤️",
      label: "Trạm yêu thích",
      to: "/driver/favorites",
      loginMsg: "xem danh sách yêu thích",
    },
    {
      icon: "🏆",
      label: "Điểm thưởng",
      to: "/driver/loyalty",
      loginMsg: "xem điểm thưởng",
    },
    {
      icon: "💬",
      label: "Nhắn tin",
      to: "/driver/chat-list",
      loginMsg: "sử dụng tính năng chat",
    },
    {
      icon: "⚠️",
      label: "Khiếu nại",
      to: "/driver/disputes",
      loginMsg: "gửi khiếu nại",
      matchDispute: true,
    },
    {
      icon: "⭐",
      label: "Đánh giá trạm sạc",
      to: "/driver/reviews",
      loginMsg: "đánh giá trạm sạc",
    },
  ];

  // All nav items for mobile menu
  const allNavItems = [
    { to: "/", icon: "🏠", label: "Trang chủ" },
    { to: "/driver/map", icon: "🗺️", label: "Tìm trạm sạc" },
    ...(token ? [{ to: "/driver/my-bookings", icon: "📅", label: "Lịch đặt trạm" }] : []),
    { to: token ? "/driver/scan-qr" : null, icon: "📷", label: "Quét mã slot sạc", requireLogin: !token },
    { to: "/driver/favorites", icon: "❤️", label: "Trạm yêu thích", requireLogin: !token },
    { to: "/driver/loyalty", icon: "🏆", label: "Điểm thưởng", requireLogin: !token },
    { to: "/driver/chat-list", icon: "💬", label: "Nhắn tin", requireLogin: !token },
    { to: "/driver/reviews", icon: "⭐", label: "Đánh giá trạm sạc", requireLogin: !token },
    ...(token ? [{ to: "/driver/wallet", icon: "💰", label: "Ví điện tử" }] : []),
  ];

  return (
    <>
      <nav className="cs-nav">
        <div className="cs-nav__container">
          {/* Brand */}
          <NavLink to="/" className="cs-nav__brand">
            <ChargeSlotLogo size={34} showText />
          </NavLink>

          {/* Primary nav links */}
          <div className="cs-nav__links">
            <NavItem
              to="/"
              icon={<svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-4 0a1 1 0 01-1-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 01-1 1" /></svg>}
              label="Trang chủ"
            />
            <NavItem
              to="/driver/map"
              icon={<svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" /><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" /></svg>}
              label="Tìm trạm sạc"
            />
            {token ? (
              <NavItem
                to="/driver/my-bookings"
                icon={<svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" /></svg>}
                label="Lịch đặt"
              />
            ) : (
              <NavItem
                onClick={() => requireLogin("đặt lịch sạc")}
                icon={<svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" /></svg>}
                label="Lịch đặt"
              />
            )}
            <NavItem
              onClick={() => {
                if (token) navigate("/driver/scan-qr");
                else requireLogin("quét mã slot sạc", "/driver/scan-qr");
              }}
              icon={<svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v1m6 11h2m-6 0h-2v4m0-11v3m0 0h.01M12 12h4.01M16 20h4M4 12h4m12 0h.01M5 8h2a1 1 0 001-1V5a1 1 0 00-1-1H5a1 1 0 00-1 1v2a1 1 0 001 1zm12 0h2a1 1 0 001-1V5a1 1 0 00-1-1h-2a1 1 0 00-1 1v2a1 1 0 001 1zM5 20h2a1 1 0 001-1v-2a1 1 0 00-1-1H5a1 1 0 00-1 1v2a1 1 0 001 1z" /></svg>}
              label="Quét mã slot sạc"
              isActive={isCheckinPage}
            />

            {/* Active Charging Session link in navbar */}
            {activeSession && (
              <NavLink
                to="/driver/charging"
                className={({ isActive }) =>
                  `nav-item nav-item--charging group ${isActive ? "nav-item--active" : ""}`
                }
              >
                <span className="nav-item__pulse" />
                <span className="nav-item__icon">⚡</span>
                <span className="nav-item__label">Phiên sạc</span>
              </NavLink>
            )}

            {/* "Khác" (More) dropdown — groups secondary features */}
            <div className="relative" ref={moreRef}>
              <button
                onClick={() => setMoreOpen(prev => !prev)}
                className={`nav-item group ${isMoreActive ? "nav-item--active" : ""}`}
              >
                <span className="nav-item__icon">
                  <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
                  </svg>
                </span>
                <span className="nav-item__label">Khác</span>
                <svg
                  className={`w-3 h-3 ml-0.5 transition-transform duration-200 ${moreOpen ? "rotate-180" : ""}`}
                  fill="none" stroke="currentColor" viewBox="0 0 24 24"
                >
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                </svg>
              </button>
              <div
                className={`cs-more-dropdown ${moreOpen ? "cs-more-dropdown--open" : ""}`}
              >
                <div className="cs-more-dropdown__inner">
                  {moreItems.map((item) => {
                    const isItemActive = item.matchDispute
                      ? location.pathname.includes("/dispute")
                      : location.pathname.startsWith(item.to);
                    return (
                      <button
                        key={item.label}
                        onClick={() => {
                          setMoreOpen(false);
                          if (!token) {
                            requireLogin(item.loginMsg);
                          } else {
                            navigate(item.to);
                          }
                        }}
                        className={`cs-more-dropdown__item ${isItemActive ? "cs-more-dropdown__item--active" : ""}`}
                      >
                        <span className="cs-more-dropdown__item-icon">{item.icon}</span>
                        <span>{item.label}</span>
                      </button>
                    );
                  })}
                </div>
              </div>
            </div>
          </div>

          {/* Right section */}
          <div className="cs-nav__right">
            {!token && (
              <div className="flex items-center gap-2">
                <Button
                  className="cs-btn cs-btn--login"
                  onClick={() => navigate("/login")}
                >
                  Đăng nhập
                </Button>
                <Button
                  className="cs-btn cs-btn--register"
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
                    className="cs-avatar-btn group"
                    aria-label="Menu hồ sơ"
                  >
                    <img
                      src={avatarSrc}
                      alt="Avatar"
                      className={`cs-avatar-btn__img ${dropdownOpen ? "cs-avatar-btn__img--open" : ""}`}
                    />
                    <svg
                      className={`w-4 h-4 text-gray-400 transition-transform duration-300 ${dropdownOpen ? "rotate-180" : ""}`}
                      fill="none"
                      stroke="currentColor"
                      viewBox="0 0 24 24"
                    >
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                    </svg>
                  </button>

                  <div
                    className={`cs-profile-dropdown ${dropdownOpen ? "cs-profile-dropdown--open" : ""}`}
                  >
                    <div className="cs-profile-dropdown__card">
                      {/* Header */}
                      <div className="cs-profile-dropdown__header">
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

                      {/* Info */}
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

                      {/* Wallet balance (driver only) */}
                      {normalizedRole === "driver" && walletBalance !== null && (
                        <div className="px-5 py-2.5 border-b border-gray-100">
                          <button
                            onClick={() => { setDropdownOpen(false); navigate("/driver/wallet"); }}
                            className="w-full flex items-center gap-3 text-sm text-gray-700 hover:bg-orange-50 hover:text-orange-600 transition-colors rounded-lg px-0 py-1 cursor-pointer"
                            style={{ background: "none", border: "none" }}
                          >
                            <svg className="w-4 h-4 text-orange-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z" />
                            </svg>
                            <span>Ví điện tử</span>
                            <span className="ml-auto font-bold text-orange-600">{walletBalance.toLocaleString("vi-VN")}đ</span>
                          </button>
                        </div>
                      )}

                      {/* Profile */}
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

                      {/* Logout */}
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

          {/* Hamburger button (mobile only) */}
          <button
            className="cs-hamburger"
            onClick={() => setMobileMenuOpen(prev => !prev)}
            aria-label="Mở menu"
          >
            {mobileMenuOpen ? (
              <svg width="22" height="22" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M6 18L18 6M6 6l12 12" />
              </svg>
            ) : (
              <svg width="22" height="22" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M4 6h16M4 12h16M4 18h16" />
              </svg>
            )}
          </button>
        </div>
      </nav>

      {/* Mobile Menu Overlay */}
      <div
        className={`cs-mobile-overlay ${mobileMenuOpen ? "cs-mobile-overlay--open" : ""}`}
        onClick={() => setMobileMenuOpen(false)}
      />

      {/* Mobile Menu Drawer */}
      <div
        ref={mobileMenuRef}
        className={`cs-mobile-menu ${mobileMenuOpen ? "cs-mobile-menu--open" : ""}`}
      >
        {/* Mobile menu header */}
        <div className="cs-mobile-menu__header">
          <ChargeSlotLogo size={30} showText />
          <button
            className="cs-mobile-menu__close"
            onClick={() => setMobileMenuOpen(false)}
          >
            <svg width="20" height="20" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* User info (if logged in) */}
        {token && (
          <div className="cs-mobile-menu__user">
            <img
              src={getStoredAvatarDataUrl(phoneNumber) || DEFAULT_AVATAR}
              alt="Avatar"
              className="cs-mobile-menu__avatar"
            />
            <div>
              <p className="cs-mobile-menu__phone">{maskPhone(phoneNumber) || "Người dùng"}</p>
              <span className="cs-mobile-menu__role">{roleLabels[(role || "").toLowerCase()] || role}</span>
            </div>
          </div>
        )}

        {/* Nav Items */}
        <div className="cs-mobile-menu__nav">
          {allNavItems.map((item, i) => {
            if (item.requireLogin) {
              return (
                <button
                  key={i}
                  className="cs-mobile-nav-item"
                  onClick={() => {
                    setMobileMenuOpen(false);
                    requireLogin(item.label);
                  }}
                >
                  <span className="cs-mobile-nav-item__icon">{item.icon}</span>
                  <span>{item.label}</span>
                  <svg className="cs-mobile-nav-item__arrow" width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                  </svg>
                </button>
              );
            }
            return (
              <NavLink
                key={i}
                to={item.to}
                className={({ isActive }) =>
                  `cs-mobile-nav-item ${isActive ? "cs-mobile-nav-item--active" : ""}`
                }
                onClick={() => setMobileMenuOpen(false)}
              >
                <span className="cs-mobile-nav-item__icon">{item.icon}</span>
                <span>{item.label}</span>
                <svg className="cs-mobile-nav-item__arrow" width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                </svg>
              </NavLink>
            );
          })}
        </div>

        {/* Auth actions */}
        <div className="cs-mobile-menu__footer">
          {!token ? (
            <>
              <button
                className="cs-mobile-menu__btn cs-mobile-menu__btn--primary"
                onClick={() => { setMobileMenuOpen(false); navigate("/login"); }}
              >
                🔑 Đăng nhập
              </button>
              <button
                className="cs-mobile-menu__btn cs-mobile-menu__btn--secondary"
                onClick={() => { setMobileMenuOpen(false); navigate("/register"); }}
              >
                📝 Đăng ký miễn phí
              </button>
            </>
          ) : (
            <>
              <button
                className="cs-mobile-menu__btn cs-mobile-menu__btn--secondary"
                onClick={() => { setMobileMenuOpen(false); navigate(profilePath); }}
              >
                👤 Xem hồ sơ
              </button>
              <button
                className="cs-mobile-menu__btn cs-mobile-menu__btn--danger"
                onClick={() => { setMobileMenuOpen(false); logout(); navigate("/login"); }}
              >
                🚪 Đăng xuất
              </button>
            </>
          )}
        </div>
      </div>

      {/* Login toast */}
      <div
        className={`fixed top-24 left-1/2 -translate-x-1/2 z-50 transition-all duration-500 ${toast
          ? "opacity-100 translate-y-0"
          : "opacity-0 -translate-y-4 pointer-events-none"
          }`}
      >
        <div className="cs-toast">
          <div className="cs-toast__icon-wrap">
            <svg className="w-5 h-5 text-orange-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z" />
            </svg>
          </div>
          <div>
            <p className="text-sm font-semibold text-gray-800">Yêu cầu đăng nhập</p>
            <p className="text-xs text-gray-500 mt-0.5">{toastMsg}</p>
          </div>
          <button onClick={() => { setToast(false); navigate("/login"); }} className="cs-toast__login-btn">
            Đăng nhập
          </button>
          <button onClick={() => setToast(false)} className="ml-1 text-gray-400 hover:text-gray-600 cursor-pointer">
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
      </div>

      {/* Active Charging Session floating banner */}
      {activeSession && normalizedRole === "driver" && !location.pathname.startsWith("/driver/charging") && (
        <div
          onClick={() => navigate("/driver/charging")}
          className="cs-charging-banner"
        >
          <div className="cs-charging-banner__pulse" />
          <span className="cs-charging-banner__label">
            {isEarlyNav ? "⏳ Chờ giờ sạc" : "⚡ Đang sạc"} — {activeSession.stationName || "Phiên sạc"}
          </span>
          <span className="cs-charging-banner__timer">
            {String(Math.floor((isEarlyNav ? sessionWait : sessionElapsed) / 3600)).padStart(2, "0")}:{String(Math.floor(((isEarlyNav ? sessionWait : sessionElapsed) % 3600) / 60)).padStart(2, "0")}:{String((isEarlyNav ? sessionWait : sessionElapsed) % 60).padStart(2, "0")}
          </span>
          <svg width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
          </svg>
        </div>
      )}

      {/* ===== MOBILE BOTTOM NAVIGATION BAR ===== */}
      {/* Chỉ hiển thị khi mobile và không phải trang fullscreen */}
      {!location.pathname.startsWith("/driver/scan-qr") &&
        !location.pathname.startsWith("/driver/check-in") &&
        !location.pathname.startsWith("/driver/charging") && (
          <nav className="cs-bottom-nav" aria-label="Điều hướng chính">
            {/* Trang chủ */}
            <NavLink to="/" end className={({ isActive }) => `cs-bottom-nav__item ${isActive ? "cs-bottom-nav__item--active" : ""}`}>
              <svg className="cs-bottom-nav__icon" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-4 0a1 1 0 01-1-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 01-1 1" />
              </svg>
              <span className="cs-bottom-nav__label">Trang chủ</span>
            </NavLink>

            {/* Tìm trạm */}
            <NavLink to="/driver/map" className={({ isActive }) => `cs-bottom-nav__item ${isActive ? "cs-bottom-nav__item--active" : ""}`}>
              <svg className="cs-bottom-nav__icon" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
              </svg>
              <span className="cs-bottom-nav__label">Tìm trạm sạc</span>
            </NavLink>

            {/* Check-in — FAB nổi bật ở giữa */}
            <button
              className="cs-bottom-nav__fab"
              onClick={() => {
                if (token) navigate("/driver/scan-qr");
                else requireLogin("quét mã slot sạc");
              }}
              aria-label="Check-in QR"
            >
              <div className="cs-bottom-nav__fab-inner">
                <svg width="26" height="26" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v1m6 11h2m-6 0h-2v4m0-11v3m0 0h.01M12 12h4.01M16 20h4M4 12h4m12 0h.01M5 8h2a1 1 0 001-1V5a1 1 0 00-1-1H5a1 1 0 00-1 1v2a1 1 0 001 1zm12 0h2a1 1 0 001-1V5a1 1 0 00-1-1h-2a1 1 0 00-1 1v2a1 1 0 001 1zM5 20h2a1 1 0 001-1v-2a1 1 0 00-1-1H5a1 1 0 00-1 1v2a1 1 0 001 1z" />
                </svg>
              </div>
              <span className="cs-bottom-nav__fab-label">Quét mã</span>
            </button>

            {/* Booking */}
            <button
              className={`cs-bottom-nav__item ${location.pathname.startsWith("/driver/my-bookings") || location.pathname.startsWith("/driver/booking") ? "cs-bottom-nav__item--active" : ""}`}
              onClick={() => {
                if (token) navigate("/driver/my-bookings");
                else requireLogin("xem booking");
              }}
            >
              <div style={{ position: "relative", display: "inline-flex" }}>
                <svg className="cs-bottom-nav__icon" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                </svg>
                {/* badge khi có active booking */}
                {activeSession && (
                  <span style={{
                    position: "absolute", top: -3, right: -4,
                    width: 8, height: 8, borderRadius: "50%",
                    background: "#22c55e", border: "2px solid #fff",
                  }} />
                )}
              </div>
              <span className="cs-bottom-nav__label">Lịch đặt</span>
            </button>

            {/* Tôi — mở bottom sheet với đầy đủ mục */}
            <button
              className={`cs-bottom-nav__item ${location.pathname.startsWith("/driver/driver-profile") ||
                location.pathname.startsWith("/driver/wallet") ||
                location.pathname.startsWith("/driver/favorites") ||
                location.pathname.startsWith("/driver/loyalty") ||
                location.pathname.startsWith("/driver/reviews") ||
                location.pathname.startsWith("/driver/chat") ||
                mobileMoreOpen
                ? "cs-bottom-nav__item--active" : ""
                }`}
              onClick={() => setMobileMoreOpen(true)}
            >
              {token ? (
                <img
                  src={avatarSrc}
                  alt="Avatar"
                  style={{
                    width: 24, height: 24, borderRadius: "50%",
                    objectFit: "cover",
                    border: mobileMoreOpen ? "2px solid #f97316" : "2px solid #e5e7eb",
                    transition: "border-color 0.2s"
                  }}
                />
              ) : (
                <svg className="cs-bottom-nav__icon" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                </svg>
              )}
              <span className="cs-bottom-nav__label">Tôi</span>
            </button>
          </nav>
        )}

      {/* ===== MOBILE MORE BOTTOM SHEET ===== */}
      {mobileMoreOpen && (
        <div
          className="cs-more-sheet-overlay"
          onClick={() => setMobileMoreOpen(false)}
        />
      )}
      <div className={`cs-more-sheet ${mobileMoreOpen ? "cs-more-sheet--open" : ""}`}>
        {/* Handle bar */}
        <div style={{ display: "flex", justifyContent: "center", padding: "12px 0 8px" }}>
          <div style={{ width: 40, height: 4, borderRadius: 2, background: "#e2e8f0" }} />
        </div>

        {/* User info (if logged in) */}
        {token && (
          <div className="cs-more-sheet__user">
            <img src={avatarSrc} alt="Avatar" className="cs-more-sheet__avatar" />
            <div>
              <p className="cs-more-sheet__phone">{maskPhone(phoneNumber) || "Người dùng"}</p>
              <span className="cs-more-sheet__role">{roleLabels[normalizedRole] || "Tài xế"}</span>
            </div>
          </div>
        )}

        {/* Nav items grid */}
        <div className="cs-more-sheet__grid">
          {[
            { emoji: "👤", label: "Hồ sơ", to: "/driver/driver-profile", auth: true },
            { emoji: "💳", label: "Ví điện tử", to: "/driver/wallet", auth: true },
            { emoji: "❤️", label: "Trạm yêu thích", to: "/driver/favorites", auth: true },
            { emoji: "🏆", label: "Điểm thưởng", to: "/driver/loyalty", auth: true },
            { emoji: "💬", label: "Nhắn tin", to: "/driver/chat-list", auth: true },
            { emoji: "⭐", label: "Đánh giá trạm sạc", to: "/driver/reviews", auth: true },
            { emoji: "⚠️", label: "Khiếu nại", to: "/driver/disputes", auth: true },
          ].map((item) => (
            <button
              key={item.to}
              className="cs-more-sheet__item"
              onClick={() => {
                setMobileMoreOpen(false);
                if (item.auth && !token) requireLogin(item.label);
                else navigate(item.to);
              }}
            >
              <span className="cs-more-sheet__item-emoji">{item.emoji}</span>
              <span className="cs-more-sheet__item-label">{item.label}</span>
            </button>
          ))}
        </div>

        {/* Footer actions */}
        <div className="cs-more-sheet__footer">
          {!token ? (
            <button
              className="cs-more-sheet__login-btn"
              onClick={() => { setMobileMoreOpen(false); navigate("/login"); }}
            >
              🔑 Đăng nhập
            </button>
          ) : (
            <button
              className="cs-more-sheet__logout-btn"
              onClick={() => { setMobileMoreOpen(false); logout(); navigate("/login"); }}
            >
              🚪 Đăng xuất
            </button>
          )}
        </div>
      </div>

      <style>{`
        /* ===== NAVBAR CORE ===== */
        .cs-nav {
          position: fixed;
          top: 0;
          left: 0;
          width: 100%;
          height: 64px;
          background: rgba(255, 255, 255, 0.92);
          backdrop-filter: blur(16px);
          -webkit-backdrop-filter: blur(16px);
          border-bottom: 1px solid rgba(0, 0, 0, 0.06);
          z-index: 30;
          display: flex;
          align-items: center;
          box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
        }
        .cs-nav__container {
          max-width: 1400px;
          width: 95%;
          margin: 0 auto;
          display: flex;
          align-items: center;
          justify-content: space-between;
          gap: 16px;
        }

        /* ===== BRAND ===== */
        .cs-nav__brand {
          display: flex;
          align-items: center;
          gap: 8px;
          text-decoration: none;
          flex-shrink: 0;
        }
        .cs-nav__brand-icon {
          font-size: 22px;
          line-height: 1;
        }
        .cs-nav__brand-text {
          font-size: 18px;
          font-weight: 800;
          letter-spacing: -0.5px;
          background: linear-gradient(135deg, #f97316 0%, #ea580c 100%);
          -webkit-background-clip: text;
          -webkit-text-fill-color: transparent;
          background-clip: text;
        }

        /* ===== NAV LINKS ===== */
        .cs-nav__links {
          display: flex;
          align-items: center;
          gap: 2px;
        }

        /* ===== NAV ITEM ===== */
        .nav-item {
          display: flex;
          align-items: center;
          gap: 6px;
          padding: 8px 14px;
          border-radius: 10px;
          font-size: 14px;
          font-weight: 500;
          color: #4b5563;
          text-decoration: none;
          transition: all 0.2s ease;
          cursor: pointer;
          border: none;
          background: none;
          white-space: nowrap;
          position: relative;
        }
        .nav-item:hover {
          background: #fff7ed;
          color: #ea580c;
        }
        .nav-item--active {
          color: #ea580c;
          font-weight: 600;
          background: #fff7ed;
        }
        .nav-item--active::after {
          content: '';
          position: absolute;
          bottom: 0;
          left: 50%;
          transform: translateX(-50%);
          width: 20px;
          height: 3px;
          border-radius: 3px;
          background: linear-gradient(90deg, #f97316, #ea580c);
        }
        .nav-item__icon {
          display: flex;
          align-items: center;
          opacity: 0.7;
          transition: opacity 0.2s;
        }
        .nav-item:hover .nav-item__icon,
        .nav-item--active .nav-item__icon {
          opacity: 1;
        }
        .nav-item__label {
          line-height: 1;
        }

        /* ===== CHARGING NAV ITEM ===== */
        .nav-item--charging {
          color: #2563eb;
          background: #eff6ff;
        }
        .nav-item--charging:hover {
          background: #dbeafe;
          color: #1d4ed8;
        }
        .nav-item__pulse {
          width: 8px;
          height: 8px;
          border-radius: 50%;
          background: #22c55e;
          box-shadow: 0 0 6px #22c55e;
          animation: cs-pulse-dot 1.5s infinite;
        }

        /* ===== MORE DROPDOWN ===== */
        .cs-more-dropdown {
          position: absolute;
          right: 0;
          top: 100%;
          margin-top: 8px;
          width: 200px;
          opacity: 0;
          transform: scale(0.95) translateY(-8px);
          pointer-events: none;
          transition: all 0.25s cubic-bezier(0.16, 1, 0.3, 1);
          transform-origin: top right;
          z-index: 50;
        }
        .cs-more-dropdown--open {
          opacity: 1;
          transform: scale(1) translateY(0);
          pointer-events: auto;
        }
        .cs-more-dropdown__inner {
          background: rgba(255, 255, 255, 0.97);
          backdrop-filter: blur(20px);
          -webkit-backdrop-filter: blur(20px);
          border: 1px solid rgba(0, 0, 0, 0.06);
          border-radius: 14px;
          padding: 6px;
          box-shadow: 0 12px 40px rgba(0, 0, 0, 0.1), 0 2px 8px rgba(0, 0, 0, 0.04);
        }
        .cs-more-dropdown__item {
          display: flex;
          align-items: center;
          gap: 10px;
          width: 100%;
          padding: 10px 14px;
          border: none;
          background: none;
          border-radius: 10px;
          font-size: 14px;
          font-weight: 500;
          color: #4b5563;
          cursor: pointer;
          transition: all 0.15s ease;
          text-align: left;
        }
        .cs-more-dropdown__item:hover {
          background: #fff7ed;
          color: #ea580c;
        }
        .cs-more-dropdown__item--active {
          background: #fff7ed;
          color: #ea580c;
          font-weight: 600;
        }
        .cs-more-dropdown__item-icon {
          font-size: 16px;
          flex-shrink: 0;
        }

        /* ===== RIGHT SECTION ===== */
        .cs-nav__right {
          display: flex;
          align-items: center;
          flex-shrink: 0;
        }

        /* ===== AUTH BUTTONS ===== */
        .cs-btn {
          border-radius: 10px !important;
          font-weight: 600 !important;
          font-size: 13px !important;
          padding: 8px 18px !important;
          cursor: pointer !important;
          transition: all 0.2s ease !important;
          border: none !important;
        }
        .cs-btn--login {
          background: linear-gradient(135deg, #f97316 0%, #ea580c 100%) !important;
          color: white !important;
          box-shadow: 0 2px 8px rgba(249, 115, 22, 0.3) !important;
        }
        .cs-btn--login:hover {
          transform: translateY(-1px) !important;
          box-shadow: 0 4px 16px rgba(249, 115, 22, 0.4) !important;
        }
        .cs-btn--register {
          background: white !important;
          color: #ea580c !important;
          border: 1.5px solid #fed7aa !important;
        }
        .cs-btn--register:hover {
          background: #fff7ed !important;
          border-color: #f97316 !important;
        }

        /* ===== AVATAR BUTTON ===== */
        .cs-avatar-btn {
          display: flex;
          align-items: center;
          gap: 6px;
          cursor: pointer;
          border: none;
          background: none;
          padding: 4px;
          border-radius: 50px;
          transition: background 0.2s;
        }
        .cs-avatar-btn:hover {
          background: #f3f4f6;
        }
        .cs-avatar-btn__img {
          width: 36px;
          height: 36px;
          border-radius: 50%;
          object-fit: cover;
          border: 2px solid #e5e7eb;
          transition: all 0.3s;
        }
        .cs-avatar-btn__img--open,
        .cs-avatar-btn:hover .cs-avatar-btn__img {
          border-color: #f97316;
          box-shadow: 0 0 0 3px rgba(249, 115, 22, 0.15);
        }

        /* ===== PROFILE DROPDOWN ===== */
        .cs-profile-dropdown {
          position: absolute;
          right: 0;
          top: 100%;
          margin-top: 10px;
          width: 280px;
          opacity: 0;
          transform: scale(0.95) translateY(-8px);
          pointer-events: none;
          transition: all 0.25s cubic-bezier(0.16, 1, 0.3, 1);
          transform-origin: top right;
          z-index: 50;
        }
        .cs-profile-dropdown--open {
          opacity: 1;
          transform: scale(1) translateY(0);
          pointer-events: auto;
        }
        .cs-profile-dropdown__card {
          background: rgba(255, 255, 255, 0.97);
          backdrop-filter: blur(20px);
          -webkit-backdrop-filter: blur(20px);
          border: 1px solid rgba(0, 0, 0, 0.06);
          border-radius: 16px;
          overflow: hidden;
          box-shadow: 0 16px 48px rgba(0, 0, 0, 0.12), 0 2px 8px rgba(0, 0, 0, 0.04);
        }
        .cs-profile-dropdown__header {
          padding: 16px 20px;
          display: flex;
          align-items: center;
          gap: 12px;
          background: linear-gradient(135deg, #f97316 0%, #ea580c 100%);
        }

        /* ===== TOAST ===== */
        .cs-toast {
          display: flex;
          align-items: center;
          gap: 12px;
          background: rgba(255, 255, 255, 0.97);
          backdrop-filter: blur(16px);
          -webkit-backdrop-filter: blur(16px);
          border: 1px solid #fed7aa;
          box-shadow: 0 12px 40px rgba(0, 0, 0, 0.1);
          border-radius: 14px;
          padding: 14px 18px;
          min-width: min(380px, calc(100vw - 32px));
          max-width: calc(100vw - 32px);
        }
        .cs-toast__icon-wrap {
          width: 40px;
          height: 40px;
          border-radius: 50%;
          background: #fff7ed;
          display: flex;
          align-items: center;
          justify-content: center;
          flex-shrink: 0;
        }
        .cs-toast__login-btn {
          margin-left: auto;
          padding: 6px 16px;
          border-radius: 8px;
          font-size: 13px;
          font-weight: 600;
          color: white;
          background: linear-gradient(135deg, #f97316, #ea580c);
          border: none;
          cursor: pointer;
          transition: all 0.2s;
          white-space: nowrap;
        }
        .cs-toast__login-btn:hover {
          transform: translateY(-1px);
          box-shadow: 0 4px 12px rgba(249, 115, 22, 0.3);
        }

        /* ===== CHARGING BANNER ===== */
        .cs-charging-banner {
          position: fixed;
          bottom: 24px;
          left: 50%;
          transform: translateX(-50%);
          z-index: 50;
          cursor: pointer;
          background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
          color: #fff;
          border-radius: 50px;
          padding: 12px 24px;
          display: flex;
          align-items: center;
          gap: 12px;
          box-shadow: 0 8px 32px rgba(59, 130, 246, 0.35);
          animation: cs-pulse 2s infinite;
        }
        .cs-charging-banner__pulse {
          width: 10px;
          height: 10px;
          border-radius: 50%;
          background: #4ade80;
          box-shadow: 0 0 8px #4ade80;
        }
        .cs-charging-banner__label {
          font-weight: 700;
          font-size: 14px;
        }
        .cs-charging-banner__timer {
          font-weight: 800;
          font-family: monospace;
          font-size: 15px;
        }

        /* ===== ANIMATIONS ===== */
        @keyframes cs-pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.85; } }
        @keyframes cs-pulse-dot { 0%, 100% { opacity: 1; transform: scale(1); } 50% { opacity: 0.5; transform: scale(0.8); } }
        @keyframes cs-slideInRight { from { transform: translateX(100%); } to { transform: translateX(0); } }

        /* ===== HAMBURGER BUTTON ===== */
        .cs-hamburger {
          display: none;
          align-items: center;
          justify-content: center;
          width: 40px;
          height: 40px;
          border: none;
          background: none;
          border-radius: 10px;
          color: #4b5563;
          cursor: pointer;
          transition: all 0.2s;
          flex-shrink: 0;
        }
        .cs-hamburger:hover {
          background: #f3f4f6;
          color: #ea580c;
        }

        /* ===== MOBILE OVERLAY ===== */
        .cs-mobile-overlay {
          display: none;
          position: fixed;
          inset: 0;
          background: rgba(0, 0, 0, 0.5);
          z-index: 40;
          opacity: 0;
          transition: opacity 0.3s ease;
        }
        .cs-mobile-overlay--open {
          opacity: 1;
        }

        /* ===== MOBILE MENU DRAWER ===== */
        .cs-mobile-menu {
          display: none;
          position: fixed;
          top: 0;
          right: 0;
          width: min(320px, 100vw);
          height: 100vh;
          background: rgba(255, 255, 255, 0.98);
          backdrop-filter: blur(20px);
          -webkit-backdrop-filter: blur(20px);
          z-index: 45;
          flex-direction: column;
          transform: translateX(100%);
          transition: transform 0.35s cubic-bezier(0.16, 1, 0.3, 1);
          box-shadow: -8px 0 40px rgba(0, 0, 0, 0.15);
          overflow-y: auto;
        }
        .cs-mobile-menu--open {
          transform: translateX(0);
        }
        .cs-mobile-menu__header {
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 16px 20px;
          border-bottom: 1px solid #f1f5f9;
          flex-shrink: 0;
        }
        .cs-mobile-menu__close {
          width: 36px;
          height: 36px;
          border: none;
          background: #f8fafc;
          border-radius: 10px;
          display: flex;
          align-items: center;
          justify-content: center;
          color: #64748b;
          cursor: pointer;
          transition: all 0.2s;
        }
        .cs-mobile-menu__close:hover {
          background: #fff7ed;
          color: #ea580c;
        }
        .cs-mobile-menu__user {
          display: flex;
          align-items: center;
          gap: 12px;
          padding: 16px 20px;
          background: linear-gradient(135deg, #fff7ed, #fed7aa);
          margin: 12px 16px;
          border-radius: 16px;
        }
        .cs-mobile-menu__avatar {
          width: 44px;
          height: 44px;
          border-radius: 50%;
          object-fit: cover;
          border: 2px solid rgba(249, 115, 22, 0.3);
          flex-shrink: 0;
        }
        .cs-mobile-menu__phone {
          font-size: 14px;
          font-weight: 600;
          color: #1e293b;
          margin: 0 0 2px;
        }
        .cs-mobile-menu__role {
          font-size: 12px;
          color: #ea580c;
          font-weight: 500;
        }
        .cs-mobile-menu__nav {
          flex: 1;
          padding: 8px 12px;
          display: flex;
          flex-direction: column;
          gap: 2px;
        }
        .cs-mobile-nav-item {
          display: flex;
          align-items: center;
          gap: 12px;
          padding: 13px 16px;
          border-radius: 12px;
          font-size: 15px;
          font-weight: 500;
          color: #374151;
          text-decoration: none;
          transition: all 0.15s;
          border: none;
          background: none;
          width: 100%;
          text-align: left;
          cursor: pointer;
        }
        .cs-mobile-nav-item:hover {
          background: #fff7ed;
          color: #ea580c;
        }
        .cs-mobile-nav-item--active {
          background: #fff7ed;
          color: #ea580c;
          font-weight: 600;
        }
        .cs-mobile-nav-item__icon {
          font-size: 18px;
          width: 28px;
          text-align: center;
          flex-shrink: 0;
        }
        .cs-mobile-nav-item__arrow {
          margin-left: auto;
          color: #cbd5e1;
          flex-shrink: 0;
        }
        .cs-mobile-menu__footer {
          padding: 16px;
          border-top: 1px solid #f1f5f9;
          display: flex;
          flex-direction: column;
          gap: 10px;
          flex-shrink: 0;
        }
        .cs-mobile-menu__btn {
          width: 100%;
          padding: 13px 20px;
          border-radius: 12px;
          font-size: 15px;
          font-weight: 600;
          cursor: pointer;
          border: none;
          transition: all 0.2s;
          text-align: center;
        }
        .cs-mobile-menu__btn--primary {
          background: linear-gradient(135deg, #f97316, #ea580c);
          color: white;
          box-shadow: 0 4px 16px rgba(249, 115, 22, 0.3);
        }
        .cs-mobile-menu__btn--primary:hover {
          box-shadow: 0 6px 20px rgba(249, 115, 22, 0.4);
          transform: translateY(-1px);
        }
        .cs-mobile-menu__btn--secondary {
          background: #f8fafc;
          color: #374151;
          border: 1.5px solid #e2e8f0;
        }
        .cs-mobile-menu__btn--secondary:hover {
          background: #fff7ed;
          color: #ea580c;
          border-color: #fed7aa;
        }
        .cs-mobile-menu__btn--danger {
          background: #fef2f2;
          color: #dc2626;
          border: 1.5px solid #fecaca;
        }
        .cs-mobile-menu__btn--danger:hover {
          background: #fee2e2;
        }

        /* ===== MOBILE MORE BOTTOM SHEET ===== */
        .cs-more-sheet-overlay {
          position: fixed; inset: 0; background: rgba(0,0,0,0.45);
          z-index: 45; backdrop-filter: blur(2px);
          animation: fadeIn 0.2s ease;
        }
        @keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
        .cs-more-sheet {
          position: fixed; bottom: 0; left: 0; right: 0; z-index: 46;
          background: #ffffff;
          border-radius: 20px 20px 0 0;
          box-shadow: 0 -8px 40px rgba(0,0,0,0.15);
          transform: translateY(100%);
          transition: transform 0.32s cubic-bezier(0.16,1,0.3,1);
          padding-bottom: calc(16px + env(safe-area-inset-bottom, 0px));
          max-height: 85vh; overflow-y: auto;
        }
        .cs-more-sheet--open { transform: translateY(0); }
        .cs-more-sheet__user {
          display: flex; align-items: center; gap: 12px;
          margin: 0 16px 16px; padding: 14px 16px;
          background: linear-gradient(135deg, #fff7ed, #ffedd5);
          border-radius: 14px;
        }
        .cs-more-sheet__avatar {
          width: 48px; height: 48px; border-radius: 50%; object-fit: cover;
          border: 2px solid rgba(249,115,22,0.3); flex-shrink: 0;
        }
        .cs-more-sheet__phone { font-size: 14px; font-weight: 700; color: #1e293b; margin: 0 0 2px; }
        .cs-more-sheet__role { font-size: 12px; color: #ea580c; font-weight: 500; }
        .cs-more-sheet__grid {
          display: grid; grid-template-columns: repeat(3, 1fr);
          gap: 8px; padding: 0 16px 8px;
        }
        .cs-more-sheet__item {
          display: flex; flex-direction: column; align-items: center; gap: 8px;
          padding: 16px 8px; border: none; background: #f8fafc;
          border-radius: 14px; cursor: pointer; transition: all 0.15s;
          -webkit-tap-highlight-color: transparent;
        }
        .cs-more-sheet__item:active { transform: scale(0.94); background: #fff7ed; }
        .cs-more-sheet__item-emoji { font-size: 26px; line-height: 1; }
        .cs-more-sheet__item-label { font-size: 12px; font-weight: 600; color: #374151; text-align: center; }
        .cs-more-sheet__footer {
          padding: 12px 16px 4px;
          border-top: 1px solid #f1f5f9;
        }
        .cs-more-sheet__login-btn, .cs-more-sheet__logout-btn {
          width: 100%; padding: 14px 20px; border-radius: 14px;
          font-size: 15px; font-weight: 600; cursor: pointer; border: none;
          transition: all 0.2s; text-align: center;
        }
        .cs-more-sheet__login-btn {
          background: linear-gradient(135deg, #f97316, #ea580c);
          color: white; box-shadow: 0 2px 12px rgba(249,115,22,0.3);
        }
        .cs-more-sheet__logout-btn {
          background: #fef2f2; color: #dc2626; border: 1.5px solid #fecaca;
        }
        .cs-more-sheet__logout-btn:active { background: #fee2e2; }

        /* ===== RESPONSIVE BREAKPOINTS ===== */
        @media (max-width: 768px) {
          .cs-nav { height: 52px; }
          /* Ẩn nav links desktop */
          .cs-nav__links { display: none !important; }
          /* Ẩn hamburger & mobile drawer (dùng bottom nav thay thế) */
          .cs-hamburger { display: none !important; }
          .cs-mobile-overlay { display: none !important; }
          .cs-mobile-menu { display: none !important; }
          /* Ẩn nút Đăng nhập/Đăng ký desktop — tab Tôi ở bottom nav lo việc này */
          .cs-nav__right .flex.items-center.gap-2 { display: none !important; }
          /* GIỮ NGUYÊN bell + avatar để người dùng đăng xuất được */
          .cs-nav__right { display: flex !important; }
          /* Chỉ ẩn chevron avatar cho gọn trên mobile */
          .cs-avatar-btn > svg.w-4 { display: none !important; }
          /* Charging banner lên trên bottom nav */
          .cs-charging-banner {
            bottom: calc(68px + env(safe-area-inset-bottom, 0px) + 8px);
            font-size: 12px; padding: 10px 16px; gap: 8px;
            max-width: calc(100vw - 24px);
          }
          .cs-charging-banner__label { font-size: 12px; }
          .cs-charging-banner__timer { font-size: 13px; }
        }

        @media (min-width: 769px) {
          .cs-bottom-nav { display: none !important; }
        }

        /* ===== BOTTOM NAVIGATION BAR ===== */
        .cs-bottom-nav {
          display: none; /* hidden by default, shown on mobile via media query below */
          position: fixed;
          bottom: 0;
          left: 0;
          right: 0;
          z-index: 40;
          background: rgba(255, 255, 255, 0.97);
          backdrop-filter: blur(20px);
          -webkit-backdrop-filter: blur(20px);
          border-top: 1px solid rgba(0, 0, 0, 0.07);
          box-shadow: 0 -4px 24px rgba(0, 0, 0, 0.06);
          padding-bottom: env(safe-area-inset-bottom, 0px);
          height: calc(60px + env(safe-area-inset-bottom, 0px));
          flex-direction: row;
          align-items: stretch;
        }
        @media (max-width: 768px) {
          .cs-bottom-nav {
            display: flex;
          }
        }
        .cs-bottom-nav__item {
          flex: 1;
          display: flex;
          flex-direction: column;
          align-items: center;
          justify-content: center;
          gap: 3px;
          border: none;
          background: none;
          cursor: pointer;
          padding: 8px 4px 6px;
          color: #94a3b8;
          text-decoration: none;
          transition: color 0.18s;
          -webkit-tap-highlight-color: transparent;
          position: relative;
        }
        .cs-bottom-nav__item--active {
          color: #f97316;
        }
        .cs-bottom-nav__item--active::before {
          content: '';
          position: absolute;
          top: 0;
          left: 50%;
          transform: translateX(-50%);
          width: 28px;
          height: 3px;
          border-radius: 0 0 4px 4px;
          background: linear-gradient(90deg, #f97316, #ea580c);
        }
        .cs-bottom-nav__icon {
          width: 22px;
          height: 22px;
          flex-shrink: 0;
          transition: transform 0.18s;
        }
        .cs-bottom-nav__item--active .cs-bottom-nav__icon {
          transform: scale(1.1);
        }
        .cs-bottom-nav__label {
          font-size: 10px;
          font-weight: 600;
          letter-spacing: 0.01em;
          line-height: 1;
          white-space: nowrap;
        }

        /* Check-in FAB (center button) */
        .cs-bottom-nav__fab {
          flex: 1;
          display: flex;
          flex-direction: column;
          align-items: center;
          justify-content: center;
          gap: 3px;
          border: none;
          background: none;
          cursor: pointer;
          padding: 0 4px 4px;
          -webkit-tap-highlight-color: transparent;
          position: relative;
        }
        .cs-bottom-nav__fab-inner {
          width: 52px;
          height: 52px;
          border-radius: 50%;
          background: linear-gradient(135deg, #f97316 0%, #ea580c 100%);
          display: flex;
          align-items: center;
          justify-content: center;
          color: white;
          box-shadow: 0 4px 16px rgba(249, 115, 22, 0.4);
          margin-top: -14px;
          transition: transform 0.18s, box-shadow 0.18s;
          border: 3px solid #fff;
        }
        .cs-bottom-nav__fab:active .cs-bottom-nav__fab-inner {
          transform: scale(0.93);
          box-shadow: 0 2px 8px rgba(249, 115, 22, 0.3);
        }
        .cs-bottom-nav__fab-label {
          font-size: 10px;
          font-weight: 700;
          color: #f97316;
          letter-spacing: 0.01em;
          line-height: 1;
          margin-top: 2px;
        }
      `}</style>

    </>
  );
}
