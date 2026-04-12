import { useMemo, useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { instance } from "@/lib/httpRequest";
import { showToast } from "@/components/Toast";
import { formatDateVN } from "@/utils/dateVN";
import { BanStatusBadge } from "@/components/BanStatusBadge";
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
  Approved: makeIcon("linear-gradient(135deg,#22c55e,#0f9d43)", "rgba(34,197,94,.35)"),
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


/* ─── API helpers ─── */
const adminStationApi = {
  /** Lấy tất cả trạm (có filter + phân trang) */
  getAll: async (status = "", search = "", page = 1, pageSize = 50) => {
    const params = new URLSearchParams();
    if (status) params.set("status", status);
    if (search) params.set("search", search);
    params.set("page", String(page));
    params.set("pageSize", String(pageSize));
    const { data } = await instance.get(`/admin/stations?${params.toString()}`);
    // BE trả về PagedResultDto { items, totalCount, page, pageSize }
    // Loại bỏ Draft (Owner chưa submit) — Admin chỉ thấy trạm đã được gửi duyệt
    const raw = Array.isArray(data) ? data : (data?.items ?? []);
    return raw.filter(s => s.approvalStatus !== "Draft");
  },
  /** Chỉ lấy trạm pending (giữ lại để tương thích) */
  getPending: async () => {
    const { data } = await instance.get("/admin/stations/pending");
    return data;
  },
  review: async (stationId, body) => {
    const { data } = await instance.post(`/admin/stations/${stationId}/review`, body);
    return data;
  },
  /** Toggle khoá/mở trạm (Admin manual ban) */
  toggleBan: async (stationId) => {
    const { data } = await instance.patch(`/admin/stations/${stationId}/toggle-ban`);
    return data; // { stationId, status: "Banned" | "Active" }
  },
};

function formatDate(dateStr) {
  return formatDateVN(dateStr) || "—";
}

function getStatusLabel(status) {
  switch (status) {
    case "PendingApproval": return "Chờ duyệt";
    case "Approved": return "Đã duyệt";
    case "Rejected": return "Từ chối";
    case "Draft": return "Bản nháp";
    default: return status;
  }
}

export default function ApproveStation() {
  const queryClient = useQueryClient();

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [ownerFilter, setOwnerFilter] = useState("");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [confirmAction, setConfirmAction] = useState(null);
  const [adminNote, setAdminNote] = useState("");
  const [viewMode, setViewMode] = useState("list");
  const [userPos, setUserPos] = useState(null);
  const [flyTarget, setFlyTarget] = useState([21.0285, 105.8542]);
  
  useEffect(() => {
    navigator.geolocation?.getCurrentPosition(
      (pos) => setUserPos([pos.coords.latitude, pos.coords.longitude]),
      () => {},
      { enableHighAccuracy: true, timeout: 8000 }
    );
  }, []);

  const { data: stations = [], isLoading, error } = useQuery({
    queryKey: ["admin-stations-all"],
    queryFn: () => adminStationApi.getAll(),
  });

  const reviewMutation = useMutation({
    mutationFn: ({ stationId, isApproved, adminNote }) =>
      adminStationApi.review(stationId, { isApproved, adminNote: adminNote || null }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-stations-all"] });
      setConfirmAction(null);
      setAdminNote("");
    },
    onError: (err) => {
      const msg = err?.response?.data?.error || err?.message || "Lỗi không xác định";
      showToast.error("Lỗi: " + msg);
    },
  });

  const ownerOptions = useMemo(() => {
    const seen = new Set();
    const list = [];
    stations.forEach((s) => {
      const name = s.ownerName || s.owner?.fullName || s.owner?.phoneNumber || "";
      if (name && !seen.has(name)) {
        seen.add(name);
        list.push(name);
      }
    });
    return list.sort((a, b) => a.localeCompare(b, "vi"));
  }, [stations]);

  const filtered = useMemo(() => {
    const keyword = search.trim().toLowerCase();
    return stations.filter((s) => {
      const matchSearch =
        !keyword ||
        s.name?.toLowerCase().includes(keyword) ||
        s.address?.toLowerCase().includes(keyword);
      const matchStatus =
        statusFilter === "ALL" || s.approvalStatus === statusFilter;
      const ownerName = s.ownerName || s.owner?.fullName || s.owner?.phoneNumber || "";
      const matchOwner = !ownerFilter || ownerName === ownerFilter;
      const createdDate = s.createdAt ? s.createdAt.slice(0, 10) : "";
      const matchFrom = !dateFrom || createdDate >= dateFrom;
      const matchTo = !dateTo || createdDate <= dateTo;
      return matchSearch && matchStatus && matchOwner && matchFrom && matchTo;
    });
  }, [stations, search, statusFilter, ownerFilter, dateFrom, dateTo]);

  const summary = useMemo(() => {
    return stations.reduce(
      (acc, s) => {
        acc.total += 1;
        if (s.approvalStatus === "PendingApproval") acc.pending += 1;
        else if (s.approvalStatus === "Approved") acc.approved += 1;
        else if (s.approvalStatus === "Rejected") acc.rejected += 1;
        return acc;
      },
      { total: 0, pending: 0, approved: 0, rejected: 0 }
    );
  }, [stations]);

  function askReview(station, isApproved) {
    setConfirmAction({ station, isApproved });
    setAdminNote("");
  }

  function confirmReview() {
    if (!confirmAction) return;
    reviewMutation.mutate({
      stationId: confirmAction.station.id,
      isApproved: confirmAction.isApproved,
      adminNote,
    });
  }

  function resetFilter() {
    setSearch("");
    setStatusFilter("ALL");
    setOwnerFilter("");
    setDateFrom("");
    setDateTo("");
  }

  if (isLoading) {
    return (
      <div className="cs-admin-page">
        <div style={{ textAlign: "center", paddingTop: 120 }}>
          <div className="cs-admin-table__spinner" style={{ margin: "0 auto 16px" }} />
          <p style={{ color: "#64748b", fontSize: 14 }}>Đang tải danh sách trạm...</p>
        </div>
        <style>{styles}</style>
      </div>
    );
  }

  if (error) {
    return (
      <div className="cs-admin-page">
        <div style={{ textAlign: "center", paddingTop: 120 }}>
          <p style={{ color: "#ef4444", fontSize: 16, marginBottom: 16 }}>❌ Lỗi tải dữ liệu: {error.message}</p>
          <button
            onClick={() => queryClient.invalidateQueries({ queryKey: ["admin-stations-all"] })}
            className="cs-admin-action-btn cs-admin-action-btn--activate"
          >
            Thử lại
          </button>
        </div>
        <style>{styles}</style>
      </div>
    );
  }

  return (
    <div className="cs-admin-page">
      {/* Page Header */}
      <div className="cs-admin-page__header">
        <div>
          <h1 className="cs-admin-page__title">Quản lý trạm sạc</h1>
          <p className="cs-admin-page__subtitle">
            Xem toàn bộ trạm, phê duyệt / từ chối yêu cầu đăng ký và ban/unban trạm vi phạm
          </p>
        </div>
      </div>

      {/* Stats Cards */}
      <div className="cs-admin-stats cs-admin-stats--4">
        <div className="cs-admin-stat-card">
          <div className="cs-admin-stat-card__icon cs-admin-stat-card__icon--total">
            <svg width="22" height="22" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
            </svg>
          </div>
          <div>
            <p className="cs-admin-stat-card__label">Tổng yêu cầu</p>
            <p className="cs-admin-stat-card__value">{summary.total}</p>
          </div>
        </div>
        <div className="cs-admin-stat-card">
          <div className="cs-admin-stat-card__icon cs-admin-stat-card__icon--pending">
            <svg width="22" height="22" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <div>
            <p className="cs-admin-stat-card__label">Chờ duyệt</p>
            <p className="cs-admin-stat-card__value" style={{ color: "#f59e0b" }}>{summary.pending}</p>
          </div>
        </div>
        <div className="cs-admin-stat-card">
          <div className="cs-admin-stat-card__icon cs-admin-stat-card__icon--active">
            <svg width="22" height="22" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <div>
            <p className="cs-admin-stat-card__label">Đã duyệt</p>
            <p className="cs-admin-stat-card__value" style={{ color: "#16a34a" }}>{summary.approved}</p>
          </div>
        </div>
        <div className="cs-admin-stat-card">
          <div className="cs-admin-stat-card__icon cs-admin-stat-card__icon--banned">
            <svg width="22" height="22" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <div>
            <p className="cs-admin-stat-card__label">Từ chối</p>
            <p className="cs-admin-stat-card__value" style={{ color: "#dc2626" }}>{summary.rejected}</p>
          </div>
        </div>
      </div>

      {/* Filter Bar */}
      <div className="cs-admin-filter" style={{ flexWrap: "wrap", gap: 10 }}>
        <div className="cs-admin-filter__search">
          <svg className="cs-admin-filter__search-icon" width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm theo tên trạm hoặc địa chỉ..."
            className="cs-admin-filter__input"
          />
        </div>
        {/* Owner combobox */}
        <select
          value={ownerFilter}
          onChange={(e) => setOwnerFilter(e.target.value)}
          className="cs-admin-filter__select"
          style={{ minWidth: 180 }}
        >
          <option value="">👤 Tất cả chủ trạm</option>
          {ownerOptions.map((name) => (
            <option key={name} value={name}>{name}</option>
          ))}
        </select>
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="cs-admin-filter__select"
        >
          <option value="ALL">Tất cả</option>
          <option value="PendingApproval">⏳ Chờ duyệt</option>
          <option value="Approved">✅ Đã duyệt</option>
          <option value="Rejected">❌ Từ chối</option>
        </select>
        {/* Date range */}
        <div style={{ display: "flex", alignItems: "center", gap: 6, flexShrink: 0 }}>
          <svg width="15" height="15" fill="none" stroke="#64748b" strokeWidth={2} viewBox="0 0 24 24">
            <rect x="3" y="4" width="18" height="18" rx="2" /><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/>
          </svg>
          <input type="date" value={dateFrom} onChange={(e) => setDateFrom(e.target.value)}
            className="cs-admin-filter__select" style={{ width: 140, cursor: "pointer" }} title="Từ ngày" />
          <span style={{ color: "#94a3b8", fontSize: 12 }}>—</span>
          <input type="date" value={dateTo} onChange={(e) => setDateTo(e.target.value)}
            className="cs-admin-filter__select" style={{ width: 140, cursor: "pointer" }} title="Đến ngày" />
        </div>
        <button onClick={resetFilter} className="cs-admin-filter__reset">
          <svg width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
          Xóa bộ lọc
        </button>
        <div style={{ display: "flex", background: "#f1f5f9", borderRadius: 12, padding: 4 }}>
          <button
            onClick={() => setViewMode("list")}
            style={{ padding: "6px 12px", border: "none", borderRadius: 8, fontSize: 13, fontWeight: 600, background: viewMode === "list" ? "white" : "transparent", color: viewMode === "list" ? "#1e293b" : "#64748b", boxShadow: viewMode === "list" ? "0 1px 3px rgba(0,0,0,0.1)" : "none", cursor: "pointer", transition: "all .2s" }}
          >
            Danh sách
          </button>
          <button
            onClick={() => setViewMode("map")}
            style={{ padding: "6px 12px", border: "none", borderRadius: 8, fontSize: 13, fontWeight: 600, background: viewMode === "map" ? "white" : "transparent", color: viewMode === "map" ? "#1e293b" : "#64748b", boxShadow: viewMode === "map" ? "0 1px 3px rgba(0,0,0,0.1)" : "none", cursor: "pointer", transition: "all .2s" }}
          >
            Bản đồ
          </button>
        </div>
      </div>

      {/* Table or Map */}
      {viewMode === "list" ? (
      <div className="cs-admin-table-wrap">
        <table className="cs-admin-table">
          <thead>
            <tr>
              <th>STT</th>
              <th>Tên trạm</th>
              <th>Chủ trạm</th>
              <th>Địa chỉ</th>
              <th>Số ổ sạc</th>
              <th>Vi phạm (AI)</th>
              <th>Ngày tạo</th>
              <th>Trạng thái</th>
              <th style={{ textAlign: "right" }}>Hành động</th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 ? (
              <tr>
                <td colSpan={9} className="cs-admin-table__empty">
                  <p>Không tìm thấy yêu cầu nào</p>
                </td>
              </tr>
            ) : (
              filtered.map((s, idx) => {
                const isPending = s.approvalStatus === "PendingApproval";
                // BE set bannedUntil = +100 năm khi khoá, null khi mở
                const isBanned = !!s.bannedUntil;
                return (
                  <tr key={s.id}>
                    <td className="cs-admin-table__id" style={{ fontWeight: 700, color: "#64748b" }}>{idx + 1}</td>
                    <td className="cs-admin-table__name">{s.name}</td>
                    <td style={{ fontSize: 13, color: "#374151" }}>{s.ownerName || s.owner?.fullName || s.owner?.phoneNumber || "—"}</td>
                    <td>{s.address}</td>
                    <td>{s.chargingSlots?.length || 0}</td>
                    <td>
                      <BanStatusBadge banCount={s.banCount ?? 0} bannedUntil={s.bannedUntil ?? null} />
                    </td>
                    <td>{formatDate(s.createdAt)}</td>
                    <td>
                      <span className={`cs-admin-status-badge cs-admin-status-badge--${s.approvalStatus === "PendingApproval" ? "pending" : s.approvalStatus === "Approved" ? "active" : s.approvalStatus === "Rejected" ? "banned" : "draft"}`}>
                        <span className="cs-admin-status-badge__dot" />
                        {getStatusLabel(s.approvalStatus)}
                      </span>
                    </td>
                    <td style={{ textAlign: "right" }}>
                      <div style={{ display: "flex", justifyContent: "flex-end", gap: 6, flexWrap: "wrap" }}>
                        <button
                          disabled={!isPending}
                          onClick={() => askReview(s, true)}
                          className={`cs-admin-action-btn ${isPending ? "cs-admin-action-btn--activate" : "cs-admin-action-btn--disabled"}`}
                        >
                          Phê duyệt
                        </button>
                        <button
                          disabled={!isPending}
                          onClick={() => askReview(s, false)}
                          className={`cs-admin-action-btn ${isPending ? "cs-admin-action-btn--ban" : "cs-admin-action-btn--disabled"}`}
                        >
                          Từ chối
                        </button>
                        </div>
                        {/* Toggle ban trạm */}
                        <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 6, marginTop: 4 }}>
                          <label
                            className="cs-toggle-switch"
                            title={isBanned ? "Trạm đang bị khoá — nhấn để mở khoá" : "Trạm hoạt động bình thường — nhấn để khoá"}
                          >
                            <input
                              type="checkbox"
                              checked={!isBanned}
                              onChange={async () => {
                                const previousStations = queryClient.getQueryData(["admin-stations-all"]);
                                const newIsBanned = !isBanned;

                                // Optimistic Update
                                queryClient.setQueryData(["admin-stations-all"], (old) => {
                                  if (!old) return old;
                                  return old.map(station =>
                                    station.id === s.id
                                      ? {
                                          ...station,
                                          bannedUntil: newIsBanned ? "2199-01-01T00:00:00Z" : null,
                                          operationalStatus: newIsBanned ? "Inactive" : "Active",
                                        }
                                      : station
                                  );
                                });

                                try {
                                  await adminStationApi.toggleBan(s.id);
                                  showToast.success(
                                    isBanned
                                      ? `✅ Đã mở khoá trạm ${s.name}`
                                      : `🔒 Đã khoá trạm ${s.name}`
                                  );
                                  queryClient.invalidateQueries({ queryKey: ["admin-stations-all"] });
                                } catch (err) {
                                  queryClient.setQueryData(["admin-stations-all"], previousStations);
                                  showToast.error(err?.response?.data?.message || "Thao tác thất bại.");
                                }
                              }}
                            />
                            <span className="cs-toggle-switch__track">
                              <span className="cs-toggle-switch__thumb" />
                            </span>
                            <span className="cs-toggle-switch__label">
                              {isBanned ? "Bị khoá" : "Hoạt động"}
                            </span>
                          </label>
                        </div>
                      </td>
                    </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
      ) : (
        <div className="map-view-container" style={{ height: 600, borderRadius: 16, border: "1px solid #e2e8f0", overflow: "hidden", position: "relative", zIndex: 1, boxShadow: "0 4px 6px -1px rgb(0 0 0 / 0.1)" }}>
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
          <MapContainer center={userPos || [21.0285, 105.8542]} zoom={14} style={{ width: "100%", height: "100%" }} zoomControl={false}>
            <TileLayer
              attribution='&copy; <a href="https://www.google.com/maps">Google Maps</a>'
              url="https://mt1.google.com/vt/lyrs=m&x={x}&y={y}&z={z}"
            />
            {flyTarget && <FlyTo center={flyTarget} zoom={15} />}
            {userPos && <Marker position={userPos} icon={new L.DivIcon({
              html: `<div style="width:20px;height:20px;border-radius:50%;background:#3b82f6;border:3px solid #fff;box-shadow:0 0 0 6px rgba(59,130,246,.25),0 2px 8px rgba(0,0,0,.3);"></div>`,
              className: "", iconSize: [20, 20], iconAnchor: [10, 10]
            })} />}
            {filtered.filter(s => s.latitude && s.longitude).map(s => {
              const icon = mapIcons[s.approvalStatus] || mapIcons.Draft;
              return (
                <Marker key={s.id} position={[s.latitude, s.longitude]} icon={icon}>
                  <Popup className="custom-popup" maxWidth={280}>
                    <div style={{ fontFamily: "Inter, system-ui, sans-serif" }}>
                      <div style={{ padding: "16px 16px 12px", borderBottom: "1px solid #f1f5f9" }}>
                        <h3 style={{ margin: "0 0 4px 0", fontSize: 16, color: "#1e293b", fontWeight: 700 }}>{s.name}</h3>
                        <div style={{ fontSize: 12, color: "#64748b" }}>{s.ownerName || s.owner?.fullName || s.owner?.phoneNumber || "—"}</div>
                      </div>
                      <div style={{ padding: "12px 16px" }}>
                        <div style={{ fontSize: 12, color: "#64748b", marginBottom: 12 }}>📍 {s.address}</div>
                        <div style={{ marginBottom: 12 }}>
                          <span className={`cs-admin-status-badge cs-admin-status-badge--${s.approvalStatus === "PendingApproval" ? "pending" : s.approvalStatus === "Approved" ? "active" : s.approvalStatus === "Rejected" ? "banned" : "draft"}`}>
                            <span className="cs-admin-status-badge__dot" />
                            {getStatusLabel(s.approvalStatus)}
                          </span>
                        </div>
                        <button
                          onClick={() => {
                            setSearch(s.name);
                            setViewMode("list");
                          }}
                          style={{ width: "100%", padding: "8px", background: "#f1f5f9", color: "#334155", border: "none", borderRadius: 8, fontWeight: 600, fontSize: 13, cursor: "pointer" }}
                        >
                          Xem trong danh sách
                        </button>
                      </div>
                    </div>
                  </Popup>
                </Marker>
              )
            })}
          </MapContainer>
          <style>{`
            .custom-popup .leaflet-popup-content-wrapper {
              border-radius: 16px;
              padding: 0;
              overflow: hidden;
              box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.1);
            }
            .custom-popup .leaflet-popup-content { margin: 0; width: 280px !important; }
            .custom-popup .leaflet-popup-close-button { top: 12px; right: 12px; color: #64748b; }
            @keyframes stationPulse {
              0% { transform: scale(0.95); opacity: 0.8; }
              70% { transform: scale(1.3); opacity: 0; }
              100% { transform: scale(0.95); opacity: 0; }
            }
          `}</style>
        </div>
      )}

      {/* Confirm Modal */}
      {confirmAction && (
        <div className="cs-admin-modal-overlay">
          <div className="cs-admin-modal">
            <div className="cs-admin-modal__icon">
              {confirmAction.isApproved ? "✅" : "🚫"}
            </div>
            <h2 className="cs-admin-modal__title">Xác nhận thao tác</h2>
            <p className="cs-admin-modal__desc">
              Bạn có chắc chắn muốn{" "}
              <strong>{confirmAction.isApproved ? "phê duyệt" : "từ chối"}</strong>{" "}
              trạm "<strong>{confirmAction.station.name}</strong>" không?
            </p>

            <div style={{ textAlign: "left", marginBottom: 20 }}>
              <label style={{ display: "block", fontSize: 13, fontWeight: 600, color: "#374151", marginBottom: 6 }}>
                Ghi chú {!confirmAction.isApproved && <span style={{ color: "#ef4444" }}>*</span>}
              </label>
              <textarea
                value={adminNote}
                onChange={(e) => setAdminNote(e.target.value)}
                placeholder={confirmAction.isApproved ? "Ghi chú (tùy chọn)" : "Lý do từ chối..."}
                rows={3}
                style={{
                  width: "100%", padding: "10px 14px", borderRadius: 12,
                  border: "1.5px solid #e5e7eb", fontSize: 14, outline: "none",
                  resize: "vertical", boxSizing: "border-box",
                  transition: "border-color 0.2s",
                }}
                onFocus={(e) => e.target.style.borderColor = "#f97316"}
                onBlur={(e) => e.target.style.borderColor = "#e5e7eb"}
              />
            </div>

            <div className="cs-admin-modal__actions">
              <button
                onClick={() => { setConfirmAction(null); setAdminNote(""); }}
                disabled={reviewMutation.isPending}
                className="cs-admin-modal__btn cs-admin-modal__btn--cancel"
              >
                Hủy
              </button>
              <button
                onClick={confirmReview}
                disabled={reviewMutation.isPending || (!confirmAction.isApproved && !adminNote.trim())}
                className={`cs-admin-modal__btn ${confirmAction.isApproved ? "cs-admin-modal__btn--success" : "cs-admin-modal__btn--danger"}`}
              >
                {reviewMutation.isPending ? "Đang xử lý..." : "Xác nhận"}
              </button>
            </div>
          </div>
        </div>
      )}

      <style>{styles}</style>
    </div>
  );
}

const styles = `
  .cs-admin-page {
    max-width: 1400px;
    width: 95%;
    margin: 0 auto;
    padding: 88px 0 40px;
  }
  @media (max-width: 768px) {
    .cs-admin-page { width: 100%; padding: 80px 16px 40px; }
  }
  .cs-admin-page__header { margin-bottom: 28px; }
  .cs-admin-page__title {
    font-size: 26px;
    font-weight: 800;
    color: #1e293b;
    letter-spacing: -0.5px;
  }
  .cs-admin-page__subtitle {
    font-size: 14px;
    color: #64748b;
    margin-top: 4px;
  }

  /* Stats */
  .cs-admin-stats {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 20px;
    margin-bottom: 24px;
  }
  .cs-admin-stats--4 {
    grid-template-columns: repeat(4, 1fr);
  }
  @media (max-width: 900px) {
    .cs-admin-stats--4 { grid-template-columns: repeat(2, 1fr); }
  }
  @media (max-width: 640px) {
    .cs-admin-stats, .cs-admin-stats--4 { grid-template-columns: 1fr; }
  }
  .cs-admin-stat-card {
    background: white;
    border: 1px solid rgba(0,0,0,0.06);
    border-radius: 16px;
    padding: 20px 24px;
    display: flex;
    align-items: center;
    gap: 16px;
    transition: all 0.3s;
  }
  .cs-admin-stat-card:hover {
    transform: translateY(-2px);
    box-shadow: 0 8px 24px rgba(0,0,0,0.08);
  }
  .cs-admin-stat-card__icon {
    width: 48px;
    height: 48px;
    border-radius: 14px;
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
  }
  .cs-admin-stat-card__icon--total { background: #eff6ff; color: #3b82f6; }
  .cs-admin-stat-card__icon--pending { background: #fffbeb; color: #f59e0b; }
  .cs-admin-stat-card__icon--active { background: #f0fdf4; color: #16a34a; }
  .cs-admin-stat-card__icon--banned { background: #fef2f2; color: #dc2626; }
  .cs-admin-stat-card__label { font-size: 13px; color: #64748b; }
  .cs-admin-stat-card__value { font-size: 28px; font-weight: 800; color: #1e293b; margin-top: 2px; }

  /* Filter */
  .cs-admin-filter {
    background: white;
    border: 1px solid rgba(0,0,0,0.06);
    border-radius: 16px;
    padding: 16px 20px;
    margin-bottom: 20px;
    display: flex;
    gap: 12px;
    align-items: center;
    flex-wrap: wrap;
  }
  @media (max-width: 600px) {
    .cs-admin-filter { flex-direction: column; align-items: stretch; }
    .cs-admin-filter__search { min-width: unset; }
    .cs-admin-filter__select, .cs-admin-filter__reset { width: 100%; }
  }
  .cs-admin-filter__search {
    flex: 1;
    min-width: 200px;
    position: relative;
  }
  .cs-admin-filter__search-icon {
    position: absolute;
    left: 14px;
    top: 50%;
    transform: translateY(-50%);
    color: #9ca3af;
  }
  .cs-admin-filter__input {
    width: 100%;
    height: 42px;
    border: 1.5px solid #e5e7eb;
    border-radius: 12px;
    padding: 0 16px 0 40px;
    font-size: 14px;
    outline: none;
    transition: all 0.2s;
    background: #f9fafb;
    box-sizing: border-box;
  }
  .cs-admin-filter__input:focus {
    border-color: #f97316;
    background: white;
    box-shadow: 0 0 0 3px rgba(249,115,22,0.1);
  }
  .cs-admin-filter__select {
    height: 42px;
    border: 1.5px solid #e5e7eb;
    border-radius: 12px;
    padding: 0 14px;
    font-size: 14px;
    background: #f9fafb;
    cursor: pointer;
    outline: none;
    transition: border-color 0.2s;
  }
  .cs-admin-filter__select:focus { border-color: #f97316; }
  .cs-admin-filter__reset {
    height: 42px;
    padding: 0 18px;
    border: 1.5px solid #e5e7eb;
    border-radius: 12px;
    background: white;
    font-size: 13px;
    font-weight: 500;
    color: #64748b;
    cursor: pointer;
    display: flex;
    align-items: center;
    gap: 6px;
    transition: all 0.2s;
  }
  .cs-admin-filter__reset:hover { background: #f9fafb; border-color: #d1d5db; }

  /* Table */
  .cs-admin-table-wrap {
    background: white;
    border: 1px solid rgba(0,0,0,0.06);
    border-radius: 16px;
    overflow: hidden;
    overflow-x: auto;
    position: relative;
  }
  .cs-admin-table__spinner {
    width: 28px; height: 28px;
    border: 3px solid #f3f4f6;
    border-top-color: #f97316;
    border-radius: 50%;
    animation: cs-spin 0.8s linear infinite;
  }
  @keyframes cs-spin { to { transform: rotate(360deg); } }
  .cs-admin-table {
    width: 100%;
    min-width: 900px;
    border-collapse: collapse;
  }
  .cs-admin-table thead {
    background: linear-gradient(180deg, #f8fafc, #f1f5f9);
  }
  .cs-admin-table th {
    padding: 14px 18px;
    font-size: 12px;
    font-weight: 700;
    color: #64748b;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    text-align: left;
    border-bottom: 1px solid #e5e7eb;
  }
  .cs-admin-table td {
    padding: 14px 18px;
    font-size: 14px;
    color: #374151;
    border-bottom: 1px solid #f1f5f9;
  }
  .cs-admin-table tbody tr {
    transition: background 0.15s;
  }
  .cs-admin-table tbody tr:hover {
    background: #fefce8;
  }
  .cs-admin-table__id { color: #9ca3af; font-size: 13px; }
  .cs-admin-table__name { font-weight: 600; color: #1e293b; }
  .cs-admin-table__empty {
    text-align: center;
    padding: 48px 0 !important;
    color: #94a3b8;
  }
  .cs-admin-table__empty p { margin-top: 12px; font-size: 14px; }

  /* Status Badges */
  .cs-admin-status-badge {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 4px 12px;
    border-radius: 50px;
    font-size: 12px;
    font-weight: 600;
  }
  .cs-admin-status-badge__dot {
    width: 7px; height: 7px; border-radius: 50%;
  }
  .cs-admin-status-badge--pending {
    background: #fffbeb; color: #f59e0b;
  }
  .cs-admin-status-badge--pending .cs-admin-status-badge__dot { background: #f59e0b; }
  .cs-admin-status-badge--active {
    background: #f0fdf4; color: #16a34a;
  }
  .cs-admin-status-badge--active .cs-admin-status-badge__dot { background: #16a34a; }
  .cs-admin-status-badge--banned {
    background: #fef2f2; color: #dc2626;
  }
  .cs-admin-status-badge--banned .cs-admin-status-badge__dot { background: #dc2626; }
  .cs-admin-status-badge--draft {
    background: #f1f5f9; color: #64748b;
  }
  .cs-admin-status-badge--draft .cs-admin-status-badge__dot { background: #94a3b8; }

  /* Action Buttons */
  .cs-admin-action-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    height: 34px;
    min-width: 90px;
    padding: 0 14px;
    border-radius: 10px;
    font-size: 12px;
    font-weight: 600;
    border: none;
    cursor: pointer;
    transition: all 0.2s;
    color: white;
  }
  .cs-admin-action-btn--ban { background: #ef4444; }
  .cs-admin-action-btn--ban:hover { background: #dc2626; transform: translateY(-1px); }
  .cs-admin-action-btn--activate { background: #22c55e; }
  .cs-admin-action-btn--activate:hover { background: #16a34a; transform: translateY(-1px); }
  .cs-admin-action-btn--disabled { background: #d1d5db; cursor: not-allowed; color: #9ca3af; }
  .cs-admin-action-btn--unban { background: #7c3aed; }
  .cs-admin-action-btn--unban:hover { background: #6d28d9; transform: translateY(-1px); }

  /* Toggle Switch */
  .cs-toggle-switch {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    cursor: pointer;
    user-select: none;
  }
  .cs-toggle-switch input { display: none; }
  .cs-toggle-switch__track {
    position: relative;
    width: 44px;
    height: 24px;
    background: #fca5a5;
    border-radius: 99px;
    transition: background 0.25s;
    flex-shrink: 0;
  }
  .cs-toggle-switch input:checked + .cs-toggle-switch__track {
    background: linear-gradient(135deg, #22c55e, #16a34a);
  }
  .cs-toggle-switch__thumb {
    position: absolute;
    top: 3px;
    left: 3px;
    width: 18px;
    height: 18px;
    background: white;
    border-radius: 50%;
    box-shadow: 0 1px 4px rgba(0,0,0,0.2);
    transition: transform 0.25s cubic-bezier(0.34,1.56,0.64,1);
  }
  .cs-toggle-switch input:checked ~ .cs-toggle-switch__track .cs-toggle-switch__thumb {
    transform: translateX(20px);
  }
  .cs-toggle-switch__label {
    font-size: 12px;
    font-weight: 600;
    color: #ef4444;
    min-width: 66px;
  }
  .cs-toggle-switch input:checked ~ .cs-toggle-switch__label { color: #16a34a; }

  /* Modal */
  .cs-admin-modal-overlay {
    position: fixed;
    inset: 0;
    z-index: 50;
    background: rgba(0,0,0,0.5);
    backdrop-filter: blur(4px);
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 24px;
  }
  .cs-admin-modal {
    width: 100%;
    max-width: 460px;
    background: white;
    border-radius: 20px;
    padding: 32px;
    text-align: center;
    animation: cs-fadeInUp 0.3s ease-out;
    box-shadow: 0 20px 60px rgba(0,0,0,0.2);
  }
  @keyframes cs-fadeInUp {
    from { opacity: 0; transform: translateY(20px); }
    to { opacity: 1; transform: translateY(0); }
  }
  .cs-admin-modal__icon { font-size: 40px; margin-bottom: 16px; }
  .cs-admin-modal__title {
    font-size: 20px;
    font-weight: 700;
    color: #1e293b;
    margin-bottom: 8px;
  }
  .cs-admin-modal__desc { font-size: 14px; color: #64748b; margin-bottom: 24px; line-height: 1.6; }
  .cs-admin-modal__actions { display: flex; gap: 12px; }
  .cs-admin-modal__btn {
    flex: 1;
    height: 44px;
    border-radius: 12px;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.2s;
    border: none;
  }
  .cs-admin-modal__btn--cancel {
    background: #f1f5f9;
    color: #374151;
  }
  .cs-admin-modal__btn--cancel:hover { background: #e2e8f0; }
  .cs-admin-modal__btn--danger { background: #ef4444; color: white; }
  .cs-admin-modal__btn--danger:hover { background: #dc2626; }
  .cs-admin-modal__btn--success { background: #22c55e; color: white; }
  .cs-admin-modal__btn--success:hover { background: #16a34a; }
  .cs-admin-modal__btn:disabled { opacity: 0.6; cursor: not-allowed; }
`;