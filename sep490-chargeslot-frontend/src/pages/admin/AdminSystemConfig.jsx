import { useState, useEffect } from "react";
import { adminConfigApi } from "@/services/api";
import { showToast } from "@/components/Toast";

export default function AdminSystemConfig() {
  const [configs, setConfigs] = useState([]);
  const [loading, setLoading] = useState(true);
  const [editingKey, setEditingKey] = useState(null);
  const [editValue, setEditValue] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    adminConfigApi.getAll()
      .then(data => setConfigs(Array.isArray(data) ? data : []))
      .catch(() => setConfigs([]))
      .finally(() => setLoading(false));
  }, []);

  function startEdit(cfg) {
    setEditingKey(cfg.key);
    setEditValue(cfg.value);
  }

  async function handleSave(key) {
    setSaving(true);
    try {
      await adminConfigApi.update(key, editValue);
      setConfigs(prev => prev.map(c => c.key === key ? { ...c, value: editValue } : c));
      setEditingKey(null);
      showToast.success("Cập nhật cấu hình thành công!");
    } catch (err) {
      showToast.error(err.message || "Lỗi cập nhật cấu hình");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <div className="min-h-screen bg-slate-100 pt-20 px-8 pb-8 flex items-center justify-center">
        <div className="text-center">
          <div className="text-5xl mb-4">⚙️</div>
          <p className="text-slate-500">Đang tải cấu hình hệ thống...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-100 pt-20 px-4 md:px-8 pb-8">
      <div className="max-w-3xl mx-auto">
        {/* Header */}
        <div className="mb-6 rounded-2xl bg-white px-6 py-5 shadow-sm ring-1 ring-slate-200">
          <p className="text-sm font-medium uppercase tracking-[0.18em] text-orange-500">Admin</p>
          <h1 className="mt-2 text-2xl font-bold text-slate-900">Cấu hình hệ thống</h1>
          <p className="mt-1 text-sm text-slate-600">Quản lý các tham số cấu hình cho hệ thống ChargeSlot.</p>
        </div>

        {/* Config table */}
        {configs.length === 0 ? (
          <div className="rounded-2xl bg-white p-12 text-center shadow-sm ring-1 ring-slate-200">
            <div className="text-5xl mb-4">⚙️</div>
            <p className="text-slate-500">Chưa có cấu hình nào.</p>
          </div>
        ) : (
          <div className="rounded-2xl bg-white shadow-sm ring-1 ring-slate-200 overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full">
              <thead>
                <tr className="bg-slate-50 border-b border-slate-200">
                  <th className="text-left px-5 py-3 text-xs font-bold uppercase tracking-wide text-slate-500">Tham số</th>
                  <th className="text-left px-5 py-3 text-xs font-bold uppercase tracking-wide text-slate-500">Giá trị</th>
                  <th className="text-left px-5 py-3 text-xs font-bold uppercase tracking-wide text-slate-500">Mô tả</th>
                  <th className="text-right px-5 py-3 text-xs font-bold uppercase tracking-wide text-slate-500">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {configs.map(cfg => (
                  <tr key={cfg.key} className="hover:bg-slate-50 transition-colors">
                    <td className="px-5 py-4">
                      <span className="text-sm font-mono font-semibold text-slate-800 bg-slate-100 px-2 py-1 rounded">{cfg.key}</span>
                    </td>
                    <td className="px-5 py-4">
                      {editingKey === cfg.key ? (
                        <div className="flex items-center gap-2">
                          <input
                            value={editValue}
                            onChange={e => setEditValue(e.target.value)}
                            className="rounded-lg border border-orange-300 px-3 py-1.5 text-sm outline-none focus:border-orange-500 w-32"
                            autoFocus
                            onKeyDown={e => e.key === "Enter" && handleSave(cfg.key)}
                          />
                        </div>
                      ) : (
                        <span className="text-sm font-bold text-orange-600">{cfg.value}</span>
                      )}
                    </td>
                    <td className="px-5 py-4">
                      <span className="text-sm text-slate-500">{cfg.description || "—"}</span>
                    </td>
                    <td className="px-5 py-4 text-right">
                      {editingKey === cfg.key ? (
                        <div className="flex items-center justify-end gap-1.5">
                          <button
                            onClick={() => handleSave(cfg.key)}
                            disabled={saving}
                            className="px-3 py-1.5 text-xs font-semibold rounded-lg bg-orange-500 text-white hover:bg-orange-600 transition cursor-pointer disabled:opacity-50"
                          >
                            {saving ? "..." : "Lưu"}
                          </button>
                          <button
                            onClick={() => setEditingKey(null)}
                            className="px-3 py-1.5 text-xs font-semibold rounded-lg border border-slate-200 text-slate-500 hover:bg-slate-50 transition cursor-pointer"
                          >
                            Hủy
                          </button>
                        </div>
                      ) : (
                        <button
                          onClick={() => startEdit(cfg)}
                          className="px-3 py-1.5 text-xs font-semibold rounded-lg text-blue-500 hover:bg-blue-50 transition cursor-pointer"
                        >
                          ✏️ Sửa
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
