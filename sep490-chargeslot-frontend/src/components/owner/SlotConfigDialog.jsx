import { useState, useEffect } from "react";

const CONNECTOR_OPTIONS = [
  { value: "CCS2", label: "CCS2" },
  { value: "CHAdeMO", label: "CHAdeMO" },
  { value: "Type2", label: "Type 2" },
  { value: "GB/T", label: "GB/T" },
];

const STATUS_OPTIONS = [
  { value: "Active", label: "Hoạt động", color: "bg-emerald-500" },
  { value: "Inactive", label: "Tạm ngưng", color: "bg-slate-400" },
  { value: "Maintenance", label: "Bảo trì", color: "bg-amber-500" },
];

export default function SlotConfigDialog({
  open,
  slot,
  rowLabel,
  colLabel,
  onSave,
  onDelete,
  onClose,
}) {
  const isEditing = !!slot;
  const defaultName = `${rowLabel}${colLabel}`;

  const [form, setForm] = useState({
    slotName: defaultName,
    connectorType: "CCS2",
    powerKw: 50,
    basePricePerHour: 45000,
    status: "Active",
  });

  useEffect(() => {
    if (slot) {
      setForm({
        slotName: slot.slotName || defaultName,
        connectorType: slot.connectorType || "CCS2",
        powerKw: slot.powerKw ?? 50,
        basePricePerHour: slot.basePricePerHour ?? 45000,
        status: slot.status || "Active",
      });
    } else {
      setForm({
        slotName: defaultName,
        connectorType: "CCS2",
        powerKw: 50,
        basePricePerHour: 45000,
        status: "Active",
      });
    }
  }, [slot, defaultName]);

  if (!open) return null;

  const handleChange = (field, value) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleSave = () => {
    onSave({
      ...form,
      powerKw: Number(form.powerKw),
      basePricePerHour: Number(form.basePricePerHour),
    });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/50 backdrop-blur-sm"
        onClick={onClose}
      />

      {/* Dialog */}
      <div className="relative z-10 w-full max-w-md rounded-2xl bg-white p-6 shadow-2xl ring-1 ring-slate-200">
        <div className="mb-5 flex items-center justify-between">
          <div>
            <h3 className="text-lg font-bold text-slate-900">
              {isEditing ? "Chỉnh sửa trụ sạc" : "Thêm trụ sạc"}
            </h3>
            <p className="mt-0.5 text-sm text-slate-500">
              Vị trí: Hàng {rowLabel} – Cột {colLabel}
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 transition hover:bg-slate-100 hover:text-slate-600"
          >
            ✕
          </button>
        </div>

        <div className="space-y-4">
          {/* Slot Name */}
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">
              Tên trụ sạc
            </label>
            <input
              type="text"
              value={form.slotName}
              onChange={(e) => handleChange("slotName", e.target.value)}
              className="h-10 w-full rounded-xl border border-slate-300 px-3 text-sm outline-none transition focus:border-orange-400 focus:ring-2 focus:ring-orange-100"
              placeholder="Ví dụ: A1"
            />
          </div>

          {/* Connector Type */}
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">
              Loại đầu sạc
            </label>
            <select
              value={form.connectorType}
              onChange={(e) => handleChange("connectorType", e.target.value)}
              className="h-10 w-full rounded-xl border border-slate-300 px-3 text-sm outline-none transition focus:border-orange-400 focus:ring-2 focus:ring-orange-100"
            >
              {CONNECTOR_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>

          {/* Power & Price row */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700">
                Công suất (kW)
              </label>
              <input
                type="number"
                step="any"
                value={form.powerKw}
                onChange={(e) => handleChange("powerKw", e.target.value)}
                className="h-10 w-full rounded-xl border border-slate-300 px-3 text-sm outline-none transition focus:border-orange-400 focus:ring-2 focus:ring-orange-100"
              />
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700">
                Giá/giờ (VND)
              </label>
              <input
                type="number"
                value={form.basePricePerHour}
                onChange={(e) =>
                  handleChange("basePricePerHour", e.target.value)
                }
                className="h-10 w-full rounded-xl border border-slate-300 px-3 text-sm outline-none transition focus:border-orange-400 focus:ring-2 focus:ring-orange-100"
              />
            </div>
          </div>

          {/* Status */}
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">
              Trạng thái
            </label>
            <div className="flex gap-2">
              {STATUS_OPTIONS.map((opt) => (
                <button
                  key={opt.value}
                  type="button"
                  onClick={() => handleChange("status", opt.value)}
                  className={`flex-1 rounded-xl border-2 px-3 py-2 text-xs font-semibold transition ${
                    form.status === opt.value
                      ? "border-orange-400 bg-orange-50 text-orange-700"
                      : "border-slate-200 bg-white text-slate-600 hover:border-slate-300"
                  }`}
                >
                  <span
                    className={`mr-1.5 inline-block h-2 w-2 rounded-full ${opt.color}`}
                  />
                  {opt.label}
                </button>
              ))}
            </div>
          </div>
        </div>

        {/* Actions */}
        <div className="mt-6 flex items-center justify-between">
          <div>
            {isEditing && (
              <button
                type="button"
                onClick={onDelete}
                className="rounded-lg px-4 py-2 text-sm font-semibold text-red-600 transition hover:bg-red-50"
              >
                Xóa trụ sạc
              </button>
            )}
          </div>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-xl border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
            >
              Hủy
            </button>
            <button
              type="button"
              onClick={handleSave}
              className="rounded-xl bg-orange-500 px-5 py-2 text-sm font-semibold text-white transition hover:bg-orange-600"
            >
              {isEditing ? "Cập nhật" : "Thêm trụ"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
