import { useState, useEffect, useCallback } from "react";
import { useNavigate, Link } from "react-router-dom";
import { showConfirm } from "@/components/ConfirmDialog";
import { stationApi, slotApi, stationPricingApi, chargingApi } from "@/services/api";
import { QRCodeSVG } from "qrcode.react";
import { showToast } from "@/components/Toast";
import TimePicker24h from "@/components/TimePicker24h";
import { MapContainer, TileLayer, Marker, Popup, useMap } from "react-leaflet";
import L from "leaflet";
import "leaflet/dist/leaflet.css";

/* ─── Leaflet Setup ─── */
delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: "https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-icon-2x.png",
  iconUrl: "https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-icon.png",
  shadowUrl: "https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-shadow.png",
});

function makeIcon(gradient, pulseColor) {
  return new L.DivIcon({
    html: `
      <div style="position:relative;width:52px;height:64px;">
        <div style="position:absolute;top:6px;left:6px;width:40px;height:40px;border-radius:50%;background:${pulseColor};animation:stationPulse 2s ease-out infinite;"></div>
        <div style="position:absolute;top:0;left:2px;width:48px;height:48px;border-radius:50% 50% 50% 0;transform:rotate(-45deg);background:${gradient};border:3px solid #fff;box-shadow:0 4px 14px rgba(0,0,0,.35);"></div>
        <div style="position:absolute;top:6px;left:8px;width:36px;height:36px;display:flex;align-items:center;justify-content:center;">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth="2.5"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z"/></svg>
        </div>
      </div>`,
    className: "",
    iconSize: [52, 64],
    iconAnchor: [26, 64],
    popupAnchor: [0, -56],
  });
}

const mapIcons = {
  Active: makeIcon("linear-gradient(135deg,#22c55e,#0f9d43)", "rgba(34,197,94,.35)"),
  Inactive: makeIcon("linear-gradient(135deg,#6b7280,#4b5563)", "rgba(107,114,128,.35)"),
  Draft: makeIcon("linear-gradient(135deg,#94a3b8,#64748b)", "rgba(148,163,184,.35)"),
  PendingApproval: makeIcon("linear-gradient(135deg,#f59e0b,#d97706)", "rgba(245,158,11,.35)"),
  Rejected: makeIcon("linear-gradient(135deg,#ef4444,#dc2626)", "rgba(239,68,68,.35)"),
};

function FlyTo({ center, zoom }) {
  const map = useMap();
  useEffect(() => {
    if (center) map.flyTo(center, zoom || 15, { duration: 1.2 });
  }, [center, zoom, map]);
  return null;
}


const statusConfig = {
  Draft: { label: "Nháp", color: "#6b7280", bg: "#f3f4f6", icon: "📝" },
  PendingApproval: { label: "Chờ duyệt", color: "#f59e0b", bg: "#fffbeb", icon: "⏳" },
  Approved: { label: "Đã duyệt", color: "#22c55e", bg: "#f0fdf4", icon: "✅" },
  Rejected: { label: "Bị từ chối", color: "#ef4444", bg: "#fef2f2", icon: "❌" },
  Active: { label: "Hoạt động", color: "#22c55e", bg: "#f0fdf4", icon: "⚡" },
  Inactive: { label: "Ngưng", color: "#6b7280", bg: "#f3f4f6", icon: "⏸️" },
};

const slotColors = {
  Active:    { bg: "#22c55e", text: "#fff", border: "#16a34a", label: "Hoạt động" },
  Inactive:  { bg: "#94a3b8", text: "#fff", border: "#64748b", label: "Ngưng" },
  Maintenance:{ bg: "#f97316", text: "#fff", border: "#ea580c", label: "Bảo trì" },
  Available: { bg: "#22c55e", text: "#fff", border: "#16a34a", label: "Trống" },
  Occupied:  { bg: "#ef4444", text: "#fff", border: "#dc2626", label: "Đang dùng" },
  CheckedIn: { bg: "#06b6d4", text: "#fff", border: "#0891b2", label: "Đã check-in" },
  Booked:    { bg: "#f59e0b", text: "#fff", border: "#d97706", label: "Đã đặt chỗ" },
  Reserved:  { bg: "#3b82f6", text: "#fff", border: "#2563eb", label: "Giữ chỗ" },
};

