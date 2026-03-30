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
      to: "/stations",
      label: "Trạm sạc",
      icon: <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" /></svg>,
    },
    {
      to: "/owner/booking-requests",
      label: "Booking",
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
      to: "/owner/booking-requests",
      matchDispute: true,
    },
    {
      icon: "⭐",
      label: "Đánh giá",
      to: "/owner/reviews",
    },
    {
      icon: "🔧",
      label: "Dịch vụ",
      to: "/owner/extra-services",
    },
    {
      icon: "💬",
      label: "Chat",
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
        <div className="cs-mobile-menu__header">
          <ChargeSlotLogo size={30} showText suffix="Owner" />
          <button className="cs-mobile-menu__close" onClick={() => setMobileMenuOpen(false)}>
            <svg width="20" height="20" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {token && (
          <div className="cs-mobile-menu__user">
            <img src={avatarSrc} alt="Avatar" className="cs-mobile-menu__avatar" />
            <div>
              <p className="cs-mobile-menu__phone">{maskPhone(phoneNumber) || "Người dùng"}</p>
              <span className="cs-mobile-menu__role">Chủ trạm</span>
            </div>
          </div>
        )}

        <div className="cs-mobile-menu__nav">
          {[
            ...primaryItems.map(item => ({ ...item, icon: "📦" })),
            { to: "/owner/reviews", icon: "⭐", label: "Đánh giá" },
            { to: "/owner/extra-services", icon: "🔧", label: "Dịch vụ" },
            { to: "/owner/chat-list", icon: "💬", label: "Chat" },
            { to: "/owner/wallet", icon: "💰", label: "Ví tiền" },
          ].map((item, i) => (
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
          ))}
        </div>

        <div className="cs-mobile-menu__footer">
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
          background: rgba(255, 255, 255, 0.92);
          backdrop-filter: blur(16px);
          -webkit-backdrop-filter: blur(16px);
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
        .cs-nav__brand-icon {
          font-size: 22px;
          line-height: 1;
        }
        .cs-nav__brand-text {
          font-size: 18px;
          font-weight: 800;
          letter-spacing: -0.5px;
        }
        .cs-nav__brand-text--owner {
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
        .nav-item--active-owner {
          color: #ea580c;
          font-weight: 600;
          background: #fff7ed;
        }
        .nav-item--active-owner::after {
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
        .nav-item--active-owner .nav-item__icon {
          opacity: 1;
        }
        .nav-item__label {
          line-height: 1;
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
        .cs-btn--login-owner {
          background: linear-gradient(135deg, #f97316 0%, #ea580c 100%) !important;
          color: white !important;
          box-shadow: 0 2px 8px rgba(249, 115, 22, 0.3) !important;
        }
        .cs-btn--login-owner:hover {
          transform: translateY(-1px) !important;
          box-shadow: 0 4px 16px rgba(249, 115, 22, 0.4) !important;
        }
        .cs-btn--register-owner {
          background: white !important;
          color: #ea580c !important;
          border: 1.5px solid #fed7aa !important;
        }
        .cs-btn--register-owner:hover {
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
        }
        .cs-profile-dropdown__header--owner {
          background: linear-gradient(135deg, #f97316 0%, #ea580c 100%);
        }

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
        .cs-hamburger:hover { background: #f3f4f6; color: #ea580c; }

        /* ===== MOBILE OVERLAY ===== */
        .cs-mobile-overlay {
          display: none;
          position: fixed;
          inset: 0;
          background: rgba(0,0,0,0.5);
          z-index: 40;
          opacity: 0;
          transition: opacity 0.3s;
        }
        .cs-mobile-overlay--open { opacity: 1; }

        /* ===== MOBILE MENU ===== */
        .cs-mobile-menu {
          display: none;
          position: fixed;
          top: 0; right: 0;
          width: min(320px, 100vw);
          height: 100vh;
          background: rgba(255,255,255,0.98);
          backdrop-filter: blur(20px);
          z-index: 45;
          flex-direction: column;
          transform: translateX(100%);
          transition: transform 0.35s cubic-bezier(0.16,1,0.3,1);
          box-shadow: -8px 0 40px rgba(0,0,0,0.15);
          overflow-y: auto;
        }
        .cs-mobile-menu--open { transform: translateX(0); }
        .cs-mobile-menu__header {
          display: flex; align-items: center; justify-content: space-between;
          padding: 16px 20px; border-bottom: 1px solid #f1f5f9; flex-shrink: 0;
        }
        .cs-mobile-menu__close {
          width: 36px; height: 36px; border: none; background: #f8fafc;
          border-radius: 10px; display: flex; align-items: center; justify-content: center;
          color: #64748b; cursor: pointer; transition: all 0.2s;
        }
        .cs-mobile-menu__close:hover { background: #fff7ed; color: #ea580c; }
        .cs-mobile-menu__user {
          display: flex; align-items: center; gap: 12px; padding: 16px 20px;
          background: linear-gradient(135deg, #fff7ed, #fed7aa); margin: 12px 16px; border-radius: 16px;
        }
        .cs-mobile-menu__avatar { width: 44px; height: 44px; border-radius: 50%; object-fit: cover; border: 2px solid rgba(249,115,22,0.3); flex-shrink: 0; }
        .cs-mobile-menu__phone { font-size: 14px; font-weight: 600; color: #1e293b; margin: 0 0 2px; }
        .cs-mobile-menu__role { font-size: 12px; color: #ea580c; font-weight: 500; }
        .cs-mobile-menu__nav { flex: 1; padding: 8px 12px; display: flex; flex-direction: column; gap: 2px; }
        .cs-mobile-nav-item {
          display: flex; align-items: center; gap: 12px; padding: 13px 16px;
          border-radius: 12px; font-size: 15px; font-weight: 500; color: #374151;
          text-decoration: none; transition: all 0.15s; border: none; background: none;
          width: 100%; text-align: left; cursor: pointer;
        }
        .cs-mobile-nav-item:hover { background: #fff7ed; color: #ea580c; }
        .cs-mobile-nav-item--active { background: #fff7ed; color: #ea580c; font-weight: 600; }
        .cs-mobile-nav-item__icon { font-size: 18px; width: 28px; text-align: center; flex-shrink: 0; }
        .cs-mobile-nav-item__arrow { margin-left: auto; color: #cbd5e1; flex-shrink: 0; }
        .cs-mobile-menu__footer { padding: 16px; border-top: 1px solid #f1f5f9; display: flex; flex-direction: column; gap: 10px; flex-shrink: 0; }
        .cs-mobile-menu__btn {
          width: 100%; padding: 13px 20px; border-radius: 12px; font-size: 15px;
          font-weight: 600; cursor: pointer; border: none; transition: all 0.2s; text-align: center;
        }
        .cs-mobile-menu__btn--secondary { background: #f8fafc; color: #374151; border: 1.5px solid #e2e8f0; }
        .cs-mobile-menu__btn--secondary:hover { background: #fff7ed; color: #ea580c; border-color: #fed7aa; }
        .cs-mobile-menu__btn--danger { background: #fef2f2; color: #dc2626; border: 1.5px solid #fecaca; }
        .cs-mobile-menu__btn--danger:hover { background: #fee2e2; }

        @media (max-width: 768px) {
          .cs-nav__links { display: none !important; }
          .cs-nav__right .flex.items-center.gap-2 { display: none !important; }
          .cs-hamburger { display: flex; }
          .cs-mobile-overlay { display: block; pointer-events: none; }
          .cs-mobile-overlay--open { pointer-events: auto; }
          .cs-mobile-menu { display: flex; }
        }
      `}</style>
    </>
  );
}
