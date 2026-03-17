import { useState, useCallback, useMemo } from "react";
import SlotConfigDialog from "./SlotConfigDialog";

/* ── helpers ─────────────────────────────────────────── */
const ROW_LABELS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
const getRowLabel = (index) =>
  index < 26
    ? ROW_LABELS[index]
    : ROW_LABELS[Math.floor(index / 26) - 1] + ROW_LABELS[index % 26];

/* Status → Tailwind colours */
const STATUS_COLORS = {
  Active: {
    bg: "bg-emerald-500",
    bgHover: "hover:bg-emerald-600",
    ring: "ring-emerald-300",
    text: "text-white",
  },
  Inactive: {
    bg: "bg-slate-400",
    bgHover: "hover:bg-slate-500",
    ring: "ring-slate-300",
    text: "text-white",
  },
  Maintenance: {
    bg: "bg-amber-400",
    bgHover: "hover:bg-amber-500",
    ring: "ring-amber-300",
    text: "text-white",
  },
};

/* ── tiny icon (charger) ─────────────────────────────── */
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

/* ── main component ──────────────────────────────────── */
export default function StationLayoutDesigner({
  rows,
  cols,
  slots,
  onSlotsChange,
  onRowsChange,
  onColsChange,
}) {
  const [dialog, setDialog] = useState({
    open: false,
    row: 0,
    col: 0,
    slot: null,
  });

  /* Build a lookup map: "row-col" → slot */
  const slotMap = useMemo(() => {
    const map = {};
    (slots || []).forEach((s) => {
      map[`${s.positionY}-${s.positionX}`] = s;
    });
    return map;
  }, [slots]);

  /* Cell click handler */
  const handleCellClick = useCallback(
    (r, c) => {
      const key = `${r + 1}-${c + 1}`;
      const existing = slotMap[key] || null;
      setDialog({ open: true, row: r, col: c, slot: existing });
    },
    [slotMap],
  );

  /* Save from dialog */
  const handleSave = useCallback(
    (formData) => {
      const posX = dialog.col + 1;
      const posY = dialog.row + 1;
      const key = `${posY}-${posX}`;

      let newSlots;
      if (slotMap[key]) {
        // update existing
        newSlots = (slots || []).map((s) =>
          s.positionX === posX && s.positionY === posY
            ? { ...s, ...formData, positionX: posX, positionY: posY }
            : s,
        );
      } else {
        // add new
        newSlots = [
          ...(slots || []),
          { ...formData, positionX: posX, positionY: posY },
        ];
      }
      onSlotsChange(newSlots);
      setDialog({ open: false, row: 0, col: 0, slot: null });
    },
    [dialog, slotMap, slots, onSlotsChange],
  );

  /* Delete from dialog */
  const handleDelete = useCallback(() => {
    const posX = dialog.col + 1;
    const posY = dialog.row + 1;
    const newSlots = (slots || []).filter(
      (s) => !(s.positionX === posX && s.positionY === posY),
    );
    onSlotsChange(newSlots);
    setDialog({ open: false, row: 0, col: 0, slot: null });
  }, [dialog, slots, onSlotsChange]);

  /* Add / remove rows & cols */
  const addRow = () => onRowsChange(rows + 1);
  const removeRow = () => {
    if (rows <= 1) return;
    // remove slots on last row
    const newSlots = (slots || []).filter((s) => s.positionY !== rows);
    onSlotsChange(newSlots);
    onRowsChange(rows - 1);
  };
  const addCol = () => onColsChange(cols + 1);
  const removeCol = () => {
    if (cols <= 1) return;
    // remove slots on last col
    const newSlots = (slots || []).filter((s) => s.positionX !== cols);
    onSlotsChange(newSlots);
    onColsChange(cols - 1);
  };

  const totalSlots = (slots || []).length;

  return (
    <div className="space-y-4">
      {/* ── Toolbar ──────────────────────────────────── */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1.5 rounded-xl border border-slate-200 bg-slate-50 px-3 py-1.5">
            <span className="text-xs font-medium text-slate-500">Hàng:</span>
            <button
              type="button"
              onClick={removeRow}
              disabled={rows <= 1}
              className="flex h-6 w-6 items-center justify-center rounded-md bg-white text-sm font-bold text-slate-600 shadow-sm ring-1 ring-slate-200 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-40"
            >
              −
            </button>
            <span className="w-6 text-center text-sm font-semibold text-slate-800">
              {rows}
            </span>
            <button
              type="button"
              onClick={addRow}
              className="flex h-6 w-6 items-center justify-center rounded-md bg-white text-sm font-bold text-slate-600 shadow-sm ring-1 ring-slate-200 transition hover:bg-slate-100"
            >
              +
            </button>
          </div>

          <div className="flex items-center gap-1.5 rounded-xl border border-slate-200 bg-slate-50 px-3 py-1.5">
            <span className="text-xs font-medium text-slate-500">Cột:</span>
            <button
              type="button"
              onClick={removeCol}
              disabled={cols <= 1}
              className="flex h-6 w-6 items-center justify-center rounded-md bg-white text-sm font-bold text-slate-600 shadow-sm ring-1 ring-slate-200 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-40"
            >
              −
            </button>
            <span className="w-6 text-center text-sm font-semibold text-slate-800">
              {cols}
            </span>
            <button
              type="button"
              onClick={addCol}
              className="flex h-6 w-6 items-center justify-center rounded-md bg-white text-sm font-bold text-slate-600 shadow-sm ring-1 ring-slate-200 transition hover:bg-slate-100"
            >
              +
            </button>
          </div>
        </div>

        <span className="rounded-full bg-orange-100 px-3 py-1 text-xs font-semibold text-orange-700">
          {totalSlots} trụ sạc
        </span>
      </div>

      {/* ── Grid ─────────────────────────────────────── */}
      <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-slate-50/50 p-4">
        <table className="mx-auto border-separate border-spacing-1.5">
          {/* Column headers */}
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
                  {/* Row label */}
                  <td className="h-14 w-12 text-center">
                    <span className="inline-flex h-8 w-8 items-center justify-center rounded-lg bg-slate-200/80 text-xs font-bold text-slate-600">
                      {rowLabel}
                    </span>
                  </td>

                  {/* Cells */}
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
                            onClick={() => handleCellClick(r, c)}
                            title={`${slot.slotName} · ${slot.connectorType} · ${slot.powerKw}kW`}
                            className={`group relative flex h-14 w-14 flex-col items-center justify-center rounded-xl ${colors.bg} ${colors.bgHover} ${colors.text} shadow-md ring-2 ${colors.ring} transition-all duration-200 hover:scale-105 hover:shadow-lg`}
                          >
                            <ChargerIcon className="h-5 w-5 drop-shadow-sm" />
                            <span className="mt-0.5 text-[10px] font-bold leading-none tracking-wide drop-shadow-sm">
                              {slot.slotName}
                            </span>
                          </button>
                        </td>
                      );
                    }

                    /* Empty cell */
                    return (
                      <td key={c} className="h-14 w-14">
                        <button
                          type="button"
                          onClick={() => handleCellClick(r, c)}
                          className="flex h-14 w-14 items-center justify-center rounded-xl border-2 border-dashed border-slate-300 bg-white text-slate-400 transition-all duration-200 hover:border-orange-400 hover:bg-orange-50 hover:text-orange-500 hover:shadow-md"
                        >
                          <svg
                            xmlns="http://www.w3.org/2000/svg"
                            className="h-5 w-5"
                            fill="none"
                            viewBox="0 0 24 24"
                            stroke="currentColor"
                            strokeWidth={2}
                          >
                            <path d="M12 5v14M5 12h14" />
                          </svg>
                        </button>
                      </td>
                    );
                  })}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* ── Legend ────────────────────────────────────── */}
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
          <span className="inline-block h-3 w-3 rounded-none border-2 border-dashed border-slate-300 bg-white" />
          Ô trống
        </span>
      </div>

      {/* ── Dialog ────────────────────────────────────── */}
      <SlotConfigDialog
        open={dialog.open}
        slot={dialog.slot}
        rowLabel={getRowLabel(dialog.row)}
        colLabel={String(dialog.col + 1)}
        onSave={handleSave}
        onDelete={handleDelete}
        onClose={() =>
          setDialog({ open: false, row: 0, col: 0, slot: null })
        }
      />
    </div>
  );
}
