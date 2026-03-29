import { useState, useEffect, useCallback } from "react";
import { createPortal } from "react-dom";

/* ───── global event bus ───── */
const listeners = new Set();
function emit(toast) {
  listeners.forEach((fn) => fn(toast));
}

/** Show a toast notification.
 *  @param {string} message
 *  @param {"success"|"error"|"info"|"warning"} type
 *  @param {number} duration  ms (default 3000)
 */
export function showToast(message, type = "info", duration = 3000) {
  emit({ id: Date.now() + Math.random(), message, type, duration });
}

/* convenience shortcuts */
showToast.success = (msg, ms) => showToast(msg, "success", ms);
showToast.error = (msg, ms) => showToast(msg, "error", ms ?? 4000);
showToast.info = (msg, ms) => showToast(msg, "info", ms);
showToast.warning = (msg, ms) => showToast(msg, "warning", ms);

/* ───── icons ───── */
const icons = {
  success: (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
      <path d="M20 6L9 17l-5-5" />
    </svg>
  ),
  error: (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="15" y1="9" x2="9" y2="15" />
      <line x1="9" y1="9" x2="15" y2="15" />
    </svg>
  ),
  warning: (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
      <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
      <line x1="12" y1="9" x2="12" y2="13" />
      <line x1="12" y1="17" x2="12.01" y2="17" />
    </svg>
  ),
  info: (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="16" x2="12" y2="12" />
      <line x1="12" y1="8" x2="12.01" y2="8" />
    </svg>
  ),
};

const colors = {
  success: { bg: "#22c55e", ring: "#16a34a" },
  error: { bg: "#ef4444", ring: "#dc2626" },
  warning: { bg: "#f59e0b", ring: "#d97706" },
  info: { bg: "#3b82f6", ring: "#2563eb" },
};

/* ───── single toast item ───── */
function ToastItem({ toast, onRemove }) {
  const [exiting, setExiting] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => setExiting(true), toast.duration);
    return () => clearTimeout(timer);
  }, [toast.duration]);

  useEffect(() => {
    if (exiting) {
      const t = setTimeout(() => onRemove(toast.id), 320);
      return () => clearTimeout(t);
    }
  }, [exiting, toast.id, onRemove]);

  const c = colors[toast.type] || colors.info;

  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 12,
        padding: "14px 20px",
        borderRadius: 14,
        background: "#fff",
        boxShadow: "0 8px 32px rgba(0,0,0,0.12), 0 2px 8px rgba(0,0,0,0.08)",
        border: `1px solid ${c.ring}20`,
        minWidth: 300,
        maxWidth: 440,
        animation: exiting
          ? "toast-slide-out .3s ease forwards"
          : "toast-slide-in .3s ease forwards",
        pointerEvents: "auto",
      }}
      onClick={() => setExiting(true)}
    >
      <div
        style={{
          width: 34,
          height: 34,
          borderRadius: 10,
          background: c.bg,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          flexShrink: 0,
        }}
      >
        {icons[toast.type] || icons.info}
      </div>
      <span style={{ fontSize: 14, fontWeight: 500, color: "#1e293b", lineHeight: 1.4, flex: 1 }}>
        {toast.message}
      </span>
      <button
        onClick={(e) => { e.stopPropagation(); setExiting(true); }}
        style={{
          background: "none", border: "none", cursor: "pointer",
          color: "#94a3b8", fontSize: 18, padding: 0, lineHeight: 1,
          flexShrink: 0,
        }}
      >
        ×
      </button>
    </div>
  );
}

/* ───── container (portal) ───── */
export function ToastContainer() {
  const [toasts, setToasts] = useState([]);

  useEffect(() => {
    const handler = (t) => setToasts((prev) => [...prev, t]);
    listeners.add(handler);
    return () => listeners.delete(handler);
  }, []);

  const remove = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  if (toasts.length === 0) return null;

  return createPortal(
    <>
      <style>{`
        @keyframes toast-slide-in {
          from { opacity: 0; transform: translateY(-16px) scale(.96); }
          to   { opacity: 1; transform: translateY(0) scale(1); }
        }
        @keyframes toast-slide-out {
          from { opacity: 1; transform: translateY(0) scale(1); }
          to   { opacity: 0; transform: translateY(-16px) scale(.96); }
        }
      `}</style>
      <div
        style={{
          position: "fixed",
          top: 24,
          right: 24,
          zIndex: 99999,
          display: "flex",
          flexDirection: "column",
          gap: 10,
          pointerEvents: "none",
        }}
      >
        {toasts.map((t) => (
          <ToastItem key={t.id} toast={t} onRemove={remove} />
        ))}
      </div>
    </>,
    document.body
  );
}
