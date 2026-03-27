import { NavLink, useNavigate, useLocation } from "react-router-dom";
import { useAuthStore } from "@/stores/authStore";
import { useState, useRef, useEffect } from "react";
import NotificationBell from "@/components/NotificationBell";
import ChargeSlotLogo from "@/components/ChargeSlotLogo";


const DEFAULT_AVATAR =
  "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'%3E%3Ccircle cx='50' cy='50' r='50' fill='%23f97316'/%3E%3Ccircle cx='50' cy='38' r='16' fill='%23fff'/%3E%3Cellipse cx='50' cy='75' rx='28' ry='20' fill='%23fff'/%3E%3C/svg%3E";

const maskPhone = (phone) =>
  phone ? `**** **** ${phone.slice(-2)}` : "";

export default function AdminNav() {
  const navigate = useNavigate();
  const location = useLocation();
  const { logout, token, phoneNumber } = useAuthStore();
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const [moreOpen, setMoreOpen] = useState(false);
  const dropdownRef = useRef(null);
  const moreRef = useRef(null);

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

  // Primary nav items (shown directly)
  const primaryItems = [
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
      to: "/admin/disputes",
      label: "Tranh chấp",
      icon: "⚠️",
    },
    {
      to: "/admin/system-config",
      label: "Cấu hình",
      icon: "⚙️",
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
                      src={DEFAULT_AVATAR}
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
                          src={DEFAULT_AVATAR}
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

      <style>{`
        /* ===== ADMIN NAVBAR CORE ===== */
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
        .cs-nav--admin {
          background: rgba(248, 250, 252, 0.95);
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
        .cs-nav__brand-text--admin {
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
        .nav-item--active-admin {
          color: #ea580c;
          font-weight: 600;
          background: #fff7ed;
        }
        .nav-item--active-admin::after {
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
        .nav-item--active-admin .nav-item__icon {
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

        /* ===== AUTH BUTTON ===== */
        .cs-btn--login {
          border-radius: 10px;
          font-weight: 600;
          font-size: 13px;
          padding: 8px 18px;
          cursor: pointer;
          transition: all 0.2s ease;
          border: none;
          color: white;
        }
        .cs-btn--login-admin {
          background: linear-gradient(135deg, #f97316 0%, #ea580c 100%);
          box-shadow: 0 2px 8px rgba(249, 115, 22, 0.3);
        }
        .cs-btn--login-admin:hover {
          transform: translateY(-1px);
          box-shadow: 0 4px 16px rgba(249, 115, 22, 0.4);
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
        .cs-profile-dropdown__header--admin {
          background: linear-gradient(135deg, #f97316 0%, #ea580c 100%);
        }
      `}</style>
    </>
  );
}
