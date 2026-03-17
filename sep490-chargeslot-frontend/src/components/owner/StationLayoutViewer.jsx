import { useMemo } from "react";

/* ── helpers ─────────────────────────────────────────── */
const ROW_LABELS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
const getRowLabel = (index) =>
  index < 26
    ? ROW_LABELS[index]
    : ROW_LABELS[Math.floor(index / 26) - 1] + ROW_LABELS[index % 26];

const STATUS_COLORS = {
  Active: {
    bg: "bg-emerald-500",
    ring: "ring-emerald-300",
    text: "text-white",
  },
  Inactive: {
    bg: "bg-slate-400",
    ring: "ring-slate-300",
    text: "text-white",
  },
  Maintenance: {
    bg: "bg-amber-400",
    ring: "ring-amber-300",
    text: "text-white",
  },
  Booked: {
    bg: "bg-blue-500",
    ring: "ring-blue-300",
    text: "text-white",
  },
};

function ChargerIcon({ className = "h-5 w-5" }) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
    >
      <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
    </svg>
  );
}

/**
 * Read-only grid layout viewer for station detail page.
 * Shows charger positions with status colors.
 */
export default function StationLayoutViewer({
  rows,
  cols,
  slots,
  onSlotClick,
}) {
  const slotMap = useMemo(() => {
    const map = {};
    (slots || []).forEach((s) => {
      const px = Number(s.positionX);
      const py = Number(s.positionY);
      if (px && py) map[`${py}-${px}`] = s;
    });
    return map;
  }, [slots]);

  if (!rows || !cols) {
    return (
      <div className="rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-8 text-center text-sm text-slate-500">
        Chưa có sơ đồ mặt bằng.
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-slate-50/50 p-4">
        <table className="mx-auto border-separate border-spacing-1.5">
          <thead>
            <tr>
              <th className="h-9 w-12" />
              {Array.from({ length: cols }, (_, c) => (
                <th
                  key={c}
                  className="h-9 w-14 rounded-lg bg-slate-200/80 text-center text-xs font-bold text-slate-600"
                >
                  {c + 1}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {Array.from({ length: rows }, (_, r) => {
              const rowLabel = getRowLabel(r);
              return (
                <tr key={r}>
                  <td className="h-14 w-12 text-center">
                    <span className="inline-flex h-8 w-8 items-center justify-center rounded-lg bg-slate-200/80 text-xs font-bold text-slate-600">
                      {rowLabel}
                    </span>
                  </td>
                  {Array.from({ length: cols }, (_, c) => {
                    const key = `${r + 1}-${c + 1}`;
                    const slot = slotMap[key];

                    if (slot) {
                      const colors =
                        STATUS_COLORS[slot.status] || STATUS_COLORS.Inactive;
                      return (
                        <td key={c} className="h-14 w-14">
                          <button
                            type="button"
                            onClick={() => onSlotClick?.(slot)}
                            title={`${slot.slotName} · ${slot.connectorType} · ${slot.powerKw ?? "?"}kW · ${slot.status}`}
                            className={`group relative flex h-14 w-14 flex-col items-center justify-center rounded-xl ${colors.bg} ${colors.text} shadow-md ring-2 ${colors.ring} transition-all duration-200 hover:scale-105 hover:shadow-lg`}
                          >
                            <ChargerIcon className="h-5 w-5 drop-shadow-sm" />
                            <span className="mt-0.5 text-[10px] font-bold leading-none tracking-wide drop-shadow-sm">
                              {slot.slotName}
                            </span>
                          </button>
                        </td>
                      );
                    }

                    return (
                      <td key={c} className="h-14 w-14">
                        <div className="flex h-14 w-14 items-center justify-center rounded-xl border border-slate-200 bg-white/60" />
                      </td>
                    );
                  })}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Legend */}
      <div className="flex flex-wrap items-center gap-4 px-1 text-xs text-slate-600">
        <span className="font-medium text-slate-500">Chú thích:</span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-3 w-3 rounded-full bg-emerald-500 ring-2 ring-emerald-200" />
          Hoạt động
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-3 w-3 rounded-full bg-slate-400 ring-2 ring-slate-200" />
          Tạm ngưng
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-3 w-3 rounded-full bg-amber-400 ring-2 ring-amber-200" />
          Bảo trì
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-3 w-3 rounded-full bg-blue-500 ring-2 ring-blue-200" />
          Đã đặt
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-3 w-3 rounded-none border border-slate-200 bg-white/60" />
          Trống
        </span>
      </div>
    </div>
  );
}
