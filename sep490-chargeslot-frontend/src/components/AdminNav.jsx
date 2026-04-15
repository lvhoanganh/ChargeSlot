import { NavLink, useNavigate, useLocation } from "react-router-dom";
import { useAuthStore } from "@/stores/authStore";
import { useState, useRef, useEffect } from "react";
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

export default function AdminNav() {
  const navigate = useNavigate();
  const location = useLocation();
  const { logout, token, phoneNumber } = useAuthStore();
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const [moreOpen, setMoreOpen] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [mobileMoreOpen, setMobileMoreOpen] = useState(false);
  const dropdownRef = useRef(null);
  const moreRef = useRef(null);
  const mobileMenuRef = useRef(null);

  const storedAvatar = getStoredAvatarDataUrl(phoneNumber) || DEFAULT_AVATAR;
  const avatarSrc = storedAvatar.startsWith("http") || storedAvatar.startsWith("data:")
    ? storedAvatar
    : `https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net${storedAvatar.startsWith("/") ? "" : "/"}${storedAvatar}`;

  // Close dropdowns when clicking outside
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

  // Primary nav items (shown directly)
  const primaryItems = [
    {
      to: "/admin/dashboard",
      label: "Tổng quan",
      icon: <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" /></svg>,
    },
    {
      to: "/admin/manage-users",
      label: "Người dùng",
      icon: <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" /></svg>,
    },
    {
      to: "/admin/view-financial-report",
      label: "Tài chính",
      icon: <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" /></svg>,
    },
    {
      to: "/admin/approve-station",
      label: "Duyệt trạm",
      icon: <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>,
    },
  ];

  // Secondary items (in "Khác" dropdown)
  const moreItems = [
    {
      to: "/admin/manage-kyc",
      label: "Duyệt hồ sơ chủ trạm",
    },
    {
      to: "/admin/bookings",
      label: "Tổng Bookings",
    },
    {
      to: "/admin/invoices",
      label: "Tất cả Hóa đơn",
    },
    {
      to: "/admin/wallets",
      label: "Radar Ví (Vốn)",
    },
    {
      to: "/admin/disputes",
      label: "Tranh chấp",
    },
    {
      to: "/admin/withdraws",
      label: "Duyệt rút tiền",
    },
    {
      to: "/admin/system-config",
      label: "Cấu hình",
    },
  ];

  const moreSubPaths = moreItems.map(i => i.to);
  const isMoreActive = moreSubPaths.some(p => location.pathname.startsWith(p));

  return (
    <>
      <nav className="cs-nav cs-nav--admin">
        <div className="cs-nav__container">
          {/* Brand */}
          <NavLink to="/admin/manage-users" className="cs-nav__brand">
            <ChargeSlotLogo size={34} showText suffix="Admin" />
          </NavLink>

          {/* Primary nav links */}
          <div className="cs-nav__links">
            {primaryItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  `nav-item group ${isActive ? "nav-item--active nav-item--active-admin" : ""}`
                }
              >
                <span className="nav-item__icon">{item.icon}</span>
                <span className="nav-item__label">{item.label}</span>
              </NavLink>
            ))}

            {/* "Khác" dropdown */}
            <div className="relative" ref={moreRef}>
              <button
                onClick={() => setMoreOpen(prev => !prev)}
                className={`nav-item group ${isMoreActive ? "nav-item--active nav-item--active-admin" : ""}`}
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
              <div className={`cs-more-dropdown ${moreOpen ? "cs-more-dropdown--open" : ""}`}>
                <div className="cs-more-dropdown__inner">
                  {moreItems.map((item) => {
                    const isItemActive = location.pathname.startsWith(item.to);
                    return (
                      <button
                        key={item.label}
                        onClick={() => { setMoreOpen(false); navigate(item.to); }}
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
              <button
                onClick={() => navigate("/login")}
                className="cs-btn cs-btn--login cs-btn--login-admin"
              >
                Đăng nhập
              </button>
            )}

            {token && (
              <div className="flex items-center gap-3">
                <NotificationBell />
                <div className="relative" ref={dropdownRef}>
                  <button
                    onClick={() => setDropdownOpen((prev) => !prev)}
                    className="cs-avatar-btn group"
                    aria-label="Menu quản trị"
                  >
                    <img
                      src={avatarSrc}
                      alt="Avatar"
                      className={`cs-avatar-btn__img ${dropdownOpen ? "cs-avatar-btn__img--open" : ""}`}
                    />
                    <svg
                      className={`w-4 h-4 text-gray-400 transition-transform duration-300 ${dropdownOpen ? "rotate-180" : ""}`}
                      fill="none" stroke="currentColor" viewBox="0 0 24 24"
                    >
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                    </svg>
                  </button>

                  <div className={`cs-profile-dropdown ${dropdownOpen ? "cs-profile-dropdown--open" : ""}`}>
                    <div className="cs-profile-dropdown__card">
                      {/* Header */}
                      <div className="cs-profile-dropdown__header cs-profile-dropdown__header--admin">
                        <img
                          src={avatarSrc}
                          alt="Avatar"
                          className="w-12 h-12 rounded-full object-cover border-2 border-white/60 shadow-md"
                        />
                        <div className="min-w-0 flex-1">
                          <p className="text-white font-semibold text-sm truncate">
                            {maskPhone(phoneNumber) || "Quản trị viên"}
                          </p>
                          <span className="inline-block mt-0.5 px-2 py-0.5 text-xs font-medium rounded-full bg-white/20 text-white">
                            Quản trị viên
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
                          <span className="text-gray-600">Quản trị viên</span>
                        </div>
                      </div>

                      {/* Profile */}
                      <div className="py-1">
                        <button
                          onClick={() => { setDropdownOpen(false); navigate("/admin/admin-profile"); }}
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
                          onClick={() => { setDropdownOpen(false); logout(); navigate("/login"); }}
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
        </div>
      </nav>

      {/* ===== ADMIN BOTTOM NAVIGATION BAR (mobile only) ===== */}
      <nav className="cs-admin-bottom-nav" aria-label="Điều hướng Admin">
        <NavLink to="/admin/dashboard" className={({ isActive }) => `cs-admin-bnav__item ${isActive ? "cs-admin-bnav__item--active" : ""}`}>
          <svg className="cs-admin-bnav__icon" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
          </svg>
          <span className="cs-admin-bnav__label">Tổng quan</span>
        </NavLink>

        <NavLink to="/admin/manage-users" className={({ isActive }) => `cs-admin-bnav__item ${isActive ? "cs-admin-bnav__item--active" : ""}`}>
          <svg className="cs-admin-bnav__icon" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
          <span className="cs-admin-bnav__label">Người dùng</span>
        </NavLink>

        {/* Duyệt trạm — FAB ở giữa */}
        <NavLink to="/admin/approve-station" className={({ isActive }) => `cs-admin-bnav__fab-wrap ${isActive ? "active" : ""}`}>
          <div className="cs-admin-bnav__fab">
            <svg width="24" height="24" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <span className="cs-admin-bnav__fab-label">Duyệt</span>
        </NavLink>

        <NavLink to="/admin/view-financial-report" className={({ isActive }) => `cs-admin-bnav__item ${isActive ? "cs-admin-bnav__item--active" : ""}`}>
          <svg className="cs-admin-bnav__icon" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z" />
          </svg>
          <span className="cs-admin-bnav__label">Tài chính</span>
        </NavLink>

        <button
          className={`cs-admin-bnav__item ${location.pathname.startsWith("/admin/admin-profile") ||
            location.pathname.startsWith("/admin/disputes") ||
            location.pathname.startsWith("/admin/system-config") ||
            location.pathname.startsWith("/admin/manage-kyc") ||
            location.pathname.startsWith("/admin/withdraws") ||
            location.pathname.startsWith("/admin/bookings") ||
            location.pathname.startsWith("/admin/invoices") ||
            location.pathname.startsWith("/admin/wallets") ||
            mobileMoreOpen ? "cs-admin-bnav__item--active" : ""
            }`}
          onClick={() => setMobileMoreOpen(true)}
        >
          <img
            src={avatarSrc}
            alt="Avatar"
            style={{
              width: 24, height: 24, borderRadius: "50%", objectFit: "cover",
              border: mobileMoreOpen ? "2px solid #f97316" : "2px solid #e5e7eb"
            }}
          />
          <span className="cs-admin-bnav__label">Tôi</span>
        </button>
      </nav>

      {/* ===== ADMIN MOBILE MORE BOTTOM SHEET ===== */}
      {mobileMoreOpen && (
        <div className="cs-more-sheet-overlay" onClick={() => setMobileMoreOpen(false)} />
      )}
      <div className={`cs-more-sheet ${mobileMoreOpen ? "cs-more-sheet--open" : ""}`}>
        <div style={{ display: "flex", justifyContent: "center", padding: "12px 0 8px" }}>
          <div style={{ width: 40, height: 4, borderRadius: 2, background: "#e2e8f0" }} />
        </div>
        <div className="cs-more-sheet__user">
          <img src={avatarSrc} alt="Avatar" className="cs-more-sheet__avatar" />
          <div>
            <p className="cs-more-sheet__phone">{maskPhone(phoneNumber) || "Quản trị viên"}</p>
            <span className="cs-more-sheet__role">Quản trị viên</span>
          </div>
        </div>
        <div className="cs-more-sheet__grid">
          {[
            { label: "Hồ sơ", to: "/admin/admin-profile", emoji: "👤" },
            { label: "Duyệt hồ sơ chủ trạm", to: "/admin/manage-kyc", emoji: "🆔" },
            { label: "Tranh chấp", to: "/admin/disputes", emoji: "⚖️" },
            { label: "Rút tiền", to: "/admin/withdraws", emoji: "💸" },
            { label: "Cấu hình", to: "/admin/system-config", emoji: "⚙️" },
            { label: "Bookings", to: "/admin/bookings", emoji: "📊" },
            { label: "Hóa đơn", to: "/admin/invoices", emoji: "📝" },
            { label: "Ví & Vốn", to: "/admin/wallets", emoji: "💰" },
          ].map((item) => (
            <button
              key={item.to}
              className="cs-more-sheet__item"
              onClick={() => { setMobileMoreOpen(false); navigate(item.to); }}
            >
              <span className="cs-more-sheet__item-emoji">{item.emoji}</span>
              <span className="cs-more-sheet__item-label">{item.label}</span>
            </button>
          ))}
        </div>
        <div className="cs-more-sheet__footer">
          <button
            className="cs-more-sheet__logout-btn"
            onClick={() => { setMobileMoreOpen(false); logout(); navigate("/login"); }}
          >
            🚪 Đăng xuất
          </button>
        </div>
      </div>

      <style>{`
        .cs-nav {
          position: fixed; top: 0; left: 0; width: 100%; height: 64px;
          background: rgba(255,255,255,0.92); backdrop-filter: blur(16px);
          -webkit-backdrop-filter: blur(16px); border-bottom: 1px solid rgba(0,0,0,0.06);
          z-index: 30; display: flex; align-items: center; box-shadow: 0 1px 3px rgba(0,0,0,0.04);
        }
        .cs-nav--admin { background: rgba(248,250,252,0.95); }
        .cs-nav__container {
          max-width: 1400px; width: 95%; margin: 0 auto;
          display: flex; align-items: center; justify-content: space-between; gap: 16px;
        }
        .cs-nav__brand { display: flex; align-items: center; gap: 8px; text-decoration: none; flex-shrink: 0; }
        .cs-nav__brand-icon { font-size: 22px; line-height: 1; }
        .cs-nav__brand-text { font-size: 18px; font-weight: 800; letter-spacing: -0.5px; }
        .cs-nav__brand-text--admin {
          background: linear-gradient(135deg, #f97316 0%, #ea580c 100%);
          -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text;
        }
        .cs-nav__links { display: flex; align-items: center; gap: 2px; }
        .nav-item {
          display: flex; align-items: center; gap: 6px; padding: 8px 14px;
          border-radius: 10px; font-size: 14px; font-weight: 500; color: #4b5563;
          text-decoration: none; transition: all 0.2s ease; cursor: pointer;
          border: none; background: none; white-space: nowrap; position: relative;
        }
        .nav-item:hover { background: #fff7ed; color: #ea580c; }
        .nav-item--active-admin { color: #ea580c; font-weight: 600; background: #fff7ed; }
        .nav-item--active-admin::after {
          content: ''; position: absolute; bottom: 0; left: 50%; transform: translateX(-50%);
          width: 20px; height: 3px; border-radius: 3px; background: linear-gradient(90deg, #f97316, #ea580c);
        }
        .nav-item__icon { display: flex; align-items: center; opacity: 0.7; transition: opacity 0.2s; }
        .nav-item:hover .nav-item__icon, .nav-item--active-admin .nav-item__icon { opacity: 1; }
        .nav-item__label { line-height: 1; }
        .cs-more-dropdown {
          position: absolute; right: 0; top: 100%; margin-top: 8px; width: 200px;
          opacity: 0; transform: scale(0.95) translateY(-8px); pointer-events: none;
          transition: all 0.25s cubic-bezier(0.16,1,0.3,1); transform-origin: top right; z-index: 50;
        }
        .cs-more-dropdown--open { opacity: 1; transform: scale(1) translateY(0); pointer-events: auto; }
        .cs-more-dropdown__inner {
          background: rgba(255,255,255,0.97); backdrop-filter: blur(20px);
          border: 1px solid rgba(0,0,0,0.06); border-radius: 14px; padding: 6px;
          box-shadow: 0 12px 40px rgba(0,0,0,0.1), 0 2px 8px rgba(0,0,0,0.04);
        }
        .cs-more-dropdown__item {
          display: flex; align-items: center; gap: 10px; width: 100%; padding: 10px 14px;
          border: none; background: none; border-radius: 10px; font-size: 14px; font-weight: 500;
          color: #4b5563; cursor: pointer; transition: all 0.15s ease; text-align: left;
        }
        .cs-more-dropdown__item:hover { background: #fff7ed; color: #ea580c; }
        .cs-more-dropdown__item--active { background: #fff7ed; color: #ea580c; font-weight: 600; }
        .cs-more-dropdown__item-icon { font-size: 16px; flex-shrink: 0; }
        .cs-nav__right { display: flex; align-items: center; flex-shrink: 0; }
        .cs-btn--login { border-radius: 10px; font-weight: 600; font-size: 13px; padding: 8px 18px; cursor: pointer; transition: all 0.2s ease; border: none; color: white; }
        .cs-btn--login-admin { background: linear-gradient(135deg, #f97316 0%, #ea580c 100%); box-shadow: 0 2px 8px rgba(249,115,22,0.3); }
        .cs-btn--login-admin:hover { transform: translateY(-1px); box-shadow: 0 4px 16px rgba(249,115,22,0.4); }
        .cs-avatar-btn { display: flex; align-items: center; gap: 6px; cursor: pointer; border: none; background: none; padding: 4px; border-radius: 50px; transition: background 0.2s; }
        .cs-avatar-btn:hover { background: #f3f4f6; }
        .cs-avatar-btn__img { width: 36px; height: 36px; border-radius: 50%; object-fit: cover; border: 2px solid #e5e7eb; transition: all 0.3s; }
        .cs-avatar-btn__img--open, .cs-avatar-btn:hover .cs-avatar-btn__img { border-color: #f97316; box-shadow: 0 0 0 3px rgba(249,115,22,0.15); }
        .cs-profile-dropdown {
          position: absolute; right: 0; top: 100%; margin-top: 10px; width: 280px;
          opacity: 0; transform: scale(0.95) translateY(-8px); pointer-events: none;
          transition: all 0.25s cubic-bezier(0.16,1,0.3,1); transform-origin: top right; z-index: 50;
        }
        .cs-profile-dropdown--open { opacity: 1; transform: scale(1) translateY(0); pointer-events: auto; }
        .cs-profile-dropdown__card {
          background: rgba(255,255,255,0.97); backdrop-filter: blur(20px);
          border: 1px solid rgba(0,0,0,0.06); border-radius: 16px; overflow: hidden;
          box-shadow: 0 16px 48px rgba(0,0,0,0.12), 0 2px 8px rgba(0,0,0,0.04);
        }
        .cs-profile-dropdown__header { padding: 16px 20px; display: flex; align-items: center; gap: 12px; }
        .cs-profile-dropdown__header--admin { background: linear-gradient(135deg, #f97316 0%, #ea580c 100%); }

        /* Hamburger + drawer — ẩn (dùng bottom nav) */
        .cs-hamburger { display: none !important; }
        .cs-mobile-overlay { display: none !important; }
        .cs-mobile-menu { display: none !important; }

        /* ===== ADMIN BOTTOM NAV ===== */
        .cs-admin-bottom-nav {
          display: none;
          position: fixed; bottom: 0; left: 0; right: 0; z-index: 40;
          background: rgba(248,250,252,0.97);
          backdrop-filter: blur(20px); -webkit-backdrop-filter: blur(20px);
          border-top: 1px solid rgba(0,0,0,0.07);
          box-shadow: 0 -4px 24px rgba(0,0,0,0.06);
          padding-bottom: env(safe-area-inset-bottom, 0px);
          height: calc(60px + env(safe-area-inset-bottom, 0px));
          flex-direction: row; align-items: stretch;
        }
        .cs-admin-bnav__item {
          flex: 1; display: flex; flex-direction: column; align-items: center;
          justify-content: center; gap: 3px; border: none; background: none;
          cursor: pointer; padding: 8px 4px 6px; color: #94a3b8;
          text-decoration: none; transition: color 0.18s;
          -webkit-tap-highlight-color: transparent; position: relative;
        }
        .cs-admin-bnav__item--active { color: #f97316; }
        .cs-admin-bnav__item--active::before {
          content: ''; position: absolute; top: 0; left: 50%; transform: translateX(-50%);
          width: 28px; height: 3px; border-radius: 0 0 4px 4px;
          background: linear-gradient(90deg, #f97316, #ea580c);
        }
        .cs-admin-bnav__icon { width: 22px; height: 22px; flex-shrink: 0; }
        .cs-admin-bnav__label { font-size: 10px; font-weight: 600; white-space: nowrap; }
        .cs-admin-bnav__fab-wrap {
          flex: 1; display: flex; flex-direction: column; align-items: center;
          justify-content: center; gap: 3px; text-decoration: none;
          padding: 0 4px 4px; -webkit-tap-highlight-color: transparent;
        }
        .cs-admin-bnav__fab {
          width: 50px; height: 50px; border-radius: 50%;
          background: linear-gradient(135deg, #f97316, #ea580c);
          display: flex; align-items: center; justify-content: center;
          color: white; margin-top: -12px;
          box-shadow: 0 4px 16px rgba(249,115,22,0.4);
          border: 3px solid rgba(248,250,252,0.97);
        }
        .cs-admin-bnav__fab-label { font-size: 10px; font-weight: 700; color: #f97316; }

        @media (max-width: 768px) {
          .cs-nav { height: 52px; }
          .cs-nav__links { display: none !important; }
          .cs-nav__right .flex.items-center.gap-2 { display: none !important; }
          .cs-nav__right { display: flex !important; }
          .cs-avatar-btn > svg.w-4 { display: none !important; }
          .cs-admin-bottom-nav { display: flex; }
        }
        @media (min-width: 769px) {
          .cs-admin-bottom-nav { display: none !important; }
          .cs-more-sheet { display: none !important; }
          .cs-more-sheet-overlay { display: none !important; }
        }

        /* ===== MOBILE MORE SHEET (Admin) ===== */
        .cs-more-sheet-overlay {
          position: fixed; inset: 0; background: rgba(0,0,0,0.45);
          z-index: 45; backdrop-filter: blur(2px);
          animation: adminFadeIn 0.2s ease;
        }
        @keyframes adminFadeIn { from { opacity: 0; } to { opacity: 1; } }
        .cs-more-sheet {
          position: fixed; bottom: 0; left: 0; right: 0; z-index: 46;
          background: #ffffff; border-radius: 20px 20px 0 0;
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
          background: linear-gradient(135deg, #fff7ed, #ffedd5); border-radius: 14px;
        }
        .cs-more-sheet__avatar { width: 48px; height: 48px; border-radius: 50%; object-fit: cover; border: 2px solid rgba(249,115,22,0.3); flex-shrink: 0; }
        .cs-more-sheet__phone { font-size: 14px; font-weight: 700; color: #1e293b; margin: 0 0 2px; }
        .cs-more-sheet__role { font-size: 12px; color: #ea580c; font-weight: 500; }
        .cs-more-sheet__grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; padding: 0 16px 8px; }
        .cs-more-sheet__item {
          display: flex; flex-direction: column; align-items: center; gap: 8px;
          padding: 16px 8px; border: none; background: #f8fafc;
          border-radius: 14px; cursor: pointer; transition: all 0.15s;
          -webkit-tap-highlight-color: transparent;
        }
        .cs-more-sheet__item:active { transform: scale(0.94); background: #fff7ed; }
        .cs-more-sheet__item-emoji { font-size: 26px; line-height: 1; }
        .cs-more-sheet__item-label { font-size: 12px; font-weight: 600; color: #374151; text-align: center; }
        .cs-more-sheet__footer { padding: 12px 16px 4px; border-top: 1px solid #f1f5f9; }
        .cs-more-sheet__logout-btn {
          width: 100%; padding: 14px 20px; border-radius: 14px;
          font-size: 15px; font-weight: 600; cursor: pointer;
          background: #fef2f2; color: #dc2626; border: 1.5px solid #fecaca;
          transition: all 0.2s;
        }
        .cs-more-sheet__logout-btn:active { background: #fee2e2; }
      `}</style>
    </>
  );
}
