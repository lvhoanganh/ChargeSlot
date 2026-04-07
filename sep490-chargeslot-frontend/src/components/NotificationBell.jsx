import { useState, useRef, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { notificationApi } from "@/services/api";
import { useAuthStore } from "@/stores/authStore";

/**
 * Extract the first numeric ID from a notification's content.
 * Searches both title AND content to ensure IDs aren't missed.
 */
function extractId(text) {
  if (!text) return null;
  const match = text.match(/#(\d+)/);
  return match ? match[1] : null;
}

/**
 * Build a navigation path based on notification content keywords (priority),
 * then type, then role fallback.
 * Routes must match exactly what's defined in App.jsx.
 */
function getNotificationRoute(notification, role) {
  const type = (notification.type || "").toLowerCase();
  const r = (role || "").toLowerCase();
  // Gộp title + content để tìm keyword VÀ extract ID
  const text = ((notification.title || "") + " " + (notification.content || "")).toLowerCase();
  const id = extractId(text);

  // ════════════════════════════════════════════════════════════
  // BƯỚC 1: Content keyword — ưu tiên cao hơn type
  // (Vì BE hay gửi type "Booking" cho nhiều loại sự kiện khác nhau)
  // ════════════════════════════════════════════════════════════

  // Ví / rút tiền / nạp tiền / thanh toán (ƯU TIÊN CAO NHẤT)
  if (text.includes("rút tiền") || text.includes("nạp tiền") || text.includes("số dư") || text.includes("thanh toán") || text.includes("hoàn tiền") || text.includes("tiền") || text.includes("vào ví") || text.includes("từ ví") || text.includes("bằng ví") || text.includes("đã hoàn") || text.includes("phí phạt")) {
    if (r === "driver") return "/driver/wallet";
    if (r === "owner") return "/owner/wallet";
    if (r === "admin") return "/admin/withdraws";
  }

  // Đánh giá / Review
  if (text.includes("đánh giá") || text.includes("review")) {
    if (r === "owner") return "/owner/reviews";
    if (r === "driver") return "/driver/reviews";
  }

  // Phiên sạc / kết thúc sớm / check-in / active session
  if (
    text.includes("kết thúc sớm") ||
    text.includes("yêu cầu kết thúc") ||
    text.includes("phiên sạc") ||
    text.includes("đang sạc") ||
    text.includes("check-in") ||
    text.includes("checkin") ||
    text.includes("sạc tại slot")
  ) {
    if (r === "owner") return "/owner/active-sessions";
    if (r === "driver") return "/driver/charging";
  }

  // Xác nhận hoàn thành phiên sạc → owner xem active sessions / driver xem charging-complete
  if (text.includes("hoàn thành") && (text.includes("sạc") || text.includes("phiên"))) {
    if (r === "owner") return "/owner/active-sessions";
    if (r === "driver") return "/driver/charging-complete";
  }

  // Tiền chuyển vào ví / thanh toán vào ví → owner wallet
  if (text.includes("chuyển vào ví") || text.includes("đã chuyển")) {
    if (r === "owner") return "/owner/wallet";
    if (r === "driver") return "/driver/wallet";
  }

  // Chat / tin nhắn
  if (text.includes("tin nhắn") || text.includes("nhắn tin") || text.includes("chat")) {
    if (r === "driver") return id ? `/driver/chat/${id}` : "/driver/chat-list";
    if (r === "owner") return id ? `/owner/chat/${id}` : "/owner/chat-list";
  }


  // Loyalty / điểm thưởng
  if (text.includes("điểm thưởng") || text.includes("loyalty") || text.includes("tích điểm")) {
    if (r === "driver") return "/driver/loyalty";
  }

  // Khiếu nại / dispute — ưu tiên cao, đặt trước type-based để tránh bị routing nhầm sang Booking
  if (
    text.includes("khiếu nại") ||
    text.includes("dispute") ||
    text.includes("tranh chấp") ||
    text.includes("phản ánh") ||
    text.includes("giải quyết khiếu")
  ) {
    if (r === "driver") return id ? `/driver/dispute/${id}` : "/driver/disputes";
    if (r === "owner") return id ? `/owner/dispute/${id}` : "/owner/disputes";
    if (r === "admin") return id ? `/admin/disputes/${id}` : "/admin/disputes";
  }

  // ════════════════════════════════════════════════════════════
  // BƯỚC 2: Type-based routing (fallback khi không match keyword)
  // ════════════════════════════════════════════════════════════

  if (type === "booking") {
    if (r === "driver") return id ? `/driver/booking/${id}` : "/driver/my-bookings";
    if (r === "owner") return id ? `/owner/booking/${id}` : "/owner/booking-requests";
    if (r === "admin") return "/admin/disputes";
  }

  if (type === "dispute") {
    if (r === "driver") return id ? `/driver/dispute/${id}` : "/driver/disputes";
    if (r === "owner") return id ? `/owner/dispute/${id}` : "/owner/disputes";
    if (r === "admin") return id ? `/admin/disputes/${id}` : "/admin/disputes";
  }

  if (type === "charging") {
    if (r === "driver") return "/driver/charging";
    if (r === "owner") return "/owner/active-sessions";
  }

  if (type === "payment") {
    if (r === "driver") return "/driver/wallet";
    if (r === "owner") return "/owner/wallet";
    if (r === "admin") return "/admin/view-financial-report";
  }

  if (type === "stationapproval") {
    if (r === "owner") return "/owner/booking-requests";
    if (r === "admin") return "/admin/approve-station";
  }

  if (type === "review") {
    if (r === "owner") return "/owner/reviews";
    if (r === "driver") return "/driver/reviews";
  }

  if (type === "wallet") {
    if (r === "driver") return "/driver/wallet";
    if (r === "owner") return "/owner/wallet";
    if (r === "admin") return "/admin/withdraws";
  }

  // ════════════════════════════════════════════════════════════
  // BƯỚC 3: Default fallback theo role
  // ════════════════════════════════════════════════════════════
  if (r === "driver") return "/driver/my-bookings";
  if (r === "owner") return "/owner/booking-requests";
  if (r === "admin") return "/admin/disputes";
  return null;
}

/** Icon theo loại notification */
function NotifIcon({ type }) {
  const t = (type || "").toLowerCase();
  const icons = {
    booking: {
      bg: "linear-gradient(135deg,#f97316,#ea580c)",
      svg: (
        <svg viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
          <path d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
        </svg>
      ),
    },
    payment: {
      bg: "linear-gradient(135deg,#10b981,#059669)",
      svg: (
        <svg viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
          <path d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z" />
        </svg>
      ),
    },
    charging: {
      bg: "linear-gradient(135deg,#3b82f6,#2563eb)",
      svg: (
        <svg viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
          <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
        </svg>
      ),
    },
    dispute: {
      bg: "linear-gradient(135deg,#ef4444,#dc2626)",
      svg: (
        <svg viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
          <path d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
        </svg>
      ),
    },
    stationapproval: {
      bg: "linear-gradient(135deg,#8b5cf6,#7c3aed)",
      svg: (
        <svg viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
          <path d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
      ),
    },
  };

  const cfg = icons[t] || {
    bg: "linear-gradient(135deg,#6b7280,#4b5563)",
    svg: (
      <svg viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
        <path d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
      </svg>
    ),
  };

  return (
    <span
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        width: 38,
        height: 38,
        borderRadius: "50%",
        background: cfg.bg,
        flexShrink: 0,
        boxShadow: "0 2px 8px rgba(0,0,0,0.15)",
      }}
    >
      <span style={{ width: 18, height: 18, display: "flex" }}>{cfg.svg}</span>
    </span>
  );
}

