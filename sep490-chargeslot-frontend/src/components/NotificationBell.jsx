import { useState, useRef, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { notificationApi } from "@/services/api";
import { useAuthStore } from "@/stores/authStore";

/**
 * Extract the first numeric ID from a notification's content.
 * Patterns: "Booking #12", "#12", "booking 12", etc.
 */
function extractId(content) {
  if (!content) return null;
  const match = content.match(/#(\d+)/);
  return match ? match[1] : null;
}

/**
 * Build a navigation path based on notification type, user role, and extracted ID.
 */
function getNotificationRoute(notification, role) {
  const id = extractId(notification.content);
  const type = (notification.type || "").toLowerCase();
  const r = (role || "").toLowerCase();

  switch (type) {
    case "booking":
      if (r === "driver") return id ? `/driver/booking/${id}` : "/driver/my-bookings";
      if (r === "owner") return "/owner/booking-requests";
      if (r === "admin") return "/admin/disputes";
      break;
    case "dispute":
      if (r === "driver") return id ? `/driver/dispute/${id}` : "/driver/my-bookings";
      if (r === "owner") return id ? `/owner/dispute/${id}` : "/owner/booking-requests";
      if (r === "admin") return id ? `/admin/disputes/${id}` : "/admin/disputes";
      break;
    case "charging":
      if (r === "driver") return "/driver/charging-active";
      if (r === "owner") return "/owner/booking-requests";
      break;
    case "payment":
      if (r === "driver") return id ? `/driver/booking/${id}` : "/driver/my-bookings";
      if (r === "owner") return "/owner/booking-requests";
      break;
    case "stationapproval":
      if (r === "owner") return "/owner";
      if (r === "admin") return "/admin/stations";
      break;
    case "system":
    default:
      // System notifications (e.g. ban/unban) — no specific page
      break;
  }

  // Fallback per role
  if (r === "driver") return "/driver/my-bookings";
  if (r === "owner") return "/owner/booking-requests";
  if (r === "admin") return "/admin/disputes";
  return null;
}

export default function NotificationBell() {
  const [notifications, setNotifications] = useState([]);
  const [open, setOpen] = useState(false);
  const ref = useRef(null);
  const navigate = useNavigate();
  const { role } = useAuthStore();

  useEffect(() => {
    function handleClickOutside(e) {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false);
    }
    if (open) document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [open]);

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
    // Mark as read
    if (!n.isRead) {
      try {
        await notificationApi.markAsRead(n.id);
        setNotifications(prev => prev.map(x => x.id === n.id ? { ...x, isRead: true } : x));
      } catch {}
    }

    // Navigate to relevant page
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
      
      // Update locally immediately for snappy UI
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
      
      // Bắn toàn bộ unread ID lên backend qua API /Notification/{id}/read
      await Promise.all(
        unread.map(n => notificationApi.markAsRead(n.id).catch(() => {}))
      );
    } catch {}
  }

  return (
    <div className="relative" ref={ref}>
      <button
        onClick={() => setOpen(prev => !prev)}
        className="relative p-2 rounded-full hover:bg-gray-100 transition-colors cursor-pointer focus:outline-none"
        aria-label="Thông báo"
      >
        <svg className="w-6 h-6 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
        </svg>
        {unreadCount > 0 && (
          <span className="absolute -top-0.5 -right-0.5 min-w-[20px] h-5 flex items-center justify-center rounded-full bg-red-500 text-white text-[11px] font-bold px-1 shadow-sm animate-pulse">
            {unreadCount > 99 ? "99+" : unreadCount}
          </span>
        )}
      </button>

      <div className={`absolute right-0 top-full mt-2 w-80 transition-all duration-300 origin-top-right ${
        open ? "opacity-100 scale-100 pointer-events-auto" : "opacity-0 scale-95 pointer-events-none"
      }`}>
        <div className="bg-white rounded-2xl shadow-2xl border border-gray-100 overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-100 flex items-center justify-between">
            <h3 className="text-sm font-bold text-gray-800">🔔 Thông báo</h3>
            {unreadCount > 0 && (
              <button onClick={markAllRead} className="text-xs text-orange-500 hover:text-orange-600 font-medium cursor-pointer">
                Đọc tất cả
              </button>
            )}
          </div>
          <div className="max-h-80 overflow-y-auto">
            {notifications.length === 0 ? (
              <div className="px-4 py-8 text-center">
                <p className="text-gray-400 text-sm">Không có thông báo</p>
              </div>
            ) : (
              notifications.slice(0, 20).map(n => (
                <div
                  key={n.id}
                  onClick={() => handleClick(n)}
                  className={`px-4 py-3 border-b border-gray-50 cursor-pointer hover:bg-gray-50 transition-colors ${
                    !n.isRead ? "bg-orange-50/50" : ""
                  }`}
                >
                  <div className="flex items-start gap-2">
                    {!n.isRead && (
                      <span className="mt-1.5 w-2 h-2 rounded-full bg-orange-500 flex-shrink-0" />
                    )}
                    <div className="flex-1 min-w-0">
                      <p className={`text-sm ${!n.isRead ? "font-semibold text-gray-800" : "text-gray-600"}`}>{n.title}</p>
                      <p className="text-xs text-gray-500 mt-0.5 line-clamp-2">{n.content}</p>
                      <p className="text-[11px] text-gray-400 mt-1">
                        {new Date(String(n.createdAt).replace("Z", "")).toLocaleString("vi-VN")}
                      </p>
                    </div>
                    {/* Arrow indicator */}
                    <svg className="w-4 h-4 text-gray-300 mt-1 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                    </svg>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
