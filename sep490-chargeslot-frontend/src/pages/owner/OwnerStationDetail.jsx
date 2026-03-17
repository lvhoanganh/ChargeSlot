import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useParams, Link } from "react-router-dom";
import { instance } from "@/lib/httpRequest";
import StationLayoutViewer from "@/components/owner/StationLayoutViewer";

/* ── constants ──────────────────────────────────────── */
const APPROVAL_STYLE = {
  Draft: "bg-slate-200 text-slate-700",
  PendingApproval: "bg-amber-100 text-amber-700",
  Approved: "bg-emerald-100 text-emerald-700",
  Rejected: "bg-rose-100 text-rose-700",
};
const APPROVAL_LABEL = {
  Draft: "Bản nháp",
  PendingApproval: "Chờ duyệt",
  Approved: "Đã duyệt",
  Rejected: "Bị từ chối",
};
const OPERATIONAL_STYLE = {
  Active: "bg-green-100 text-green-700",
  Inactive: "bg-zinc-200 text-zinc-700",
};
const OPERATIONAL_LABEL = {
  Active: "Đang hoạt động",
  Inactive: "Tạm ngưng",
};

const SLOT_STATUS_LABEL = {
  Active: "Hoạt động",
  Inactive: "Tạm ngưng",
  Maintenance: "Bảo trì",
  Booked: "Đã đặt",
};
const SLOT_STATUS_DOT = {
  Active: "bg-emerald-500",
  Inactive: "bg-slate-400",
  Maintenance: "bg-amber-400",
  Booked: "bg-blue-500",
};

const DAY_LABELS = [
  "Chủ nhật",
  "Thứ 2",
  "Thứ 3",
  "Thứ 4",
  "Thứ 5",
  "Thứ 6",
  "Thứ 7",
];

/* ── helpers ─────────────────────────────────────────── */
function formatDateTime(value) {
  if (!value) return "-";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return "-";
  return d.toLocaleString("vi-VN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function formatTime(value) {
  if (!value) return "-";
  // value could be "HH:mm:ss" or "HH:mm"
  return value.substring(0, 5);
}

function formatCurrency(value) {
  if (value == null) return "-";
  return Number(value).toLocaleString("vi-VN") + "đ";
}

const getStation = async (id) => {
  const res = await instance.get(`/stations/${id}`);
  return res.data;
};

/* ── Slot detail popup ───────────────────────────────── */
function SlotDetailPopup({ slot, onClose }) {
  if (!slot) return null;

  const statusDot = SLOT_STATUS_DOT[slot.status] || "bg-slate-400";
  const statusLabel = SLOT_STATUS_LABEL[slot.status] || slot.status;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div
        className="absolute inset-0 bg-black/50 backdrop-blur-sm"
        onClick={onClose}
      />
      <div className="relative z-10 w-full max-w-sm rounded-2xl bg-white p-6 shadow-2xl ring-1 ring-slate-200">
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-lg font-bold text-slate-900">
            Trụ sạc: {slot.slotName}
          </h3>
          <button
            type="button"
            onClick={onClose}
            className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 transition hover:bg-slate-100 hover:text-slate-600"
          >
            ✕
          </button>
        </div>

        <div className="space-y-3 text-sm text-slate-700">
          <div className="flex items-center justify-between rounded-xl bg-slate-50 px-4 py-2.5">
            <span className="text-slate-500">Trạng thái</span>
            <span className="flex items-center gap-1.5 font-semibold">
              <span
                className={`inline-block h-2.5 w-2.5 rounded-full ${statusDot}`}
              />
              {statusLabel}
            </span>
          </div>
          <div className="flex items-center justify-between rounded-xl bg-slate-50 px-4 py-2.5">
            <span className="text-slate-500">Loại đầu sạc</span>
            <span className="font-semibold">{slot.connectorType}</span>
          </div>
          <div className="flex items-center justify-between rounded-xl bg-slate-50 px-4 py-2.5">
            <span className="text-slate-500">Công suất</span>
            <span className="font-semibold">
              {slot.powerKw != null ? `${slot.powerKw} kW` : "-"}
            </span>
          </div>
          <div className="flex items-center justify-between rounded-xl bg-slate-50 px-4 py-2.5">
            <span className="text-slate-500">Giá / giờ</span>
            <span className="font-semibold text-orange-600">
              {formatCurrency(slot.basePricePerHour)}
            </span>
          </div>
          <div className="flex items-center justify-between rounded-xl bg-slate-50 px-4 py-2.5">
            <span className="text-slate-500">Vị trí</span>
            <span className="font-semibold">
              X={slot.positionX}, Y={slot.positionY}
            </span>
          </div>
          <div className="flex items-center justify-between rounded-xl bg-slate-50 px-4 py-2.5">
            <span className="text-slate-500">Tạo lúc</span>
            <span className="font-semibold">
              {formatDateTime(slot.createdAt)}
            </span>
          </div>
        </div>

        <button
          type="button"
          onClick={onClose}
          className="mt-5 w-full rounded-xl bg-slate-900 py-2.5 text-sm font-semibold text-white transition hover:bg-slate-800"
        >
          Đóng
        </button>
      </div>
    </div>
  );
}

