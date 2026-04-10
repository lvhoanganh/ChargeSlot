import { NavLink, useNavigate, useLocation } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { useAuthStore } from "@/stores/authStore";
import { useState, useRef, useEffect } from "react";
import NotificationBell from "@/components/NotificationBell";
import { walletApi } from "@/services/api";
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

export default function OwnerNav() {
  const { logout, phoneNumber, role, token } = useAuthStore();
  const navigate = useNavigate();
  const location = useLocation();
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const [moreOpen, setMoreOpen] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [mobileMoreOpen, setMobileMoreOpen] = useState(false);
  const dropdownRef = useRef(null);
  const moreRef = useRef(null);
  const mobileMenuRef = useRef(null);
  const [walletBalance, setWalletBalance] = useState(null);

  const normalizedRole = (role || "").toLowerCase();
  const avatarSrc = getStoredAvatarDataUrl(phoneNumber) || DEFAULT_AVATAR;
  const profilePath = "/owner/owner-profile";

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

  // Fetch wallet balance
  useEffect(() => {
    if (token) {
      walletApi.getWallet()
        .then(w => setWalletBalance(w?.availableBalance ?? null))
        .catch(() => setWalletBalance(null));
    }
  }, [token]);

  // Primary nav items (always shown)
  const primaryItems = [
    {
      to: "/owner/dashboard",
      label: "Tổng quan",
      icon: <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" /></svg>,
    },
    {
      to: "/stations",
      label: "Trạm sạc",
      icon: <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" /></svg>,
    },
    {
      to: "/owner/booking-requests",
      label: "Lịch đặt",
      icon: <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" /></svg>,
    },
    {
      to: "/owner/active-sessions",
      label: "Phiên sạc",
      icon: <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" /></svg>,
    },
  ];

  // Secondary items (in "Khác" dropdown)
  const moreItems = [
    {
      icon: "⚠️",
      label: "Khiếu nại",
      to: "/owner/disputes",
      matchDispute: true,
    },
    {
      icon: "⭐",
      label: "Đánh giá",
      to: "/owner/reviews",
    },
    {
      icon: "🔧",
      label: "Dịch vụ thêm",
      to: "/owner/extra-services",
    },
    {
      icon: "💬",
      label: "Nhắn tin",
      to: "/owner/chat-list",
    },
  ];

  const moreSubPaths = ["/owner/reviews", "/owner/extra-services", "/owner/chat-list"];
  const isMoreActive = moreSubPaths.some(p => location.pathname.startsWith(p))
    || location.pathname.includes("/dispute");

  return (
    <>
      <nav className="cs-nav cs-nav--owner">
        <div className="cs-nav__container">
          {/* Brand */}
          <NavLink to="/stations" className="cs-nav__brand">
            <ChargeSlotLogo size={34} showText suffix="Owner" />
          </NavLink>

          {/* Primary nav links */}
          <div className="cs-nav__links">
            {primaryItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  `nav-item group ${isActive ? "nav-item--active nav-item--active-owner" : ""}`
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
                className={`nav-item group ${isMoreActive ? "nav-item--active nav-item--active-owner" : ""}`}
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
                    const isItemActive = item.matchDispute
                      ? location.pathname.includes("/dispute")
                      : location.pathname.startsWith(item.to);
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
              <div className="flex items-center gap-2">
                <Button
                  className="cs-btn cs-btn--login cs-btn--login-owner"
                  onClick={() => navigate("/login")}
                >
                  Đăng nhập
                </Button>
                <Button
                  className="cs-btn cs-btn--register-owner"
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
                      fill="none" stroke="currentColor" viewBox="0 0 24 24"
                    >
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                    </svg>
                  </button>

                  <div className={`cs-profile-dropdown ${dropdownOpen ? "cs-profile-dropdown--open" : ""}`}>
                    <div className="cs-profile-dropdown__card">
                      {/* Header */}
                      <div className="cs-profile-dropdown__header cs-profile-dropdown__header--owner">
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

                      {/* Wallet balance */}
                      {walletBalance !== null && (
                        <div className="px-5 py-2.5 border-b border-gray-100">
                          <button
                            onClick={() => { setDropdownOpen(false); navigate("/owner/wallet"); }}
                            className="w-full flex items-center gap-3 text-sm text-gray-700 hover:bg-orange-50 hover:text-orange-600 transition-colors rounded-lg px-0 py-1 cursor-pointer"
                            style={{ background: "none", border: "none" }}
                          >
                            <svg className="w-4 h-4 text-orange-500 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z" />
                            </svg>
                            <span>Ví tiền</span>
                            <span className="ml-auto font-bold text-orange-600">{walletBalance.toLocaleString("vi-VN")}đ</span>
                          </button>
                        </div>
                      )}

                      {/* Profile */}
                      <div className="py-1">
                        <button
                          onClick={() => { setDropdownOpen(false); navigate(profilePath); }}
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

          {/* Hamburger button — ẩn trên mobile (dùng bottom nav) */}
        </div>
      </nav>

      {/* ===== OWNER BOTTOM NAVIGATION BAR (mobile only) ===== */}
      <nav className="cs-owner-bottom-nav" aria-label="Điều hướng Owner">
        {/* Dashboard */}
        <NavLink to="/owner/dashboard" className={({ isActive }) => `cs-owner-bnav__item ${isActive ? "cs-owner-bnav__item--active" : ""}`}>
          <svg className="cs-owner-bnav__icon" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
          </svg>
          <span className="cs-owner-bnav__label">Tổng quan</span>
        </NavLink>

        {/* Trạm sạc */}
        <NavLink to="/stations" className={({ isActive }) => `cs-owner-bnav__item ${isActive ? "cs-owner-bnav__item--active" : ""}`}>
          <svg className="cs-owner-bnav__icon" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
          </svg>
          <span className="cs-owner-bnav__label">Trạm sạc</span>
        </NavLink>

        {/* Booking — FAB ở giữa */}
        <NavLink to="/owner/booking-requests" className={({ isActive }) => `cs-owner-bnav__fab-wrap ${isActive ? "active" : ""}`}>
          <div className="cs-owner-bnav__fab">
            <svg width="24" height="24" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
            </svg>
          </div>
          <span className="cs-owner-bnav__fab-label">Lịch đặt</span>
        </NavLink>

        {/* Phiên sạc */}
        <NavLink to="/owner/active-sessions" className={({ isActive }) => `cs-owner-bnav__item ${isActive ? "cs-owner-bnav__item--active" : ""}`}>
          <svg className="cs-owner-bnav__icon" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
          </svg>
          <span className="cs-owner-bnav__label">Phiên sạc</span>
        </NavLink>

        {/* Tôi — mở bottom sheet */}
        <button
          className={`cs-owner-bnav__item ${location.pathname.startsWith("/owner/owner-profile") ||
              location.pathname.startsWith("/owner/wallet") ||
              location.pathname.startsWith("/owner/reviews") ||
              mobileMoreOpen ? "cs-owner-bnav__item--active" : ""
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
          <span className="cs-owner-bnav__label">Tôi</span>
        </button>
      </nav>

      {/* ===== OWNER MOBILE MORE BOTTOM SHEET ===== */}
      {mobileMoreOpen && (
        <div className="cs-more-sheet-overlay" onClick={() => setMobileMoreOpen(false)} />
      )}
      <div className={`cs-more-sheet ${mobileMoreOpen ? "cs-more-sheet--open" : ""}`}>
        <div style={{ display: "flex", justifyContent: "center", padding: "12px 0 8px" }}>
          <div style={{ width: 40, height: 4, borderRadius: 2, background: "#e2e8f0" }} />
        </div>
        {token && (
          <div className="cs-more-sheet__user">
            <img src={avatarSrc} alt="Avatar" className="cs-more-sheet__avatar" />
            <div>
              <p className="cs-more-sheet__phone">{maskPhone(phoneNumber) || "Chủ trạm"}</p>
              <span className="cs-more-sheet__role">Chủ trạm</span>
            </div>
          </div>
        )}
        <div className="cs-more-sheet__grid">
          {[
            { emoji: "👤", label: "Hồ sơ", to: "/owner/owner-profile" },
            { emoji: "💳", label: "Ví tiền", to: "/owner/wallet" },
            { emoji: "📊", label: "Doanh thu", to: "/owner/revenue" },
            { emoji: "💬", label: "Nhắn tin", to: "/owner/chat-list" },
            { emoji: "⭐", label: "Đánh giá", to: "/owner/reviews" },
            { emoji: "⚠️", label: "Khiếu nại", to: "/owner/disputes" },
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
        /* ===== OWNER NAVBAR CORE ===== */
        .cs-nav {
          position: fixed;
          top: 0;
          left: 0;
          width: 100%;
          height: 64px;
          background: rgba(255, 255, 255, 0.95);
          backdrop-filter: blur(8px);
          -webkit-backdrop-filter: blur(8px);
          border-bottom: 1px solid rgba(0, 0, 0, 0.06);
          z-index: 30;
          display: flex;
          align-items: center;
          box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
        }
        .cs-nav--owner {
          background: rgba(255, 251, 245, 0.95);
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
        .cs-nav__brand-icon { font-size: 22px; line-height: 1; }
        .cs-nav__brand-text { font-size: 18px; font-weight: 800; letter-spacing: -0.5px; }
        .cs-nav__brand-text--owner {
          background: linear-gradient(135deg, #f97316 0%, #ea580c 100%);
          -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text;
        }

        /* ===== NAV LINKS ===== */
        .cs-nav__links { display: flex; align-items: center; gap: 2px; }

        /* ===== NAV ITEM ===== */
        .nav-item {
          display: flex; align-items: center; gap: 6px; padding: 8px 14px;
          border-radius: 10px; font-size: 14px; font-weight: 500; color: #4b5563;
          text-decoration: none; transition: all 0.2s ease; cursor: pointer;
          border: none; background: none; white-space: nowrap; position: relative;
        }
        .nav-item:hover { background: #fff7ed; color: #ea580c; }
        .nav-item--active-owner { color: #ea580c; font-weight: 600; background: #fff7ed; }
        .nav-item--active-owner::after {
          content: ''; position: absolute; bottom: 0; left: 50%;
          transform: translateX(-50%); width: 20px; height: 3px;
          border-radius: 3px; background: linear-gradient(90deg, #f97316, #ea580c);
        }
        .nav-item__icon { display: flex; align-items: center; opacity: 0.7; transition: opacity 0.2s; }
        .nav-item:hover .nav-item__icon, .nav-item--active-owner .nav-item__icon { opacity: 1; }
        .nav-item__label { line-height: 1; }

        /* ===== MORE DROPDOWN ===== */
        .cs-more-dropdown {
          position: absolute; right: 0; top: 100%; margin-top: 8px; width: 200px;
          opacity: 0; transform: scale(0.95) translateY(-8px); pointer-events: none;
          transition: all 0.25s cubic-bezier(0.16, 1, 0.3, 1);
          transform-origin: top right; z-index: 50;
        }
        .cs-more-dropdown--open { opacity: 1; transform: scale(1) translateY(0); pointer-events: auto; }
        .cs-more-dropdown__inner {
          background: rgba(255,255,255,0.98); backdrop-filter: blur(8px);
          -webkit-backdrop-filter: blur(8px); border: 1px solid rgba(0,0,0,0.06);
          border-radius: 14px; padding: 6px;
          box-shadow: 0 12px 40px rgba(0,0,0,0.1), 0 2px 8px rgba(0,0,0,0.04);
        }
        .cs-more-dropdown__item {
          display: flex; align-items: center; gap: 10px; width: 100%;
          padding: 10px 14px; border: none; background: none; border-radius: 10px;
          font-size: 14px; font-weight: 500; color: #4b5563; cursor: pointer;
          transition: all 0.15s ease; text-align: left;
        }
        .cs-more-dropdown__item:hover { background: #fff7ed; color: #ea580c; }
        .cs-more-dropdown__item--active { background: #fff7ed; color: #ea580c; font-weight: 600; }
        .cs-more-dropdown__item-icon { font-size: 16px; flex-shrink: 0; }

        /* ===== RIGHT SECTION ===== */
        .cs-nav__right { display: flex; align-items: center; flex-shrink: 0; }

        /* ===== AUTH BUTTONS ===== */
        .cs-btn { border-radius: 10px !important; font-weight: 600 !important; font-size: 13px !important; padding: 8px 18px !important; cursor: pointer !important; transition: all 0.2s ease !important; border: none !important; }
        .cs-btn--login-owner { background: linear-gradient(135deg, #f97316 0%, #ea580c 100%) !important; color: white !important; box-shadow: 0 2px 8px rgba(249,115,22,0.3) !important; }
        .cs-btn--login-owner:hover { transform: translateY(-1px) !important; box-shadow: 0 4px 16px rgba(249,115,22,0.4) !important; }
        .cs-btn--register-owner { background: white !important; color: #ea580c !important; border: 1.5px solid #fed7aa !important; }
        .cs-btn--register-owner:hover { background: #fff7ed !important; border-color: #f97316 !important; }

        /* ===== AVATAR BUTTON ===== */
        .cs-avatar-btn { display: flex; align-items: center; gap: 6px; cursor: pointer; border: none; background: none; padding: 4px; border-radius: 50px; transition: background 0.2s; }
        .cs-avatar-btn:hover { background: #f3f4f6; }
        .cs-avatar-btn__img { width: 36px; height: 36px; border-radius: 50%; object-fit: cover; border: 2px solid #e5e7eb; transition: all 0.3s; }
        .cs-avatar-btn__img--open, .cs-avatar-btn:hover .cs-avatar-btn__img { border-color: #f97316; box-shadow: 0 0 0 3px rgba(249,115,22,0.15); }

        /* ===== PROFILE DROPDOWN ===== */
        .cs-profile-dropdown {
          position: absolute; right: 0; top: 100%; margin-top: 10px; width: 280px;
          opacity: 0; transform: scale(0.95) translateY(-8px); pointer-events: none;
          transition: all 0.25s cubic-bezier(0.16,1,0.3,1); transform-origin: top right; z-index: 50;
        }
        .cs-profile-dropdown--open { opacity: 1; transform: scale(1) translateY(0); pointer-events: auto; }
        .cs-profile-dropdown__card {
          background: rgba(255,255,255,0.98); backdrop-filter: blur(8px);
          -webkit-backdrop-filter: blur(8px); border: 1px solid rgba(0,0,0,0.06);
          border-radius: 16px; overflow: hidden;
          box-shadow: 0 16px 48px rgba(0,0,0,0.12), 0 2px 8px rgba(0,0,0,0.04);
        }
        .cs-profile-dropdown__header { padding: 16px 20px; display: flex; align-items: center; gap: 12px; }
        .cs-profile-dropdown__header--owner { background: linear-gradient(135deg, #f97316 0%, #ea580c 100%); }

        /* ===== HAMBURGER (hidden — replaced by bottom nav) ===== */
        .cs-hamburger { display: none !important; }
        .cs-mobile-overlay { display: none !important; }
        .cs-mobile-menu { display: none !important; }

        /* ===== OWNER BOTTOM NAV ===== */
        .cs-owner-bottom-nav {
          display: none;
          position: fixed; bottom: 0; left: 0; right: 0; z-index: 40;
          background: rgba(255,251,245,0.97);
          backdrop-filter: blur(8px); -webkit-backdrop-filter: blur(8px);
          border-top: 1px solid rgba(249,115,22,0.12);
          box-shadow: 0 -4px 24px rgba(0,0,0,0.06);
          padding-bottom: env(safe-area-inset-bottom, 0px);
          height: calc(60px + env(safe-area-inset-bottom, 0px));
          flex-direction: row; align-items: stretch;
        }
        .cs-owner-bnav__item {
          flex: 1; display: flex; flex-direction: column; align-items: center;
          justify-content: center; gap: 3px; border: none; background: none;
          cursor: pointer; padding: 8px 4px 6px; color: #94a3b8;
          text-decoration: none; transition: color 0.18s;
          -webkit-tap-highlight-color: transparent; position: relative;
        }
        .cs-owner-bnav__item--active { color: #f97316; }
        .cs-owner-bnav__item--active::before {
          content: ''; position: absolute; top: 0; left: 50%; transform: translateX(-50%);
          width: 28px; height: 3px; border-radius: 0 0 4px 4px;
          background: linear-gradient(90deg, #f97316, #ea580c);
        }
        .cs-owner-bnav__icon { width: 22px; height: 22px; flex-shrink: 0; }
        .cs-owner-bnav__label { font-size: 10px; font-weight: 600; white-space: nowrap; }
        .cs-owner-bnav__fab-wrap {
          flex: 1; display: flex; flex-direction: column; align-items: center;
          justify-content: center; gap: 3px; text-decoration: none;
          padding: 0 4px 4px; -webkit-tap-highlight-color: transparent;
        }
        .cs-owner-bnav__fab {
          width: 50px; height: 50px; border-radius: 50%;
          background: linear-gradient(135deg, #f97316, #ea580c);
          display: flex; align-items: center; justify-content: center;
          color: white; margin-top: -12px;
          box-shadow: 0 4px 16px rgba(249,115,22,0.4);
          border: 3px solid rgba(255,251,245,0.97);
          transition: transform .18s;
        }
        .cs-owner-bnav__fab-wrap.active .cs-owner-bnav__fab { box-shadow: 0 4px 20px rgba(249,115,22,0.5); }
        .cs-owner-bnav__fab-label { font-size: 10px; font-weight: 700; color: #f97316; }

        /* ===== RESPONSIVE ===== */
        @media (max-width: 768px) {
          .cs-nav { height: 52px; }
          .cs-nav__links { display: none !important; }
          .cs-nav__right .flex.items-center.gap-2 { display: none !important; }
          .cs-nav__right { display: flex !important; }
          .cs-avatar-btn > svg.w-4 { display: none !important; }
          .cs-owner-bottom-nav { display: flex; }
        }
        @media (min-width: 769px) {
          .cs-owner-bottom-nav { display: none !important; }
          .cs-more-sheet { display: none !important; }
          .cs-more-sheet-overlay { display: none !important; }
        }

        /* ===== MOBILE MORE SHEET (Owner) ===== */
        .cs-more-sheet-overlay {
          position: fixed; inset: 0; background: rgba(0,0,0,0.45);
          z-index: 45; backdrop-filter: blur(2px);
          animation: ownerFadeIn 0.2s ease;
        }
        @keyframes ownerFadeIn { from { opacity: 0; } to { opacity: 1; } }
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
          font-size: 15px; font-weight: 600; cursor: pointer; border: none;
          background: #fef2f2; color: #dc2626; border: 1.5px solid #fecaca;
          transition: all 0.2s;
        }
        .cs-more-sheet__logout-btn:active { background: #fee2e2; }
      `}</style>
    </>
  );
}