/** Thời gian tương đối: "2 phút trước", "1 giờ trước", v.v. */
function timeAgo(dateStr) {
  const date = new Date(String(dateStr).replace("Z", ""));
  const diff = (Date.now() - date.getTime()) / 1000;
  if (diff < 60) return "Vừa xong";
  if (diff < 3600) return `${Math.floor(diff / 60)} phút trước`;
  if (diff < 86400) return `${Math.floor(diff / 3600)} giờ trước`;
  if (diff < 604800) return `${Math.floor(diff / 86400)} ngày trước`;
  return date.toLocaleDateString("vi-VN");
}

export default function NotificationBell() {
  const [notifications, setNotifications] = useState([]);
  const [open, setOpen] = useState(false);
  const ref = useRef(null);
  const navigate = useNavigate();
  const { role } = useAuthStore();

  // Close on outside click
  useEffect(() => {
    function handleClickOutside(e) {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false);
    }
    if (open) document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [open]);

  // Fetch & poll notifications
  useEffect(() => {
    const fetchNoti = () => {
      notificationApi.getAll()
        .then(data => { if (Array.isArray(data)) setNotifications(data); })
        .catch(() => {});
    };
    fetchNoti();
    const interval = setInterval(fetchNoti, 15000);
    return () => clearInterval(interval);
  }, []);

  const unreadCount = notifications.filter(n => !n.isRead).length;

  async function handleClick(n) {
    if (!n.isRead) {
      try {
        await notificationApi.markAsRead(n.id);
        setNotifications(prev => prev.map(x => x.id === n.id ? { ...x, isRead: true } : x));
      } catch {}
    }
    const route = getNotificationRoute(n, role);
    if (route) {
      setOpen(false);
      navigate(route);
    }
  }

  async function markAllRead() {
    try {
      const unread = notifications.filter(n => !n.isRead);
      if (unread.length === 0) return;
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
      await Promise.all(unread.map(n => notificationApi.markAsRead(n.id).catch(() => {})));
    } catch {}
  }

  return (
    <>
      <div className="cs-nbell" ref={ref}>
        {/* Bell button */}
        <button
          onClick={() => setOpen(prev => !prev)}
          className="cs-nbell__btn"
          aria-label="Thông báo"
        >
          <svg className="cs-nbell__icon" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
              d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
          </svg>
          {unreadCount > 0 && (
            <span className="cs-nbell__badge">
              {unreadCount > 99 ? "99+" : unreadCount}
            </span>
          )}
        </button>

        {/* Dropdown panel */}
        <div className={`cs-nbell__panel ${open ? "cs-nbell__panel--open" : ""}`}>
          {/* Header */}
          <div className="cs-nbell__header">
            <div className="cs-nbell__header-left">
              <svg style={{ width: 18, height: 18, color: "#f97316" }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
              </svg>
              <span className="cs-nbell__title">Thông báo</span>
              {unreadCount > 0 && (
                <span className="cs-nbell__count-badge">{unreadCount}</span>
              )}
            </div>
            {unreadCount > 0 && (
              <button onClick={markAllRead} className="cs-nbell__mark-all">
                Đọc tất cả
              </button>
            )}
          </div>

          {/* List */}
          <div className="cs-nbell__list">
            {notifications.length === 0 ? (
              <div className="cs-nbell__empty">
                <div className="cs-nbell__empty-icon">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5}>
                    <path strokeLinecap="round" strokeLinejoin="round"
                      d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
                  </svg>
                </div>
                <p className="cs-nbell__empty-text">Không có thông báo</p>
                <p className="cs-nbell__empty-sub">Mọi cập nhật sẽ xuất hiện ở đây</p>
              </div>
            ) : (
              notifications.slice(0, 20).map(n => (
                <div
                  key={n.id}
                  onClick={() => handleClick(n)}
                  className={`cs-nbell__item ${!n.isRead ? "cs-nbell__item--unread" : ""}`}
                >
                  <NotifIcon type={n.type} />
                  <div className="cs-nbell__item-body">
                    <p className="cs-nbell__item-title">{n.title}</p>
                    <p className="cs-nbell__item-content">{n.content}</p>
                    <p className="cs-nbell__item-time">{timeAgo(n.createdAt)}</p>
                  </div>
                  {!n.isRead && <span className="cs-nbell__unread-dot" />}
                </div>
              ))
            )}
          </div>
        </div>
      </div>

      <style>{`
        /* ===== NOTIFICATION BELL WRAPPER ===== */
        .cs-nbell {
          position: relative;
        }

        /* ===== BELL BUTTON ===== */
        .cs-nbell__btn {
          position: relative;
          display: flex;
          align-items: center;
          justify-content: center;
          width: 40px;
          height: 40px;
          border-radius: 50%;
          border: none;
          background: none;
          cursor: pointer;
          transition: background 0.2s;
        }
        .cs-nbell__btn:hover {
          background: #fff7ed;
        }
        .cs-nbell__icon {
          width: 22px;
          height: 22px;
          color: #6b7280;
          transition: color 0.2s;
        }
        .cs-nbell__btn:hover .cs-nbell__icon {
          color: #f97316;
        }
        .cs-nbell__badge {
          position: absolute;
          top: 2px;
          right: 2px;
          min-width: 18px;
          height: 18px;
          display: flex;
          align-items: center;
          justify-content: center;
          border-radius: 9999px;
          background: #ef4444;
          color: white;
          font-size: 10px;
          font-weight: 700;
          padding: 0 4px;
          border: 2px solid white;
          animation: cs-pulse 2s infinite;
        }
        @keyframes cs-pulse {
          0%, 100% { box-shadow: 0 0 0 0 rgba(239,68,68,0.4); }
          50% { box-shadow: 0 0 0 4px rgba(239,68,68,0); }
        }

        /* ===== DROPDOWN PANEL ===== */
        .cs-nbell__panel {
          position: fixed;
          right: 8px;
          top: 68px;
          width: calc(100vw - 16px);
          max-width: 380px;
          background: white;
          border-radius: 16px;
          box-shadow: 0 20px 60px rgba(0,0,0,0.12), 0 4px 16px rgba(0,0,0,0.08);
          border: 1px solid rgba(0,0,0,0.06);
          z-index: 9999;
          overflow: hidden;
          /* Animation */
          opacity: 0;
          transform: translateY(-8px) scale(0.97);
          pointer-events: none;
          transition: opacity 0.2s ease, transform 0.2s ease;
          transform-origin: top right;
        }
        .cs-nbell__panel--open {
          opacity: 1;
          transform: translateY(0) scale(1);
          pointer-events: auto;
        }

        /* ===== HEADER ===== */
        .cs-nbell__header {
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 14px 16px;
          border-bottom: 1px solid #f3f4f6;
          background: linear-gradient(135deg, #fff7ed 0%, #ffffff 100%);
        }
        .cs-nbell__header-left {
          display: flex;
          align-items: center;
          gap: 8px;
        }
        .cs-nbell__title {
          font-size: 15px;
          font-weight: 700;
          color: #111827;
        }
        .cs-nbell__count-badge {
          display: inline-flex;
          align-items: center;
          justify-content: center;
          min-width: 20px;
          height: 20px;
          background: linear-gradient(135deg, #f97316, #ea580c);
          color: white;
          font-size: 11px;
          font-weight: 700;
          border-radius: 9999px;
          padding: 0 5px;
        }
        .cs-nbell__mark-all {
          font-size: 12px;
          font-weight: 600;
          color: #f97316;
          background: none;
          border: none;
          cursor: pointer;
          padding: 4px 10px;
          border-radius: 8px;
          transition: background 0.15s;
        }
        .cs-nbell__mark-all:hover {
          background: #fff7ed;
          color: #ea580c;
        }

        /* ===== LIST ===== */
        .cs-nbell__list {
          max-height: min(65vh, 440px);
          overflow-y: auto;
          overscroll-behavior: contain;
        }
        .cs-nbell__list::-webkit-scrollbar {
          width: 4px;
        }
        .cs-nbell__list::-webkit-scrollbar-track {
          background: transparent;
        }
        .cs-nbell__list::-webkit-scrollbar-thumb {
          background: #e5e7eb;
          border-radius: 4px;
        }

        /* ===== ITEM ===== */
        .cs-nbell__item {
          display: flex;
          align-items: flex-start;
          gap: 12px;
          padding: 13px 16px;
          cursor: pointer;
          transition: background 0.15s;
          border-bottom: 1px solid #f9fafb;
          position: relative;
        }
        .cs-nbell__item:last-child {
          border-bottom: none;
        }
        .cs-nbell__item:hover {
          background: #f9fafb;
        }
        .cs-nbell__item--unread {
          background: #fffbf7;
        }
        .cs-nbell__item--unread:hover {
          background: #fff3e8;
        }
        .cs-nbell__item-body {
          flex: 1;
          min-width: 0;
        }
        .cs-nbell__item-title {
          font-size: 13px;
          font-weight: 600;
          color: #111827;
          line-height: 1.4;
          margin: 0 0 3px 0;
        }
        .cs-nbell__item-content {
          font-size: 12px;
          color: #6b7280;
          line-height: 1.45;
          margin: 0 0 4px 0;
          display: -webkit-box;
          -webkit-line-clamp: 2;
          -webkit-box-orient: vertical;
          overflow: hidden;
        }
        .cs-nbell__item-time {
          font-size: 11px;
          color: #9ca3af;
          margin: 0;
          font-weight: 500;
        }
        .cs-nbell__unread-dot {
          width: 8px;
          height: 8px;
          border-radius: 50%;
          background: linear-gradient(135deg, #f97316, #ea580c);
          flex-shrink: 0;
          margin-top: 4px;
          box-shadow: 0 0 0 2px rgba(249,115,22,0.2);
        }

        /* ===== EMPTY STATE ===== */
        .cs-nbell__empty {
          padding: 40px 24px;
          text-align: center;
        }
        .cs-nbell__empty-icon {
          width: 56px;
          height: 56px;
          border-radius: 50%;
          background: #f3f4f6;
          display: flex;
          align-items: center;
          justify-content: center;
          margin: 0 auto 12px;
          color: #d1d5db;
        }
        .cs-nbell__empty-icon svg {
          width: 28px;
          height: 28px;
        }
        .cs-nbell__empty-text {
          font-size: 14px;
          font-weight: 600;
          color: #374151;
          margin: 0 0 4px 0;
        }
        .cs-nbell__empty-sub {
          font-size: 12px;
          color: #9ca3af;
          margin: 0;
        }

        /* ===== RESPONSIVE: tablet+ use positioned dropdown ===== */
        @media (min-width: 480px) {
          .cs-nbell__panel {
            position: absolute;
            right: -8px;
            top: calc(100% + 10px);
            width: 380px;
          }
        }
      `}</style>
    </>
  );
}