/* ── main page ───────────────────────────────────────── */
export default function OwnerStationDetail() {
  const { id } = useParams();
  const [selectedSlot, setSelectedSlot] = useState(null);

  const {
    data: station,
    isLoading,
    error,
  } = useQuery({
    queryKey: ["owner-station", id],
    queryFn: () => getStation(id),
    enabled: !!id,
  });

  /* ── Loading ─────────────────────────────────── */
  if (isLoading) {
    return (
      <div className="min-h-screen bg-slate-100 px-6 py-8">
        <div className="mx-auto max-w-6xl">
          <div className="rounded-2xl bg-white p-12 text-center text-slate-500 shadow-sm ring-1 ring-slate-200">
            <div className="mx-auto mb-3 h-8 w-8 animate-spin rounded-full border-4 border-slate-300 border-t-orange-500" />
            Đang tải thông tin trạm sạc...
          </div>
        </div>
      </div>
    );
  }

  /* ── Error ────────────────────────────────────── */
  if (error || !station) {
    return (
      <div className="min-h-screen bg-slate-100 px-6 py-8">
        <div className="mx-auto max-w-6xl">
          <div className="rounded-2xl bg-white p-12 text-center shadow-sm ring-1 ring-slate-200">
            <p className="text-lg font-semibold text-rose-600">
              Không thể tải thông tin trạm sạc
            </p>
            <p className="mt-2 text-sm text-slate-500">
              {error?.response?.data?.message ||
                error?.message ||
                "Trạm sạc không tồn tại hoặc bạn không có quyền truy cập."}
            </p>
            <Link
              to="/stations"
              className="mt-5 inline-block rounded-xl bg-slate-900 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-slate-800"
            >
              Quay lại danh sách
            </Link>
          </div>
        </div>
      </div>
    );
  }

  const gridRows = Number(station.layoutHeight) || 0;
  const gridCols = Number(station.layoutWidth) || 0;
  const totalSlots = station.chargingSlots?.length || 0;
  const activeSlots =
    station.chargingSlots?.filter((s) => s.status === "Active")?.length || 0;

  return (
    <div className="min-h-screen bg-slate-100 px-6 py-8 text-slate-900">
      <div className="mx-auto max-w-6xl space-y-6">
        {/* ── Header ──────────────────────────────── */}
        <section className="flex flex-wrap items-start justify-between gap-4 rounded-2xl bg-white px-6 py-5 shadow-sm ring-1 ring-slate-200">
          <div className="flex-1">
            <p className="text-sm font-medium uppercase tracking-[0.18em] text-orange-500">
              Chi tiết trạm sạc
            </p>
            <h1 className="mt-2 text-3xl font-bold">{station.name}</h1>
            <p className="mt-1 text-sm text-slate-600">{station.address}</p>
            <div className="mt-3 flex flex-wrap items-center gap-2">
              <span
                className={`rounded-full px-3 py-1 text-xs font-semibold ${
                  APPROVAL_STYLE[station.approvalStatus] ||
                  "bg-slate-200 text-slate-700"
                }`}
              >
                {APPROVAL_LABEL[station.approvalStatus] ||
                  station.approvalStatus}
              </span>
              <span
                className={`rounded-full px-3 py-1 text-xs font-semibold ${
                  OPERATIONAL_STYLE[station.operationalStatus] ||
                  "bg-zinc-200 text-zinc-700"
                }`}
              >
                {OPERATIONAL_LABEL[station.operationalStatus] ||
                  station.operationalStatus}
              </span>
              <span className="text-xs text-slate-400">
                Tạo: {formatDateTime(station.createdAt)}
              </span>
            </div>
          </div>

          <Link
            to="/stations"
            className="rounded-xl border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:border-slate-400 hover:bg-slate-50"
          >
            ← Quay lại
          </Link>
        </section>

        {/* ── Admin note ──────────────────────────── */}
        {station.approvalStatus === "Rejected" && station.adminNote && (
          <section className="rounded-2xl border border-rose-200 bg-rose-50 p-5 shadow-sm">
            <p className="font-semibold text-rose-700">Ghi chú từ Admin</p>
            <p className="mt-1 text-sm text-rose-600">{station.adminNote}</p>
          </section>
        )}

        {/* ── Station images ─────────────────────── */}
        {station.images?.length > 0 && (
          <section className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <h2 className="mb-4 text-xl font-semibold">Hình ảnh trạm</h2>
            <div className="flex gap-3 overflow-x-auto pb-2">
              {station.images.map((img) => (
                <img
                  key={img.id}
                  src={img.imageUrl}
                  alt={`Ảnh trạm ${station.name}`}
                  className="h-40 w-60 flex-shrink-0 rounded-xl object-cover ring-1 ring-slate-200"
                />
              ))}
            </div>
          </section>
        )}

        {/* ── Stats cards ─────────────────────────── */}
        <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <div className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
            <p className="text-xs font-medium uppercase tracking-wider text-slate-400">
              Tổng trụ sạc
            </p>
            <p className="mt-2 text-3xl font-bold text-slate-900">
              {totalSlots}
            </p>
          </div>
          <div className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
            <p className="text-xs font-medium uppercase tracking-wider text-slate-400">
              Trụ hoạt động
            </p>
            <p className="mt-2 text-3xl font-bold text-emerald-600">
              {activeSlots}
            </p>
          </div>
          <div className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
            <p className="text-xs font-medium uppercase tracking-wider text-slate-400">
              Kích thước sơ đồ
            </p>
            <p className="mt-2 text-3xl font-bold text-slate-900">
              {gridCols}×{gridRows}
            </p>
          </div>
          <div className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
            <p className="text-xs font-medium uppercase tracking-wider text-slate-400">
              Tọa độ
            </p>
            <p className="mt-2 text-lg font-bold text-slate-900">
              {station.latitude != null && station.longitude != null
                ? `${station.latitude}, ${station.longitude}`
                : "Chưa có"}
            </p>
          </div>
        </section>

        {/* ── Description ─────────────────────────── */}
        {station.description && (
          <section className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <h2 className="mb-3 text-xl font-semibold">Mô tả</h2>
            <p className="whitespace-pre-line text-sm leading-relaxed text-slate-600">
              {station.description}
            </p>
          </section>
        )}

        {/* ── Grid Layout ─────────────────────────── */}
        <section className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
          <h2 className="mb-1 text-xl font-semibold">Sơ đồ trạm sạc</h2>
          <p className="mb-5 text-sm text-slate-500">
            Click vào trụ sạc để xem chi tiết. Mỗi trụ được tô màu theo trạng
            thái.
          </p>
          <StationLayoutViewer
            rows={gridRows}
            cols={gridCols}
            slots={station.chargingSlots || []}
            onSlotClick={(slot) => setSelectedSlot(slot)}
          />
        </section>

        {/* ── Slot list table ─────────────────────── */}
        {station.chargingSlots?.length > 0 && (
          <section className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <h2 className="mb-4 text-xl font-semibold">
              Danh sách trụ sạc ({totalSlots})
            </h2>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-slate-200 text-left text-xs font-semibold uppercase tracking-wider text-slate-400">
                    <th className="px-4 py-3">Tên</th>
                    <th className="px-4 py-3">Đầu sạc</th>
                    <th className="px-4 py-3">Công suất</th>
                    <th className="px-4 py-3">Giá / giờ</th>
                    <th className="px-4 py-3">Vị trí</th>
                    <th className="px-4 py-3">Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {station.chargingSlots.map((slot) => {
                    const dot = SLOT_STATUS_DOT[slot.status] || "bg-slate-400";
                    const label =
                      SLOT_STATUS_LABEL[slot.status] || slot.status;
                    return (
                      <tr
                        key={slot.id}
                        className="border-b border-slate-100 transition hover:bg-slate-50"
                      >
                        <td className="px-4 py-3 font-medium text-slate-800">
                          {slot.slotName}
                        </td>
                        <td className="px-4 py-3 text-slate-600">
                          {slot.connectorType}
                        </td>
                        <td className="px-4 py-3 text-slate-600">
                          {slot.powerKw != null ? `${slot.powerKw} kW` : "-"}
                        </td>
                        <td className="px-4 py-3 font-medium text-orange-600">
                          {formatCurrency(slot.basePricePerHour)}
                        </td>
                        <td className="px-4 py-3 text-slate-500">
                          ({slot.positionX}, {slot.positionY})
                        </td>
                        <td className="px-4 py-3">
                          <span className="inline-flex items-center gap-1.5">
                            <span
                              className={`inline-block h-2 w-2 rounded-full ${dot}`}
                            />
                            <span className="text-slate-700">{label}</span>
                          </span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </section>
        )}

        {/* ── Operating hours ─────────────────────── */}
        {station.operatingHours?.length > 0 && (
          <section className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <h2 className="mb-4 text-xl font-semibold">Giờ hoạt động</h2>
            <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
              {station.operatingHours.map((oh) => {
                const dayLabel =
                  DAY_LABELS[oh.dayOfWeek] || `Ngày ${oh.dayOfWeek}`;
                return (
                  <div
                    key={oh.dayOfWeek}
                    className={`rounded-xl px-4 py-3 ${
                      oh.isClosed
                        ? "border border-slate-200 bg-slate-50"
                        : "border border-emerald-200 bg-emerald-50"
                    }`}
                  >
                    <p className="text-sm font-semibold text-slate-800">
                      {dayLabel}
                    </p>
                    {oh.isClosed ? (
                      <p className="mt-1 text-xs text-slate-400">Đóng cửa</p>
                    ) : (
                      <p className="mt-1 text-xs font-medium text-emerald-700">
                        {formatTime(oh.openTime)} – {formatTime(oh.closeTime)}
                      </p>
                    )}
                  </div>
                );
              })}
            </div>
          </section>
        )}
      </div>

      {/* ── Slot detail popup ─────────────────────── */}
      <SlotDetailPopup
        slot={selectedSlot}
        onClose={() => setSelectedSlot(null)}
      />
    </div>
  );
}
