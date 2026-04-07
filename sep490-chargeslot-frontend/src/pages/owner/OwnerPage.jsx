import { useState, useEffect, useCallback } from "react";
import { useNavigate, Link } from "react-router-dom";
import { showConfirm } from "@/components/ConfirmDialog";
import { stationApi, slotApi, stationPricingApi } from "@/services/api";
import { QRCodeSVG } from "qrcode.react";
import { showToast } from "@/components/Toast";
import TimePicker24h from "@/components/TimePicker24h";

const statusConfig = {
  Draft: { label: "Nháp", color: "#6b7280", bg: "#f3f4f6", icon: "📝" },
  PendingApproval: { label: "Chờ duyệt", color: "#f59e0b", bg: "#fffbeb", icon: "⏳" },
  Approved: { label: "Đã duyệt", color: "#22c55e", bg: "#f0fdf4", icon: "✅" },
  Rejected: { label: "Bị từ chối", color: "#ef4444", bg: "#fef2f2", icon: "❌" },
  Active: { label: "Hoạt động", color: "#22c55e", bg: "#f0fdf4", icon: "⚡" },
  Inactive: { label: "Ngưng", color: "#6b7280", bg: "#f3f4f6", icon: "⏸️" },
};

const slotColors = {
  Active: { bg: "#22c55e", text: "#fff", border: "#16a34a", label: "Hoạt động" },
  Inactive: { bg: "#94a3b8", text: "#fff", border: "#64748b", label: "Ngưng" },
  Maintenance: { bg: "#f97316", text: "#fff", border: "#ea580c", label: "Bảo trì" },
  Available: { bg: "#22c55e", text: "#fff", border: "#16a34a", label: "Trống" },
  Occupied: { bg: "#ef4444", text: "#fff", border: "#dc2626", label: "Đang dùng" },
};

export default function OwnerPage() {
  const navigate = useNavigate();
  const [stations, setStations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [expandedStation, setExpandedStation] = useState(null);
  const [actionLoading, setActionLoading] = useState(null);
  const [selectedSlot, setSelectedSlot] = useState(null);
  const [filter, setFilter] = useState("all");

  function fetchStations() {
    setLoading(true);
    stationApi.getAll()
      .then((data) => setStations(Array.isArray(data) ? data : []))
      .catch(() => setStations([]))
      .finally(() => setLoading(false));
  }

  useEffect(() => { fetchStations(); }, []);

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
            <div className="mb-4 flex flex-wrap gap-2">
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
            
            <div className="space-y-4">
              {stations.filter(s => {
                if (filter === "all") return true;
                const dKey = s.approvalStatus === "Approved" ? (s.operationalStatus || "Approved") : s.approvalStatus;
                return dKey === filter || s.approvalStatus === filter;
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
                                  const sc = slotColors[slot.status] || slotColors.Inactive;
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
                            {Object.entries(slotColors).filter(([k]) => ["Active", "Occupied", "Maintenance", "Inactive"].includes(k)).map(([key, val]) => (
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
                              const sc = slotColors[slot.status] || slotColors.Inactive;
                              return (
                                <div className="bg-slate-50 rounded-xl p-4 border border-slate-200 space-y-3">
                                  <div className="flex items-center gap-2">
                                    <div style={{ width: 36, height: 36, borderRadius: 10, background: sc.bg, display: "flex", alignItems: "center", justifyContent: "center" }}>
                                      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth="2.5"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" /></svg>
                                    </div>
                                    <div>
                                      <div className="font-bold text-slate-900">{slot.slotName}</div>
                                      <span className="text-xs font-semibold px-2 py-0.5 rounded-full" style={{ color: sc.text, background: sc.bg }}>
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
                                    </div>
                                  )}

                                  {/* Delete slot */}
                                  <button
                                    onClick={async () => {
                                      if (!(await showConfirm(`Xóa trụ sạc ${slot.slotName}?`, "Xác nhận xóa trụ sạc"))) return;
                                      setActionLoading(slot.id);
                                      try {
                                        await slotApi.delete(s.id, slot.id);
                                        setSelectedSlot(null);
                                        fetchStations();
                                        showToast.success(`Trụ sạc ${slot.slotName} đã bị xóa.`);
                                      } catch (err) {
                                        const errMsg = err.message || "";
                                        if (s.approvalStatus !== "Draft" && (errMsg.includes("500") || errMsg.includes("400") || errMsg.toLowerCase().includes("booking"))) {
                                          showToast.error("⚠️ Không thể xóa trụ sạc vì đã từng có giao dịch liên quan! Xin hãy dùng tính năng 'Đổi trạng thái' sang 'Ngưng' để ẩn trụ này thay vì xóa.");
                                        } else {
                                          showToast.error("Lỗi xóa: " + (errMsg || "Không rõ"));
                                        }
                                      } finally {
                                        setActionLoading(null);
                                      }
                                    }}
                                    disabled={actionLoading === slot.id}
                                    className="w-full text-xs font-semibold text-red-500 hover:text-red-700 hover:bg-red-50 rounded-lg py-1.5 transition cursor-pointer disabled:opacity-50"
                                  >
                                    🗑️ Xóa trụ sạc
                                  </button>
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
                    </div>
                  )}
                </div>
              );
            })}
          </div>
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
        <div className="mt-2 bg-white rounded-lg p-3 border border-orange-200 space-y-2">
          <div className="flex gap-2">
            <div className="flex-1">
              <label className="block text-[10px] text-slate-400 mb-0.5">Bắt đầu</label>
              <TimePicker24h
                value={newTier.startTime}
                onChange={(v) => { setNewTier(p => ({ ...p, startTime: v })); setPricingError(""); }}
              />
            </div>
            <div className="flex-1">
              <label className="block text-[10px] text-slate-400 mb-0.5">Kết thúc</label>
              <TimePicker24h
                value={newTier.endTime}
                minAfter={newTier.startTime}
                onChange={(v) => { setNewTier(p => ({ ...p, endTime: v })); setPricingError(""); }}
              />
            </div>
          </div>
          <div>
            <label className="block text-[10px] text-slate-400 mb-0.5">Giá/h (VND)</label>
            <input type="number" value={newTier.pricePerHour} onChange={(e) => { setNewTier(p => ({ ...p, pricePerHour: e.target.value })); setPricingError(""); }}
              placeholder="15000"
              className="h-7 w-full rounded border border-slate-200 px-2 text-xs outline-none" />
          </div>
          {pricingError && (
            <p className="text-[10px] text-red-500 font-medium">{pricingError}</p>
          )}
          <div className="flex gap-1.5">
            <button onClick={handleAddPricing} disabled={pricingLoading}
              className="flex-1 h-7 rounded bg-orange-500 text-white text-[10px] font-semibold hover:bg-orange-600 disabled:opacity-50 cursor-pointer">
              {pricingLoading ? "..." : "Thêm"}
            </button>
            <button onClick={() => { setShowAddPricing(false); setPricingError(""); }}
              className="flex-1 h-7 rounded border border-slate-200 text-[10px] text-slate-500 hover:bg-slate-50 cursor-pointer">
              Hủy
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