export default function OwnerPage() {
  const navigate = useNavigate();
  const [stations, setStations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [expandedStation, setExpandedStation] = useState(null);
  const [actionLoading, setActionLoading] = useState(null);
  const [selectedSlot, setSelectedSlot] = useState(null);
  const [filter, setFilter] = useState("all");
  const [search, setSearch] = useState("");
  const [viewMode, setViewMode] = useState("list"); // "list" or "map"
  const [flyTarget, setFlyTarget] = useState([21.0285, 105.8542]);
  const [userPos, setUserPos] = useState(null);
  
  const [occupiedSlots, setOccupiedSlots] = useState(new Set());    // Đang sạc (InProgress/Charging)
  const [checkedInSlots, setCheckedInSlots] = useState(new Set());  // Đã check-in, chưa sạc

  useEffect(() => {
    navigator.geolocation?.getCurrentPosition(
      (pos) => setUserPos([pos.coords.latitude, pos.coords.longitude]),
      () => {},
      { enableHighAccuracy: true, timeout: 8000 }
    );
  }, []);

  function fetchStations() {
    setLoading(true);
    stationApi.getAll()
      .then((data) => setStations(Array.isArray(data) ? data : []))
      .catch(() => setStations([]))
      .finally(() => setLoading(false));
  }

  useEffect(() => { fetchStations(); }, []);

  // Fetch active sessions — phân biệt đang sạc (InProgress) và đã check-in (chưa sạc)
  useEffect(() => {
    function parseSessions(sessions) {
      const occupied = new Set();
      const checkedIn = new Set();
      if (Array.isArray(sessions)) {
        sessions.forEach(session => {
          if (!session.slotId) return;
          const now = Date.now();
          let isTimeStarted = false;
          if (session.bookingStartTime || session.actualStartTime) {
            const timeStr = String(session.bookingStartTime || session.actualStartTime);
            const ms = new Date(timeStr.endsWith("Z") || /[+-]\d{2}:\d{2}$/.test(timeStr) ? timeStr : timeStr + "+07:00").getTime();
            if (!isNaN(ms) && now >= ms) isTimeStarted = true;
          }

          const st = session.status || session.bookingStatus || "";
          const isInProgressRaw = st === "InProgress" || st === "Charging" || session.bookingStatus === "InProgress" || session.bookingStatus === "Charging";
          const isCheckedInRaw = st === "CheckedIn" || session.bookingStatus === "CheckedIn";

          const isOccupied = isInProgressRaw || (isCheckedInRaw && isTimeStarted);
          const isCheckedIn = isCheckedInRaw && !isTimeStarted;
          
          if (isOccupied) {
            occupied.add(Number(session.slotId));
          } else if (isCheckedIn) {
            checkedIn.add(Number(session.slotId));
          }
        });
      }
      return { occupied, checkedIn };
    }
    chargingApi.getActiveSessions()
      .then((sessions) => {
        const { occupied, checkedIn } = parseSessions(sessions);
        setOccupiedSlots(occupied);
        setCheckedInSlots(checkedIn);
      })
      .catch(() => { setOccupiedSlots(new Set()); setCheckedInSlots(new Set()); });
    const interval = setInterval(() => {
      chargingApi.getActiveSessions()
        .then((sessions) => {
          const { occupied, checkedIn } = parseSessions(sessions);
          setOccupiedSlots(occupied);
          setCheckedInSlots(checkedIn);
        })
        .catch(() => {});
    }, 10000);
    return () => clearInterval(interval);
  }, []);

  async function handleSubmitForApproval(id) {
    if (!(await showConfirm("Gửi trạm sạc để Admin duyệt?", "Xác nhận gửi duyệt"))) return;
    setActionLoading(id);
    try {
      await stationApi.submitForApproval(id);
      fetchStations();
    } catch (err) {
      showToast.error(err.message || "Lỗi khi gửi duyệt");
    } finally {
      setActionLoading(null);
    }
  }

  if (loading) {
    return (
      <div className="min-h-screen bg-slate-100 px-6 pt-20 pb-8 flex items-center justify-center">
        <div className="text-center">
          <div className="text-5xl mb-4">⚡</div>
          <p className="text-slate-500">Đang tải danh sách trạm sạc...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-100 px-6 pt-20 pb-8 text-slate-900">
      <div className="mx-auto max-w-6xl">
        {/* Header */}
        <div className="mb-6 flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-white px-6 py-5 shadow-sm ring-1 ring-slate-200">
          <div>
            <p className="text-sm font-medium uppercase tracking-[0.18em] text-orange-500">
              Owner Dashboard
            </p>
            <h1 className="mt-2 text-3xl font-bold">Trạm sạc của tôi</h1>
            <p className="mt-1 text-sm text-slate-600">
              Quản lý trạm sạc, mặt bằng và trụ sạc.
            </p>
          </div>
          <Link
            to="/stations/add"
            className="inline-flex items-center gap-2 rounded-xl bg-orange-500 px-5 py-3 text-sm font-semibold text-white transition hover:bg-orange-600"
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" /></svg>
            Tạo trạm mới
          </Link>
        </div>

        <style>{`
          .map-view-container {
            height: 600px;
            width: 100%;
            border-radius: 16px;
            overflow: hidden;
            box-shadow: 0 4px 6px -1px rgb(0 0 0 / 0.1);
            position: relative;
            z-index: 1;
            border: 1px solid #e2e8f0;
          }
          .custom-popup .leaflet-popup-content-wrapper {
            border-radius: 16px;
            padding: 0;
            overflow: hidden;
            box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.1), 0 8px 10px -6px rgba(0, 0, 0, 0.1);
          }
          .custom-popup .leaflet-popup-content {
            margin: 0;
            width: 280px !important;
          }
          .custom-popup .leaflet-popup-close-button {
            top: 12px;
            right: 12px;
            color: #64748b;
          }
          @keyframes stationPulse {
            0% { transform: scale(0.95); opacity: 0.8; }
            70% { transform: scale(1.3); opacity: 0; }
            100% { transform: scale(0.95); opacity: 0; }
          }
        `}</style>


        {/* Station list */}
        {stations.length === 0 ? (
          <div className="rounded-2xl bg-white p-12 text-center shadow-sm ring-1 ring-slate-200">
            <div className="text-5xl mb-4">🏗️</div>
            <h2 className="text-xl font-bold text-slate-800 mb-2">Chưa có trạm sạc nào</h2>
            <p className="text-slate-500 mb-6">Bắt đầu bằng cách tạo trạm sạc đầu tiên.</p>
            <Link
              to="/stations/add"
              className="inline-flex items-center gap-2 rounded-xl bg-orange-500 px-6 py-3 text-white font-semibold transition hover:bg-orange-600"
            >
              Tạo trạm sạc
            </Link>
          </div>
        ) : (
          <div>
            <div className="mb-4 flex flex-col md:flex-row gap-4 justify-between items-start md:items-center">
              <div className="flex flex-wrap gap-2">
                {[
                  { key: "all", label: "Tất cả" },
                  { key: "Draft", label: "Nháp" },
                  { key: "PendingApproval", label: "Chờ duyệt" },
                  { key: "Active", label: "Hoạt động" },
                  { key: "Inactive", label: "Đang ngưng" },
                  { key: "Rejected", label: "Từ chối" },
                ].map(t => (
                  <button
                    key={t.key}
                    onClick={() => setFilter(t.key)}
                    className={`px-4 py-2 rounded-xl text-sm font-semibold transition ${filter === t.key ? "bg-orange-500 text-white shadow-sm" : "bg-white text-slate-600 hover:bg-slate-50 ring-1 ring-slate-200"}`}
                  >
                    {t.label}
                  </button>
                ))}
              </div>
              
              <div className="flex gap-2 w-full md:w-auto">
                <div className="relative flex-1 md:w-64">
                  <svg className="absolute left-3 top-1/2 -translate-y-1/2" width="18" height="18" fill="none" stroke="#9ca3af" strokeWidth="2" viewBox="0 0 24 24">
                    <circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/>
                  </svg>
                  <input
                    type="text"
                    placeholder="Tìm trạm..."
                    className="w-full pl-10 pr-4 py-2 rounded-xl border border-slate-200 focus:outline-none focus:border-orange-500 text-sm"
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                  />
                </div>
                <div className="flex bg-slate-200 rounded-xl p-1">
                  <button
                    onClick={() => setViewMode("list")}
                    className={`px-3 py-1.5 rounded-lg text-sm font-semibold transition-colors flex items-center gap-1 border-none ${viewMode === "list" ? "bg-white text-slate-800 shadow-sm" : "text-slate-500"}`}
                  >
                    <svg width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24"><path d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01"/></svg>
                    Danh sách
                  </button>
                  <button
                    onClick={() => setViewMode("map")}
                    className={`px-3 py-1.5 rounded-lg text-sm font-semibold transition-colors flex items-center gap-1 border-none ${viewMode === "map" ? "bg-white text-slate-800 shadow-sm" : "text-slate-500"}`}
                  >
                    <svg width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>
                    Bản đồ
                  </button>
                </div>
              </div>
            </div>

            {viewMode === "list" ? (
              <div className="space-y-4">
                {stations.filter(s => {
                  if (filter !== "all") {
                    const dKey = s.approvalStatus === "Approved" ? (s.operationalStatus || "Approved") : s.approvalStatus;
                    if (dKey !== filter && s.approvalStatus !== filter) return false;
                  }
                  if (search.trim()) {
                    const q = search.toLowerCase();
                    return (s.name?.toLowerCase().includes(q) || s.address?.toLowerCase().includes(q));
                  }
                  return true;
                }).map((s) => {
                const st = s.approvalStatus === "Approved"
                  ? (statusConfig[s.operationalStatus] || statusConfig.Approved)
                  : (statusConfig[s.approvalStatus] || statusConfig.Draft);

                const isExpanded = expandedStation === s.id;
                const slots = s.chargingSlots || [];
                const layoutW = s.layoutWidth || 6;
                const layoutH = s.layoutHeight || 4;

                return (
                  <div key={s.id} className="rounded-2xl bg-white shadow-sm ring-1 ring-slate-200 overflow-hidden">
                    {/* Station header */}
                    <div
                      className="flex items-center gap-4 p-5 cursor-pointer hover:bg-slate-50 transition-colors"
                      onClick={() => { setExpandedStation(isExpanded ? null : s.id); setSelectedSlot(null); }}
                    >
                      <div className="w-14 h-14 rounded-xl bg-gradient-to-br from-orange-400 to-orange-600 flex items-center justify-center text-white text-2xl flex-shrink-0">
                        ⚡
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 mb-1">
                          <h3 className="text-lg font-bold text-slate-900 truncate">{s.name}</h3>
                          <span
                            className="text-xs font-semibold px-2.5 py-0.5 rounded-full flex-shrink-0"
                            style={{ color: st.color, background: st.bg }}
                          >
                            {st.icon} {st.label}
                          </span>
                        </div>
                        <p className="text-sm text-slate-500 truncate">{s.address}</p>
                      </div>
                      <div className="flex items-center gap-2 flex-shrink-0">
                        <span className="text-xs text-slate-500 bg-slate-100 px-2.5 py-1 rounded-lg">
                          {slots.length} trụ sạc
                        </span>
                        <span className="text-xs text-slate-400 bg-slate-50 px-2.5 py-1 rounded-lg">
                          {layoutW}×{layoutH}
                        </span>
                        <svg
                          width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#94a3b8" strokeWidth="2"
                          style={{ transform: isExpanded ? "rotate(180deg)" : "", transition: "transform .2s" }}
                        >
                          <polyline points="6 9 12 15 18 9" />
                        </svg>
                      </div>
                    </div>

                    {/* Expanded: Visual slot grid */}
                    {isExpanded && (
                      <div className="border-t border-slate-100 px-5 pb-5">
                        {/* Actions */}
                        <div className="flex gap-2 py-4 flex-wrap">
                          {/* Ban banner — hiện khi admin đã khóa trạm */}
                          {s.bannedUntil && (
                            <div style={{
                              width: "100%", marginBottom: 8,
                              background: "linear-gradient(135deg, #fef2f2, #fecaca)",
                              border: "1.5px solid #f87171",
                              borderRadius: 12, padding: "10px 14px",
                              display: "flex", alignItems: "center", gap: 10,
                              fontSize: 13, color: "#991b1b", fontWeight: 600,
                            }}>
                              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#ef4444" strokeWidth="2">
                                <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                                <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                              </svg>
                              Trạm đang bị khóa bởi Admin.
                            </div>
                          )}

                          {/* Edit station info button — ẩn khi bị ban */}
                          {!s.bannedUntil && (
                            <button
                              onClick={() => navigate(`/stations/edit/${s.id}`)}
                              className="px-4 py-2 text-sm font-semibold rounded-lg bg-slate-100 text-slate-700 hover:bg-slate-200 transition cursor-pointer flex items-center gap-1.5"
                            >
                              ✏️ Chỉnh sửa trạm
                            </button>
                          )}

                          {/* Gửi duyệt — ẩn khi bị ban */}
                          {!s.bannedUntil && (s.approvalStatus === "Draft" || s.approvalStatus === "Rejected") && (
                            <button
                              onClick={() => handleSubmitForApproval(s.id)}
                              disabled={actionLoading === s.id}
                              className="px-4 py-2 text-sm font-semibold rounded-lg bg-blue-500 text-white hover:bg-blue-600 transition disabled:opacity-50 cursor-pointer"
                            >
                              📤 Gửi duyệt
                            </button>
                          )}

                          {/* Bật/Tắt trạm — ẩn khi bị ban */}
                          {!s.bannedUntil && s.approvalStatus === "Approved" && (
                            <button
                              onClick={async () => {
                                const newStatus = s.operationalStatus === "Active" ? "Inactive" : "Active";
                                const actionLabel = newStatus === "Inactive" ? "Tắt" : "Bật";
                                if (!(await showConfirm(
                                  newStatus === "Inactive"
                                    ? `Tắt trạm sạc "${s.name}"?\n\n⚠️ Nếu còn booking chưa phục vụ, hệ thống sẽ báo lỗi.`
                                    : `Bật lại trạm sạc "${s.name}"?`,
                                  `Xác nhận ${actionLabel} trạm`
                                ))) return;
                                setActionLoading(s.id);
                                try {
                                  await stationApi.updateStatus(s.id, newStatus);
                                  fetchStations();
                                  showToast.success(`${actionLabel} trạm thành công!`);
                                } catch (err) {
                                  const msg = err.message || "Lỗi đổi trạng thái";
                                  if (msg.toLowerCase().includes("booking") || msg.includes("400")) {
                                    showToast.error("⚠️ Không thể tắt trạm! Vẫn còn booking chưa hoàn thành tại trạm này. Vui lòng chờ hết booking rồi thử lại.");
                                  } else {
                                    showToast.error(msg);
                                  }
                                } finally {
                                  setActionLoading(null);
                                }
                              }}
                              disabled={actionLoading === s.id}
                              className={`px-4 py-2 text-sm font-semibold rounded-lg transition disabled:opacity-50 cursor-pointer ${s.operationalStatus === "Active"
                                ? "bg-amber-50 text-amber-600 hover:bg-amber-100"
                                : "bg-green-50 text-green-600 hover:bg-green-100"
                                }`}
                            >
                              {s.operationalStatus === "Active" ? "⏸️ Tắt trạm" : "▶️ Bật trạm"}
                            </button>
                          )}
                        </div>

                        <div className="flex gap-6 flex-col lg:flex-row">
                          {/* LEFT: Visual grid */}
                          <div className="flex-1">
                            <h4 className="text-base font-bold text-slate-800 mb-3 flex items-center gap-2">
                              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><rect x="3" y="3" width="18" height="18" rx="2" /><line x1="3" y1="9" x2="21" y2="9" /><line x1="9" y1="3" x2="9" y2="21" /></svg>
                              Mặt bằng trạm sạc ({layoutW}×{layoutH})
                            </h4>

                            {/* Grid */}
                            <div
                              style={{
                                display: "grid",
                                gridTemplateColumns: `repeat(${layoutW}, 1fr)`,
                                gridTemplateRows: `repeat(${layoutH}, 1fr)`,
                                gap: 4,
                                background: "#f1f5f9",
                                borderRadius: 16,
                                padding: 12,
                                border: "2px solid #e2e8f0",
                                aspectRatio: `${layoutW}/${layoutH}`,
                                maxWidth: 600,
                              }}
                            >
                              {Array.from({ length: layoutH }).map((_, row) =>
                                Array.from({ length: layoutW }).map((_, col) => {
                                  const x = col + 1;
                                  const y = row + 1;
                                  const slot = slots.find(
                                    (sl) => sl.positionX === x && sl.positionY === y
                                  );

                                  if (slot) {
                                    const isOccupied  = occupiedSlots.has(slot.id);
                                    const isCheckedIn = checkedInSlots.has(slot.id);
                                    const displayStatus = isOccupied ? "Occupied" : isCheckedIn ? "CheckedIn" : slot.status;
                                    const sc = slotColors[displayStatus] || slotColors[slot.status] || slotColors.Inactive;
                                    const isSelected = selectedSlot === slot.id;
                                    return (
                                      <button
                                        key={`${x}-${y}`}
                                        onClick={() => setSelectedSlot(isSelected ? null : slot.id)}
                                        style={{
                                          background: sc.bg,
                                          color: sc.text,
                                          borderRadius: 10,
                                          border: isSelected ? "3px solid #1e293b" : `2px solid ${sc.border}`,
                                          display: "flex",
                                          flexDirection: "column",
                                          alignItems: "center",
                                          justifyContent: "center",
                                          cursor: "pointer",
                                          transition: "all .15s",
                                          transform: isSelected ? "scale(1.08)" : "scale(1)",
                                          boxShadow: isSelected ? "0 4px 12px rgba(0,0,0,0.2)" : "0 1px 3px rgba(0,0,0,0.1)",
                                          minHeight: 48,
                                          padding: "4px 2px",
                                        }}
                                        title={`${slot.slotName} — ${sc.label}`}
                                      >
                                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                                          <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
                                        </svg>
                                        <span style={{ fontSize: 9, fontWeight: 700, marginTop: 1, lineHeight: 1 }}>
                                          {slot.slotName}
                                        </span>
                                      </button>
                                    );
                                  }

                                  // Empty cell — click to add slot
                                  return (
                                    <button
                                      key={`${x}-${y}`}
                                      onClick={async () => {
                                        const slotName = `${String.fromCharCode(64 + y)}${x}`;
                                        setActionLoading(s.id);
                                        try {
                                          await slotApi.create(s.id, { slotName, positionX: x, positionY: y });
                                          fetchStations();
                                        } catch (err) {
                                          showToast.error("Lỗi thêm trụ: " + (err.message || "Không rõ"));
                                        } finally {
                                          setActionLoading(null);
                                        }
                                      }}
                                      disabled={actionLoading === s.id}
                                      style={{
                                        background: "#e2e8f0",
                                        borderRadius: 8,
                                        border: "2px dashed #cbd5e1",
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "center",
                                        cursor: "pointer",
                                        opacity: 0.5,
                                        minHeight: 48,
                                        transition: "all .15s",
                                      }}
                                      className="hover:opacity-80 hover:border-orange-400 hover:bg-orange-50"
                                      title={`Thêm trụ tại ${String.fromCharCode(64 + y)}${x}`}
                                    >
                                      <span style={{ fontSize: 16, color: "#94a3b8" }}>+</span>
                                    </button>
                                  );
                                })
                              )}
                            </div>

                            {/* Legend */}
                            <div className="flex gap-3 mt-3 flex-wrap">
                              {Object.entries(slotColors).filter(([k]) => ["Active", "Occupied", "CheckedIn", "Booked", "Reserved", "Maintenance", "Inactive"].includes(k)).map(([key, val]) => (
                                <div key={key} className="flex items-center gap-1.5 text-xs text-slate-600">
                                  <div style={{ width: 10, height: 10, borderRadius: 3, background: val.bg }} />
                                  {val.label}
                                </div>
                              ))}
                              <div className="flex items-center gap-1.5 text-xs text-slate-400">
                                <div style={{ width: 10, height: 10, borderRadius: 3, background: "#e2e8f0", border: "1px dashed #cbd5e1" }} />
                                Trống
                              </div>
                            </div>
                          </div>

                          {/* RIGHT: Selected slot info */}
                          <div className="lg:w-[280px] flex-shrink-0">
                            <h4 className="text-base font-bold text-slate-800 mb-3">Thông tin trụ</h4>
                            {selectedSlot ? (
                              (() => {
                                const slot = slots.find((sl) => sl.id === selectedSlot);
                                if (!slot) return <p className="text-sm text-slate-400 italic">Không tìm thấy.</p>;
                                // Tính displayStatus: ưu tiên occupiedSlots (active session real-time)
                                const isCurrentlyOccupied = occupiedSlots.has(slot.id);
                                const isCurrentlyCheckedIn = checkedInSlots.has(slot.id);
                                const displayStatus = isCurrentlyOccupied ? "Occupied" : isCurrentlyCheckedIn ? "CheckedIn" : slot.status;
                                const sc = slotColors[displayStatus] || slotColors[slot.status] || slotColors.Inactive;
                                // Chỉ cho phép đổi trạng thái khi slot không bận
                                const isSlotBusy = displayStatus === "Occupied" || displayStatus === "CheckedIn" || slot.status === "Booked";
                                return (
                                  <div className="bg-slate-50 rounded-xl p-4 border border-slate-200 space-y-3">
                                    <div className="flex items-center gap-2">
                                      <div style={{ width: 36, height: 36, borderRadius: 10, background: sc.bg, display: "flex", alignItems: "center", justifyContent: "center" }}>
                                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth="2.5"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" /></svg>
                                      </div>
                                      <div>
                                        <div className="font-bold text-slate-900">{slot.slotName}</div>
                                        <span className="text-xs font-semibold px-2 py-0.5 rounded-full" style={{ color: sc.border, background: `${sc.bg}44`, border: `1px solid ${sc.border}55` }}>
                                          {sc.label}
                                        </span>
                                      </div>
                                    </div>
                                    <InfoRow label="Vị trí" value={`${String.fromCharCode(64 + Number(slot.positionY))}${slot.positionX}`} />
                                    {slot.qrCodeToken && (
                                      <div className="pt-1">
                                        <p className="text-xs text-slate-500 mb-2">Mã QR</p>
                                        <div className="bg-white rounded-lg p-3 border border-slate-200 flex items-center justify-center">
                                          <QRCodeSVG value={slot.qrCodeToken} size={140} />
                                        </div>
                                      </div>
                                    )}

                                    {/* Slot status controls */}
                                    {s.approvalStatus === "Approved" && (
                                      <div className="pt-2 border-t border-slate-200">
                                        <p className="text-xs font-medium text-slate-600 mb-2">Đổi trạng thái</p>
                                        {isSlotBusy ? (
                                          <div style={{ fontSize: 11, color: "#64748b", background: "#f1f5f9", borderRadius: 8, padding: "7px 10px", fontStyle: "italic" }}>
                                            {displayStatus === "Occupied"
                                              ? "⚡ Đang sạc — không thể đổi trạng thái"
                                              : displayStatus === "CheckedIn"
                                              ? "📍 Đã check-in, chưa sạc — không thể đổi trạng thái"
                                              : "📅 Đang có lịch đặt — không thể đổi trạng thái"}
                                          </div>
                                        ) : (
                                          <div className="flex gap-1.5 flex-wrap">
                                            {["Active", "Inactive", "Maintenance"].map((key) => {
                                              const isCurrentStatus = slot.status === key;
                                              const labels = { Active: "Hoạt động", Inactive: "Ngưng", Maintenance: "Bảo trì" };
                                              const colors = { Active: "#22c55e", Inactive: "#94a3b8", Maintenance: "#f97316" };

                                              // BE Enum mapping
                                              const statusValues = { Active: 0, Inactive: 1, Maintenance: 2 };

                                              return (
                                                <button
                                                  key={key}
                                                  disabled={isCurrentStatus || actionLoading === slot.id}
                                                  onClick={async () => {
                                                    setActionLoading(slot.id);
                                                    try {
                                                      await slotApi.updateStatus(s.id, slot.id, { status: statusValues[key] });
                                                      fetchStations();
                                                    } catch (err) {
                                                      showToast.error("Lỗi: " + (err.message || "Không rõ"));
                                                    } finally {
                                                      setActionLoading(null);
                                                    }
                                                  }}
                                                  className="px-2.5 py-1 text-[10px] font-semibold rounded-lg border cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed transition"
                                                  style={{
                                                    color: isCurrentStatus ? "#fff" : colors[key],
                                                    background: isCurrentStatus ? colors[key] : "transparent",
                                                    borderColor: colors[key],
                                                  }}
                                                >
                                                  {labels[key]}
                                                </button>
                                              );
                                            })}
                                          </div>
                                        )}
                                      </div>
                                    )}
                                  </div>
                                );
                              })()
                            ) : (
                              <div className="bg-slate-50 rounded-xl p-6 border border-dashed border-slate-300 text-center">
                                <div className="text-3xl mb-2 opacity-40">👆</div>
                                <p className="text-sm text-slate-400">Nhấn vào một trụ sạc trên mặt bằng để xem chi tiết</p>
                              </div>
                            )}
                          </div>
                        </div>

                        {/* Station-level pricing */}
                        <div className="mt-4 pt-4 border-t border-slate-200">
                          <StationPricingPanel stationId={s.id} pricingTiers={s.pricingTiers || []} operatingHours={s.operatingHours || []} onSaved={fetchStations} />
                        </div>

                        {/* Unavailable dates */}
                        <div className="mt-4 pt-4 border-t border-slate-200">
                          <UnavailableDatesPanel stationId={s.id} />
                        </div>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          ) : (
            <div className="map-view-container">
              <button
                onClick={(e) => {
                  e.preventDefault();
                  if (userPos) setFlyTarget(userPos);
                  else
                    navigator.geolocation?.getCurrentPosition(
                      (pos) => {
                        const p = [pos.coords.latitude, pos.coords.longitude];
                        setUserPos(p);
                        setFlyTarget(p);
                      },
                      () => showToast.error("Không thể lấy vị trí của bạn"),
                      { enableHighAccuracy: true, timeout: 8000 }
                    );
                }}
                style={{
                  position: "absolute", bottom: 20, right: 20, zIndex: 1000, 
                  background: "white", width: 44, height: 44, borderRadius: "50%",
                  display: "flex", alignItems: "center", justifyContent: "center",
                  boxShadow: "0 2px 10px rgba(0,0,0,0.1)", border: "none", cursor: "pointer", color: "#3b82f6"
                }}
                title="Vị trí của tôi"
              >
                <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                  <circle cx="12" cy="12" r="3" />
                  <path d="M12 2v4M12 18v4M2 12h4M18 12h4" />
                </svg>
              </button>
              <MapContainer
                center={userPos || [21.0285, 105.8542]}
                zoom={14}
                style={{ width: "100%", height: "100%" }}
                zoomControl={false}
              >
                <TileLayer
                  attribution='&copy; <a href="https://www.google.com/maps">Google Maps</a>'
                  url="https://mt1.google.com/vt/lyrs=m&x={x}&y={y}&z={z}"
                />
                {flyTarget && <FlyTo center={flyTarget} zoom={15} />}
                {userPos && <Marker position={userPos} icon={new L.DivIcon({
                  html: `<div style="width:20px;height:20px;border-radius:50%;background:#3b82f6;border:3px solid #fff;box-shadow:0 0 0 6px rgba(59,130,246,.25),0 2px 8px rgba(0,0,0,.3);"></div>`,
                  className: "", iconSize: [20, 20], iconAnchor: [10, 10]
                })} />}

                {stations.filter(s => {
                  if (filter !== "all") {
                    const dKey = s.approvalStatus === "Approved" ? (s.operationalStatus || "Approved") : s.approvalStatus;
                    if (dKey !== filter && s.approvalStatus !== filter) return false;
                  }
                  if (search.trim()) {
                    const q = search.toLowerCase();
                    return (s.name?.toLowerCase().includes(q) || s.address?.toLowerCase().includes(q));
                  }
                  if (!s.latitude || !s.longitude) return false;
                  return true;
                }).map((s) => {
                  const dKey = s.approvalStatus === "Approved" ? (s.operationalStatus || "Approved") : s.approvalStatus;
                  const st = statusConfig[dKey] || statusConfig.Draft;
                  const icon = mapIcons[dKey] || mapIcons.Draft;
                  
                  return (
                    <Marker
                      key={s.id}
                      position={[s.latitude, s.longitude]}
                      icon={icon}
                    >
                      <Popup className="custom-popup" maxWidth={280}>
                        <div style={{ fontFamily: "Inter, system-ui, sans-serif" }}>
                          <div style={{ padding: "16px 16px 12px", background: `linear-gradient(to right, ${st.bg}, white)` }}>
                            <div style={{ display: "flex", gap: "10px" }}>
                              <div style={{ flexShrink: 0, width: "36px", height: "36px", borderRadius: "10px", background: st.color, display: "flex", alignItems: "center", justifyContent: "center" }}>
                                <svg width="20" height="20" fill="none" stroke="white" strokeWidth="2.5" viewBox="0 0 24 24">
                                  <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
                                </svg>
                              </div>
                              <div style={{ flex: 1 }}>
                                <h3 style={{ margin: 0, fontSize: "16px", fontWeight: 700, color: "#1e293b", lineHeight: 1.2, display: "-webkit-box", WebkitLineClamp: 2, WebkitBoxOrient: "vertical", overflow: "hidden" }}>
                                  {s.name}
                                </h3>
                                <span style={{ display: "inline-block", marginTop: "4px", fontSize: "11px", fontWeight: 600, color: st.color, padding: "2px 8px", borderRadius: "99px", background: `${st.color}20` }}>
                                  {st.label}
                                </span>
                              </div>
                            </div>
                          </div>
                          
                          <div style={{ padding: "12px 16px" }}>
                            <div style={{ fontSize: "12px", color: "#64748b", marginBottom: "12px", display: "flex", alignItems: "flex-start", gap: "4px" }}>
                              <span style={{ fontSize: "14px" }}>📍</span>
                              <span style={{ flex: 1 }}>{s.address}</span>
                            </div>
                            
                            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "8px", marginBottom: "16px" }}>
                              <div style={{ background: "#f8fafc", padding: "8px", borderRadius: "8px", textAlign: "center", border: "1px solid #f1f5f9" }}>
                                <div style={{ fontSize: "16px", fontWeight: 700, color: "#3b82f6" }}>{s.chargingSlots?.length || 0}</div>
                                <div style={{ fontSize: "10px", color: "#64748b", marginTop: "2px" }}>Trụ sạc</div>
                              </div>
                              <div style={{ background: "#f8fafc", padding: "8px", borderRadius: "8px", textAlign: "center", border: "1px solid #f1f5f9" }}>
                                <div style={{ fontSize: "16px", fontWeight: 700, color: "#64748b" }}>{s.layoutWidth}×{s.layoutHeight}</div>
                                <div style={{ fontSize: "10px", color: "#64748b", marginTop: "2px" }}>Kích thước</div>
                              </div>
                            </div>
                            
                            <button
                              onClick={() => {
                                setViewMode("list");
                                setExpandedStation(s.id);
                                setTimeout(() => {
                                  window.scrollTo({ top: 300, behavior: 'smooth' });
                                }, 300);
                              }}
                              style={{ width: "100%", padding: "10px 0", background: "linear-gradient(135deg, #f97316, #ea580c)", color: "white", border: "none", borderRadius: "10px", fontWeight: 600, fontSize: "13px", cursor: "pointer", display: "flex", gap: "6px", justifyContent: "center", alignItems: "center" }}
                            >
                              Xem chi tiết / Chỉnh sửa
                              <svg width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24"><path d="M5 12h14M12 5l7 7-7 7"/></svg>
                            </button>
                          </div>
                        </div>
                      </Popup>
                    </Marker>
                  );
                })}
              </MapContainer>
            </div>
          )}
          </div>
        )}
      </div>

    </div>
  );
}

function InfoRow({ label, value, highlight }) {
  return (
    <div className="flex justify-between text-sm">
      <span className="text-slate-500">{label}</span>
      <span className={highlight ? "font-bold text-orange-600" : "font-semibold text-slate-800"}>{value}</span>
    </div>
  );
}

function StationPricingPanel({ stationId, pricingTiers: initialTiers, operatingHours = [], onSaved }) {
  const [showAddPricing, setShowAddPricing] = useState(false);
  const [newTier, setNewTier] = useState({ startTime: "00:00", endTime: "08:00", pricePerHour: "" });
  const [pricingLoading, setPricingLoading] = useState(false);
  const [pricingError, setPricingError] = useState("");
  const [pricingTiers, setPricingTiers] = useState(initialTiers);
  const [fetchingPricing, setFetchingPricing] = useState(false);

  useEffect(() => {
    let cancelled = false;
    async function loadPricing() {
      setFetchingPricing(true);
      try {
        const data = await stationPricingApi.getAll(stationId);
        if (!cancelled) setPricingTiers(Array.isArray(data) ? data : []);
      } catch {
        if (!cancelled) setPricingTiers(initialTiers);
      } finally {
        if (!cancelled) setFetchingPricing(false);
      }
    }
    loadPricing();
    return () => { cancelled = true; };
  }, [stationId]);

  function fmtTime(t) {
    if (!t) return "";
    return String(t).substring(0, 5);
  }

  const dayNames = ["CN", "T2", "T3", "T4", "T5", "T6", "T7"];

  // Tính giờ mở sớm nhất & đóng muộn nhất (để so sánh pricing)
  const openHours = operatingHours.filter(h => !h.isClosed);
  let earliestOpen = "23:59";
  let latestClose = "00:00";
  openHours.forEach(h => {
    const open = fmtTime(h.openTime);
    const close = fmtTime(h.closeTime);
    if (open < earliestOpen) earliestOpen = open;
    if (close > latestClose) latestClose = close;
  });
  // Nếu close = 00:00 → coi là 24:00 (cả ngày)
  if (latestClose === "00:00" && openHours.length > 0) latestClose = "23:59";

  // Check pricing coverage gaps
  function getCoverageWarnings() {
    if (openHours.length === 0 || pricingTiers.length === 0) return [];
    const warnings = [];
    const sorted = [...pricingTiers].sort((a, b) => fmtTime(a.startTime).localeCompare(fmtTime(b.startTime)));

    // Check gap trước khung giá đầu tiên
    const firstPricingStart = fmtTime(sorted[0].startTime);
    if (firstPricingStart > earliestOpen) {
      warnings.push(`Thiếu giá: ${earliestOpen} – ${firstPricingStart}`);
    }

    // Check gaps giữa các khung giá
    for (let i = 0; i < sorted.length - 1; i++) {
      const currentEnd = fmtTime(sorted[i].endTime);
      const nextStart = fmtTime(sorted[i + 1].startTime);
      if (nextStart > currentEnd) {
        warnings.push(`Thiếu giá: ${currentEnd} – ${nextStart}`);
      }
    }

    // Check gap sau khung giá cuối cùng
    const lastPricingEnd = fmtTime(sorted[sorted.length - 1].endTime);
    if (lastPricingEnd < latestClose) {
      warnings.push(`Thiếu giá: ${lastPricingEnd} – ${latestClose}`);
    }

    return warnings;
  }

  const coverageWarnings = getCoverageWarnings();

  function getLastEndTime() {
    if (pricingTiers.length === 0) return earliestOpen || "00:00";
    const sorted = [...pricingTiers].sort((a, b) => String(a.endTime).localeCompare(String(b.endTime)));
    return fmtTime(sorted[sorted.length - 1].endTime);
  }

  function openAddPricing() {
    const lastEnd = getLastEndTime();
    setNewTier({ startTime: lastEnd, endTime: "", pricePerHour: "" });
    setPricingError("");
    setShowAddPricing(true);
  }

  async function reloadPricing() {
    try {
      const data = await stationPricingApi.getAll(stationId);
      setPricingTiers(Array.isArray(data) ? data : []);
    } catch { /* keep current */ }
  }

  async function handleAddPricing() {
    setPricingError("");
    if (!newTier.startTime || !newTier.endTime) { setPricingError("Vui lòng chọn giờ bắt đầu và kết thúc!"); return; }
    if (!newTier.pricePerHour) { setPricingError("Vui lòng nhập giá!"); return; }

    // Validate định dạng giờ hợp lệ: HH:MM với giờ 00-23 và phút 00-59
    const timeRegex = /^([01]\d|2[0-3]):([0-5]\d)$/;
    if (!timeRegex.test(newTier.startTime) || !timeRegex.test(newTier.endTime)) {
      setPricingError("Thời gian không hợp lệ! Giờ từ 00–23, phút từ 00–59.");
      return;
    }

    if (newTier.endTime <= newTier.startTime) { setPricingError("Giờ kết thúc phải sau giờ bắt đầu!"); return; }

    const lastEnd = getLastEndTime();
    if (newTier.startTime < lastEnd) { setPricingError(`Giờ bắt đầu phải từ ${lastEnd} trở đi!`); return; }

    // Validate end time không vượt 23:59
    if (newTier.endTime > "23:59") {
      setPricingError("Giờ kết thúc không hợp lệ (tối đa 23:59)!");
      return;
    }

    // Validate không vượt quá giờ đóng cửa
    if (openHours.length > 0 && latestClose && latestClose !== "00:00" && newTier.endTime > latestClose) {
      setPricingError(`Giờ kết thúc vượt quá giờ đóng cửa (${latestClose})!`);
      return;
    }

    setPricingLoading(true);
    try {
      await stationPricingApi.create(stationId, {
        startTime: newTier.startTime,
        endTime: newTier.endTime,
        pricePerHour: Number(newTier.pricePerHour),
        priority: 0,
      });
      setShowAddPricing(false);
      setNewTier({ startTime: "00:00", endTime: "", pricePerHour: "" });
      setPricingError("");
      await reloadPricing();
      onSaved?.();
    } catch (err) {
      setPricingError("Lỗi thêm giá: " + (err.message || "Không rõ"));
    } finally {
      setPricingLoading(false);
    }
  }

  async function handleDeletePricing(pricingId) {
    if (!(await showConfirm("Xóa khung giờ giá này?", "Xác nhận xóa khung giờ"))) return;
    try {
      await stationPricingApi.delete(stationId, pricingId);
      await reloadPricing();
      onSaved?.();
    } catch (err) {
      setPricingError("Lỗi xóa: " + (err.message || "Không rõ"));
    }
  }

  return (
    <div>
      {/* Giờ hoạt động */}
      <div className="mb-3">
        <h4 className="text-sm font-bold text-slate-700 mb-1.5">🕐 Giờ hoạt động</h4>
        {operatingHours.length > 0 ? (
          <div className="flex flex-wrap gap-1.5">
            {operatingHours.map((h) => (
              <span
                key={h.dayOfWeek}
                className="text-[11px] px-2 py-1 rounded-lg border"
                style={{
                  background: h.isClosed ? "#fef2f2" : "#f0fdf4",
                  color: h.isClosed ? "#ef4444" : "#16a34a",
                  borderColor: h.isClosed ? "#fecaca" : "#bbf7d0",
                }}
              >
                <strong>{dayNames[h.dayOfWeek]}</strong>{" "}
                {h.isClosed ? "Nghỉ" : `${fmtTime(h.openTime)}–${fmtTime(h.closeTime)}`}
              </span>
            ))}
          </div>
        ) : (
          <p className="text-xs text-slate-400 italic">Chưa cài giờ hoạt động.</p>
        )}
      </div>

      {/* Coverage warnings */}
      {coverageWarnings.length > 0 && (
        <div className="mb-2 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
          <p className="text-[11px] font-bold text-amber-700 mb-1">⚠️ Chưa phủ hết giờ hoạt động:</p>
          {coverageWarnings.map((w, i) => (
            <p key={i} className="text-[11px] text-amber-600">• {w}</p>
          ))}
        </div>
      )}
      {openHours.length > 0 && pricingTiers.length > 0 && coverageWarnings.length === 0 && (
        <div className="mb-2 bg-green-50 border border-green-200 rounded-lg px-3 py-1.5">
          <p className="text-[11px] font-semibold text-green-700">✅ Giá đã phủ hết giờ hoạt động</p>
        </div>
      )}

      {/* Pricing tiers */}
      <div className="flex items-center justify-between mb-2">
        <h4 className="text-sm font-bold text-slate-700">⏰ Giá theo khung giờ</h4>
        {!showAddPricing && (
          <button onClick={openAddPricing} className="text-xs font-semibold text-orange-500 hover:text-orange-700 cursor-pointer">+ Thêm</button>
        )}
      </div>
      {fetchingPricing ? (
        <p className="text-xs text-slate-400 italic">Đang tải giá...</p>
      ) : pricingTiers.length > 0 ? (
        <div className="space-y-1">
          {pricingTiers.map((tier) => (
            <div key={tier.id} className="flex items-center justify-between bg-white rounded-lg px-3 py-2 border border-slate-100 text-xs">
              <div>
                <span className="text-slate-500">{fmtTime(tier.startTime)}–{fmtTime(tier.endTime)}</span>
                <span className="ml-2 font-bold text-amber-600">{tier.pricePerHour?.toLocaleString("vi-VN")}đ/h</span>
              </div>
              <button onClick={() => handleDeletePricing(tier.id)} className="text-red-400 hover:text-red-600 cursor-pointer text-xs" title="Xóa">✕</button>
            </div>
          ))}
        </div>
      ) : (
        <p className="text-xs text-slate-400 italic">Chưa có khung giờ giá. Nhấn "+ Thêm" để thêm.</p>
      )}

      {showAddPricing && (
        <div className="mt-3 bg-white rounded-xl p-4 border border-orange-200 space-y-3">
          <p className="text-xs font-bold text-slate-700 mb-1">➕ Thêm khung giờ giá</p>

          {/* Bắt đầu */}
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1.5">🕐 Giờ bắt đầu</label>
            <div className="flex justify-start">
              <TimePicker24h
                value={newTier.startTime}
                onChange={(v) => { setNewTier(p => ({ ...p, startTime: v })); setPricingError(""); }}
              />
            </div>
          </div>

          {/* Kết thúc */}
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1.5">🕑 Giờ kết thúc</label>
            <div className="flex justify-start">
              <TimePicker24h
                value={newTier.endTime}
                minAfter={newTier.startTime}
                onChange={(v) => { setNewTier(p => ({ ...p, endTime: v })); setPricingError(""); }}
              />
            </div>
          </div>

          {/* Giá */}
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1.5">💰 Giá/giờ (VND)</label>
            <input
              type="number"
              value={newTier.pricePerHour}
              onChange={(e) => { setNewTier(p => ({ ...p, pricePerHour: e.target.value })); setPricingError(""); }}
              placeholder="VD: 15000"
              className="h-9 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-orange-400 focus:ring-2 focus:ring-orange-100 transition"
            />
          </div>
          {pricingError && (
            <p className="text-xs text-red-500 font-medium bg-red-50 rounded-lg px-3 py-2">{pricingError}</p>
          )}

          <div className="flex gap-2 pt-1">
            <button
              onClick={handleAddPricing}
              disabled={pricingLoading}
              className="flex-1 h-9 rounded-lg bg-orange-500 text-white text-sm font-semibold hover:bg-orange-600 disabled:opacity-50 cursor-pointer transition"
            >
              {pricingLoading ? "Đang lưu..." : "✓ Thêm"}
            </button>
            <button
              onClick={() => { setShowAddPricing(false); setPricingError(""); }}
              className="flex-1 h-9 rounded-lg border border-slate-200 text-sm text-slate-500 hover:bg-slate-50 cursor-pointer transition"
            >
              Hủy
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

// ─────────────── UNAVAILABLE DATES PANEL ───────────────
function UnavailableDatesPanel({ stationId }) {
  // dates = array of { id, date: "YYYY-MM-DD", reason }
  const [dates, setDates] = useState([]);
  const [loading, setLoading] = useState(true);

  // Load danh sách ngày nghỉ
  const loadDates = useCallback(async () => {
    setLoading(true);
    try {
      const data = await stationApi.getUnavailableDates(stationId);
      setDates(Array.isArray(data) ? data : []);
    } catch {
      setDates([]);
    } finally {
      setLoading(false);
    }
  }, [stationId]);

  useEffect(() => { loadDates(); }, [loadDates]);

  // Ngày tối thiểu = hôm nay
  const todayStr = new Date().toISOString().split("T")[0];

  // Normalize date field
  function toDateStr(raw) {
    if (!raw) return "";
    return String(raw).substring(0, 10);
  }

  function formatDate(dateStr) {
    if (!dateStr) return "";
    const s = String(dateStr).substring(0, 10);
    const [y, m, d] = s.split("-");
    return `${d}/${m}/${y}`;
  }

  // Normalize & sort
  const sortedDates = [...dates].sort((a, b) => toDateStr(a.date).localeCompare(toDateStr(b.date)));
  const upcoming = sortedDates.filter(d => toDateStr(d.date) >= todayStr);
  const past = sortedDates.filter(d => toDateStr(d.date) < todayStr);

  return (
    <div>
      <div className="flex items-center justify-between mb-3">
        <h4 className="text-sm font-bold text-slate-700">🚫 Ngày không hoạt động</h4>
        <span className="text-xs text-slate-400">{upcoming.length} ngày sắp tới</span>
      </div>

      {/* Calendar Tick */}
      <UnavailableDateCalendar
        stationId={stationId}
        unavailableDates={dates}
        todayStr={todayStr}
        toDateStr={toDateStr}
        onAdded={loadDates}
        onRemoved={loadDates}
      />

      {loading ? (
        <p className="text-xs text-slate-400 italic">Đang tải...</p>
      ) : sortedDates.length === 0 ? (
        <p className="text-xs text-slate-400 italic">Chưa có ngày nghỉ nào được đặt.</p>
      ) : (
        <div className="space-y-1.5">
          {upcoming.length > 0 && (
            <>
              <p className="text-[10px] font-bold uppercase tracking-wider text-slate-400 mt-1">Sắp tới</p>
              {upcoming.map(item => {
                const ds = toDateStr(item.date);
                return (
                  <div key={item.id ?? ds} className="flex items-center justify-between bg-amber-50 border border-amber-200 rounded-lg px-3 py-1.5 text-xs">
                    <div>
                      <span className="text-amber-700 font-semibold">📅 {formatDate(ds)}</span>
                      {item.reason && <span className="ml-2 text-amber-500 italic">{item.reason}</span>}
                    </div>
                    <button
                      onClick={async () => {
                        if (!(await showConfirm(`Xóa ngày nghỉ ${formatDate(ds)}?`, "Xác nhận xóa"))) return;
                        try {
                          await stationApi.removeUnavailableDates(stationId, [item.id]);
                          await loadDates();
                          showToast.success("Đã xóa ngày nghỉ!");
                        } catch (err) { showToast.error(err.message || "Lỗi khi xóa ngày"); }
                      }}
                      className="text-red-400 hover:text-red-600 cursor-pointer ml-3"
                      title="Xóa ngày này"
                    >✕</button>
                  </div>
                );
              })}
            </>
          )}
          {past.length > 0 && (
            <>
              <p className="text-[10px] font-bold uppercase tracking-wider text-slate-400 mt-2">Đã qua</p>
              {past.map(item => {
                const ds = toDateStr(item.date);
                return (
                  <div key={item.id ?? ds} className="flex items-center justify-between bg-slate-50 border border-slate-200 rounded-lg px-3 py-1.5 text-xs opacity-60">
                    <span className="text-slate-500">📅 {formatDate(ds)}</span>
                  </div>
                );
              })}
            </>
          )}
        </div>
      )}
    </div>
  );
}

// ─────────────── UNAVAILABLE DATE CALENDAR ───────────────
function UnavailableDateCalendar({ stationId, unavailableDates, todayStr, toDateStr, onAdded, onRemoved }) {
  const today = new Date();
  const [viewYear, setViewYear] = useState(today.getFullYear());
  const [viewMonth, setViewMonth] = useState(today.getMonth()); // 0-based
  const [pendingDates, setPendingDates] = useState(new Set()); // dates ticked nhưng chưa lưu
  const [saving, setSaving] = useState(false);

  const unavailableSet = new Set(unavailableDates.map(d => toDateStr(d.date)));
  const unavailableIdMap = {};
  unavailableDates.forEach(d => { unavailableIdMap[toDateStr(d.date)] = d.id; });

  const DAYS = ["CN", "T2", "T3", "T4", "T5", "T6", "T7"];
  const MONTHS = ["Tháng 1","Tháng 2","Tháng 3","Tháng 4","Tháng 5","Tháng 6","Tháng 7","Tháng 8","Tháng 9","Tháng 10","Tháng 11","Tháng 12"];

  function getDaysInMonth(year, month) {
    return new Date(year, month + 1, 0).getDate();
  }

  function getFirstDayOfWeek(year, month) {
    return new Date(year, month, 1).getDay(); // 0=Sun
  }

  function toPadded(year, month, day) {
    return `${year}-${String(month + 1).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
  }

  function prevMonth() {
    if (viewMonth === 0) { setViewMonth(11); setViewYear(y => y - 1); }
    else setViewMonth(m => m - 1);
  }

  function nextMonth() {
    if (viewMonth === 11) { setViewMonth(0); setViewYear(y => y + 1); }
    else setViewMonth(m => m + 1);
  }

  function toggleDay(dateStr) {
    if (dateStr < todayStr) return; // không cho chọn ngày đã qua
    if (unavailableSet.has(dateStr)) {
      // Ngày đã lưu → click để xóa ngay
      const id = unavailableIdMap[dateStr];
      if (!id) return;
      showConfirm(`Xóa ngày nghỉ ${dateStr.split("-").reverse().join("/")}?`, "Xác nhận xóa").then(ok => {
        if (!ok) return;
        stationApi.removeUnavailableDates(stationId, [id])
          .then(() => { onRemoved(); showToast.success("Đã xóa ngày nghỉ!"); })
          .catch(err => showToast.error(err.message || "Lỗi xóa ngày"));
      });
      return;
    }
    setPendingDates(prev => {
      const next = new Set(prev);
      if (next.has(dateStr)) next.delete(dateStr);
      else next.add(dateStr);
      return next;
    });
  }

  async function savePending() {
    if (pendingDates.size === 0) return;
    const countToSave = pendingDates.size;
    setSaving(true);
    try {
      await stationApi.addUnavailableDates(stationId, [...pendingDates]);
      setPendingDates(new Set());
      await onAdded();
      showToast.success(`Đã thêm ${countToSave} ngày nghỉ!`);
    } catch (err) {
      showToast.error(err.message || "Lỗi khi thêm ngày");
    } finally {
      setSaving(false);
    }
  }

  const daysInMonth = getDaysInMonth(viewYear, viewMonth);
  const firstDow = getFirstDayOfWeek(viewYear, viewMonth);

  return (
    <div style={{ background: "#f8fafc", borderRadius: 14, padding: 14, border: "1px solid #e2e8f0", marginBottom: 12 }}>
      {/* Tiêu đề + navigation */}
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 10 }}>
        <button
          onClick={prevMonth}
          style={{ background: "none", border: "none", cursor: "pointer", color: "#64748b", fontSize: 18, padding: "2px 8px", borderRadius: 6, lineHeight: 1 }}
        >‹</button>
        <span style={{ fontWeight: 700, fontSize: 13, color: "#1e293b" }}>
          {MONTHS[viewMonth]} {viewYear}
        </span>
        <button
          onClick={nextMonth}
          style={{ background: "none", border: "none", cursor: "pointer", color: "#64748b", fontSize: 18, padding: "2px 8px", borderRadius: 6, lineHeight: 1 }}
        >›</button>
      </div>

      {/* Header ngày trong tuần */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(7, 1fr)", gap: 2, marginBottom: 4 }}>
        {DAYS.map((d, i) => (
          <div key={d} style={{ textAlign: "center", fontSize: 10, fontWeight: 700, color: i === 0 ? "#f87171" : "#94a3b8", padding: "2px 0" }}>{d}</div>
        ))}
      </div>

      {/* Grid ngày */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(7, 1fr)", gap: 2 }}>
        {/* Ô trống đầu */}
        {Array.from({ length: firstDow }).map((_, i) => (
          <div key={`empty-${i}`} />
        ))}

        {/* Các ngày trong tháng */}
        {Array.from({ length: daysInMonth }).map((_, i) => {
          const day = i + 1;
          const dateStr = toPadded(viewYear, viewMonth, day);
          const isPast = dateStr < todayStr;
          const isToday = dateStr === todayStr;
          const isUnavailable = unavailableSet.has(dateStr);
          const isPending = pendingDates.has(dateStr);
          const isSunday = new Date(viewYear, viewMonth, day).getDay() === 0;

          let bg = "transparent";
          let color = isSunday && !isPast ? "#f87171" : "#1e293b";
          let border = "1px solid transparent";

          if (isPast) { color = "#cbd5e1"; }
          else if (isUnavailable) { bg = "#ef4444"; color = "#fff"; border = "1px solid #dc2626"; }
          else if (isPending) { bg = "#f97316"; color = "#fff"; border = "1px solid #ea580c"; }
          else if (isToday) { border = "1.5px solid #f97316"; color = color === "#f87171" ? "#f87171" : "#f97316"; }

          return (
            <button
              key={dateStr}
              onClick={() => toggleDay(dateStr)}
              disabled={isPast}
              title={
                isPast ? ""
                  : isUnavailable ? `Đã đặt nghỉ – Click để xóa`
                    : isPending ? `Đang chọn – Click để bỏ chọn`
                      : `Click để chọn ngày nghỉ`
              }
              style={{
                background: bg, color, border,
                cursor: isPast ? "default" : "pointer",
                borderRadius: 7, fontSize: 11, fontWeight: isToday ? 800 : 600,
                padding: "5px 2px", textAlign: "center",
                transition: "all 0.1s",
                opacity: isPast ? 0.3 : 1,
                position: "relative",
              }}
            >
              {day}
              {isUnavailable && (
                <span style={{ position: "absolute", top: 0, right: 1, fontSize: 6, lineHeight: 1, opacity: 0.85 }}>✕</span>
              )}
              {isPending && (
                <span style={{ position: "absolute", top: 0, right: 1, fontSize: 6, lineHeight: 1, opacity: 0.85 }}>✓</span>
              )}
            </button>
          );
        })}
      </div>

      {/* Legend + Lưu */}
      <div style={{ marginTop: 10, display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 6 }}>
        <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
          <div style={{ display: "flex", alignItems: "center", gap: 4, fontSize: 10, color: "#64748b" }}>
            <div style={{ width: 10, height: 10, borderRadius: 3, background: "#ef4444" }} />
            <span>Ngày nghỉ (click xóa)</span>
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: 4, fontSize: 10, color: "#64748b" }}>
            <div style={{ width: 10, height: 10, borderRadius: 3, background: "#f97316" }} />
            <span>Đang chọn ({pendingDates.size})</span>
          </div>
        </div>
        {pendingDates.size > 0 && (
          <button
            onClick={savePending}
            disabled={saving}
            style={{
              background: "#f97316", color: "#fff", border: "none", borderRadius: 8,
              padding: "5px 14px", fontSize: 11, fontWeight: 700, cursor: "pointer",
              opacity: saving ? 0.6 : 1,
            }}
          >
            {saving ? "Đang lưu..." : `✓ Lưu ${pendingDates.size} ngày`}
          </button>
        )}
      </div>
    </div>
  );
}
