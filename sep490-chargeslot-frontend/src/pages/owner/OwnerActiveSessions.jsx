import { useState, useEffect } from "react";
import { chargingApi } from "@/services/api";
import { showToast } from "@/components/Toast";

const toLocal = (dt) => {
  if (!dt) return "—";
  const s = String(dt);
  return new Date(String(s).replace("Z", "")).toLocaleString("vi-VN");
};

function formatElapsed(startTime) {
  if (!startTime) return "—";
  const s = String(startTime);
  const start = new Date(String(s).replace("Z", ""));
  const now = new Date();
  const diff = Math.floor((now - start) / 1000);
  const h = Math.floor(diff / 3600);
  const m = Math.floor((diff % 3600) / 60);
  return h > 0 ? `${h} giờ ${m} phút` : `${m} phút`;
}

export default function OwnerActiveSessions() {
  const [sessions, setSessions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [stoppingId, setStoppingId] = useState(null);
  const [confirmStop, setConfirmStop] = useState(null);

  function fetchSessions() {
    chargingApi.getActiveSessions()
      .then((data) => setSessions(Array.isArray(data) ? data : []))
      .catch(() => setSessions([]))
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    fetchSessions();
    const interval = setInterval(fetchSessions, 15000);
    return () => clearInterval(interval);
  }, []);

  async function handleStop(sessionId) {
    setStoppingId(sessionId);
    try {
      await chargingApi.stopCharging(sessionId);
      setSessions((prev) => prev.filter((s) => s.id !== sessionId));
      setConfirmStop(null);
    } catch (err) {
      showToast.error(err?.message || "Lỗi khi dừng phiên sạc");
    } finally {
      setStoppingId(null);
    }
  }

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 40 }}>⚡</div>
        <p style={{ color: "#6b7280" }}>Đang tải phiên sạc...</p>
      </div>
    );
  }

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90 }}>
      <div style={{ maxWidth: 800, margin: "0 auto", padding: "0 16px 40px" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 20 }}>
          <h1 style={{ fontSize: 24, fontWeight: 800, color: "#1e293b" }}>
            Phiên sạc đang hoạt động
            {sessions.length > 0 && (
              <span style={{ fontSize: 13, fontWeight: 600, color: "#06b6d4", background: "#ecfeff", padding: "4px 10px", borderRadius: 20, marginLeft: 8 }}>
                {sessions.length} phiên
              </span>
            )}
          </h1>
          <button
            onClick={() => { setLoading(true); fetchSessions(); }}
            style={{ padding: "8px 16px", borderRadius: 10, border: "1.5px solid #e5e7eb", background: "#fff", color: "#374151", fontWeight: 600, fontSize: 13, cursor: "pointer" }}
          >
            🔄 Làm mới
          </button>
        </div>

        {sessions.length === 0 ? (
          <div style={{ textAlign: "center", padding: 60, background: "#fff", borderRadius: 16, boxShadow: "0 2px 8px rgba(0,0,0,0.04)" }}>
            <div style={{ fontSize: 56, marginBottom: 12 }}>🔌</div>
            <h2 style={{ fontSize: 18, fontWeight: 700, color: "#1e293b", marginBottom: 4 }}>Không có phiên sạc nào</h2>
            <p style={{ color: "#6b7280", fontSize: 14 }}>Các phiên sạc đang diễn ra sẽ hiển thị tại đây</p>
          </div>
        ) : (
          sessions.map((s) => (
            <div
              key={s.id}
              style={{
                background: "#fff", borderRadius: 16, padding: 20, marginBottom: 12,
                boxShadow: "0 2px 8px rgba(0,0,0,0.04)", border: "2px solid #06b6d4",
              }}
            >
              {/* Header */}
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
                <div>
                  <span style={{ fontWeight: 700, fontSize: 16, color: "#1e293b" }}>Phiên #{s.id}</span>
                  <span style={{ fontSize: 12, fontWeight: 600, color: "#06b6d4", background: "#ecfeff", padding: "3px 8px", borderRadius: 12, marginLeft: 8 }}>
                    ⚡ Đang sạc
                  </span>
                </div>
                <span style={{ fontSize: 13, color: "#6b7280" }}>Booking #{s.bookingId}</span>
              </div>

              {/* Info */}
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8, marginBottom: 16 }}>
                <InfoItem label="Driver" value={s.driverName || "—"} />
                <InfoItem label="Trạm" value={s.stationName || "—"} />
                <InfoItem label="Cổng sạc" value={s.slotName || `Slot ${s.slotId}`} />
                <InfoItem label="Đã sạc" value={formatElapsed(s.actualStartTime)} highlight />
                <InfoItem label="Bắt đầu" value={toLocal(s.actualStartTime)} />
                <InfoItem label="Booking kết thúc" value={toLocal(s.bookingEndTime)} />
                <InfoItem label="Tổng tiền" value={`${(s.totalAmount || 0).toLocaleString("vi-VN")}đ`} highlight />
              </div>

              {/* Stop button */}
              <button
                onClick={() => setConfirmStop(s)}
                disabled={stoppingId === s.id}
                style={{
                  width: "100%", padding: "12px 0", borderRadius: 12, border: "none",
                  background: stoppingId === s.id ? "#d1d5db" : "linear-gradient(135deg, #ef4444, #dc2626)",
                  color: "#fff", fontWeight: 700, fontSize: 15, cursor: stoppingId === s.id ? "not-allowed" : "pointer",
                  boxShadow: "0 4px 14px rgba(239,68,68,0.25)",
                }}
              >
                {stoppingId === s.id ? "Đang xử lý..." : "⏹️ Dừng phiên sạc & Tạo hóa đơn"}
              </button>
            </div>
          ))
        )}
      </div>

      {/* Confirm Modal */}
      {confirmStop && (
        <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.4)", display: "flex", alignItems: "center", justifyContent: "center", zIndex: 50 }}>
          <div style={{ background: "#fff", borderRadius: 16, padding: 24, width: 420, maxWidth: "90vw" }}>
            <h2 style={{ fontSize: 18, fontWeight: 700, marginBottom: 12 }}>⏹️ Xác nhận dừng phiên sạc</h2>
            <p style={{ fontSize: 14, color: "#6b7280", marginBottom: 8 }}>
              Bạn có chắc muốn dừng phiên sạc <strong>#{confirmStop.id}</strong> của driver <strong>{confirmStop.driverName}</strong>?
            </p>
            <div style={{ background: "#fffbeb", border: "1px solid #fde68a", borderRadius: 10, padding: 12, marginBottom: 16 }}>
              <p style={{ fontSize: 13, color: "#92400e", margin: 0 }}>
                ⚠️ Sau khi dừng, hệ thống sẽ tạo hóa đơn và gửi cho driver xác nhận hoặc khiếu nại.
              </p>
            </div>
            <div style={{ display: "flex", gap: 8 }}>
              <button
                onClick={() => setConfirmStop(null)}
                disabled={stoppingId}
                style={{ flex: 1, padding: 12, borderRadius: 12, border: "1.5px solid #e5e7eb", background: "#fff", color: "#374151", fontWeight: 600, cursor: "pointer" }}
              >
                Hủy
              </button>
              <button
                onClick={() => handleStop(confirmStop.id)}
                disabled={stoppingId}
                style={{
                  flex: 1, padding: 12, borderRadius: 12, border: "none",
                  background: stoppingId ? "#d1d5db" : "#ef4444", color: "#fff", fontWeight: 700, cursor: stoppingId ? "not-allowed" : "pointer",
                }}
              >
                {stoppingId ? "Đang xử lý..." : "Xác nhận dừng"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function InfoItem({ label, value, highlight }) {
  return (
    <div style={{ fontSize: 13 }}>
      <span style={{ color: "#9ca3af" }}>{label}: </span>
      <span style={{ fontWeight: 600, color: highlight ? "#f97316" : "#1e293b" }}>{value}</span>
    </div>
  );
}
