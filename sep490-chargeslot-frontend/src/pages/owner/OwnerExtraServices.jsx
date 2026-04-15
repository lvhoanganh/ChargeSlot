import { useState, useEffect } from "react";
import { stationApi, extraServiceApi } from "@/services/api";
import { showToast } from "@/components/Toast";
import { showConfirm } from "@/components/ConfirmDialog";

const emptyForm = { serviceName: "", description: "", price: "", totalStock: "", isRental: false, isActive: true };

export default function OwnerExtraServices() {
  const [stations, setStations] = useState([]);
  const [selectedStation, setSelectedStation] = useState(null);
  const [services, setServices] = useState([]);
  const [loading, setLoading] = useState(true);
  const [svcLoading, setSvcLoading] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [editId, setEditId] = useState(null);
  const [form, setForm] = useState(emptyForm);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");

  useEffect(() => {
    stationApi.getAll()
      .then(data => {
        const list = Array.isArray(data) ? data : [];
        const approvedList = list.filter(st => st.approvalStatus === "Approved");
        setStations(approvedList);
        if (approvedList.length > 0) setSelectedStation(approvedList[0].id);
      })
      .catch(() => setStations([]))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (!selectedStation) return;
    setSvcLoading(true);
    extraServiceApi.getAll(selectedStation)
      .then(data => setServices(Array.isArray(data) ? data : []))
      .catch(() => setServices([]))
      .finally(() => setSvcLoading(false));
  }, [selectedStation]);

  function openAdd() {
    setEditId(null);
    setForm(emptyForm);
    setFormError("");
    setShowForm(true);
  }

  function openEdit(svc) {
    setEditId(svc.id);
    setForm({
      serviceName: svc.serviceName || "",
      description: svc.description || "",
      price: svc.price ?? "",
      totalStock: svc.totalStock ?? "",
      isRental: svc.isRental === true,
      isActive: svc.isActive !== false,
    });
    setFormError("");
    setShowForm(true);
  }

  async function handleSave() {
    if (!form.serviceName.trim()) { setFormError("Tên dịch vụ không được trống!"); return; }
    if (form.price === "" || Number(form.price) < 0) { setFormError("Giá không hợp lệ!"); return; }

    setSaving(true);
    setFormError("");
    const body = {
      serviceName: form.serviceName.trim(),
      description: form.description.trim() || null,
      price: Number(form.price),
      totalStock: form.totalStock === "" || form.totalStock === null ? null : Number(form.totalStock),
      isRental: !!form.isRental,
      ...(editId && { isActive: form.isActive }),
    };

    try {
      if (editId) {
        await extraServiceApi.update(selectedStation, editId, body);
        showToast.success("Cập nhật dịch vụ thành công!");
      } else {
        await extraServiceApi.create(selectedStation, body);
        showToast.success("Thêm dịch vụ thành công!");
      }
      setShowForm(false);
      // Reload
      const data = await extraServiceApi.getAll(selectedStation);
      setServices(Array.isArray(data) ? data : []);
    } catch (err) {
      setFormError(err.message || "Lỗi lưu dịch vụ");
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(id) {
    if (!(await showConfirm("Bạn có chắc muốn xóa dịch vụ này?", "Xác nhận xóa dịch vụ"))) return;
    try {
      await extraServiceApi.delete(selectedStation, id);
      showToast.success("Đã xóa dịch vụ!");
      setServices(prev => prev.filter(s => s.id !== id));
    } catch (err) {
      showToast.error(err.message || "Không thể xóa dịch vụ (có thể đã có booking sử dụng)");
    }
  }

  async function handleToggle(svc) {
    try {
      await extraServiceApi.update(selectedStation, svc.id, {
        serviceName: svc.serviceName,
        description: svc.description,
        price: svc.price,
        totalStock: svc.totalStock,
        isRental: svc.isRental,   // ← quanọng: giữ nguyên, không reset về false
        isActive: !svc.isActive,
      });
      setServices(prev => prev.map(s => s.id === svc.id ? { ...s, isActive: !s.isActive } : s));
    } catch (err) {
      showToast.error(err.message || "Lỗi cập nhật");
    }
  }

  if (loading) {
    return (
      <div className="min-h-screen bg-slate-100 px-6 pt-20 pb-8 flex items-center justify-center">
        <div className="text-center">
          <div className="text-5xl mb-4">️</div>
          <p className="text-slate-500">Đang tải...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-100 px-6 pt-20 pb-8 text-slate-900">
      <div className="mx-auto max-w-4xl">
        {/* Header */}
        <div className="mb-6 rounded-2xl bg-white px-6 py-5 shadow-sm ring-1 ring-slate-200">
          <p className="text-sm font-medium uppercase tracking-[0.18em] text-purple-500">Chủ trạm</p>
          <h1 className="mt-2 text-2xl font-bold">Dịch vụ bổ sung</h1>
          <p className="mt-1 text-sm text-slate-600">Quản lý dịch vụ bổ sung tại các trạm sạc của bạn.</p>
        </div>

        {/* Station selector */}
        <div className="mb-4 flex items-center gap-3">
          <label className="text-sm font-semibold text-slate-700">Chọn trạm:</label>
          <select
            value={selectedStation || ""}
            onChange={e => setSelectedStation(Number(e.target.value))}
            className="rounded-lg border border-slate-200 px-3 py-2 text-sm outline-none focus:border-purple-400"
          >
            {stations.map(s => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </select>
          <button
            onClick={openAdd}
            className="ml-auto inline-flex items-center gap-1.5 rounded-lg bg-purple-500 px-4 py-2 text-sm font-semibold text-white hover:bg-purple-600 transition cursor-pointer"
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" /></svg>
            Thêm dịch vụ
          </button>
        </div>

        {/* Service list */}
        {svcLoading ? (
          <div className="text-center py-12 text-slate-400">Đang tải dịch vụ...</div>
        ) : services.length === 0 ? (
          <div className="rounded-2xl bg-white p-12 text-center shadow-sm ring-1 ring-slate-200">
            <div className="text-5xl mb-4"></div>
            <h2 className="text-lg font-bold text-slate-700 mb-2">Chưa có dịch vụ nào</h2>
            <p className="text-sm text-slate-500">Nhấn "Thêm dịch vụ" để tạo dịch vụ bổ sung cho trạm này.</p>
          </div>
        ) : (
          <div className="space-y-3">
            {services.map(svc => (
              <div key={svc.id} className="rounded-xl bg-white p-4 shadow-sm ring-1 ring-slate-200 flex items-center gap-4">
                <div className={`w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0 ${svc.isActive ? "bg-purple-100" : "bg-slate-100"}`}>
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke={svc.isActive ? "#7c3aed" : "#94a3b8"} strokeWidth="2"><path d="M20 7h-9" /><path d="M14 17H5" /><circle cx="17" cy="17" r="3" /><circle cx="7" cy="7" r="3" /></svg>
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="font-semibold text-slate-800">{svc.serviceName}</span>
                    {svc.isRental
                      ? <span className="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-blue-100 text-blue-600"> Cho thuê</span>
                      : <span className="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-orange-100 text-orange-600"> Bán</span>
                    }
                    {!svc.isActive && <span className="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-slate-100 text-slate-500">Ẩn</span>}
                  </div>
                  {svc.description && <p className="text-xs text-slate-500 mt-0.5">{svc.description}</p>}
                  <div className="flex items-center gap-3 mt-1 text-xs">
                    <span className="font-bold text-purple-600">{svc.price > 0 ? `${svc.price.toLocaleString("vi-VN")}đ` : "Miễn phí"}</span>
                    <span className="text-slate-400">
                      {svc.totalStock != null ? `Kho: ${svc.totalStock}` : "Không giới hạn"}
                    </span>
                  </div>
                </div>
                <div className="flex items-center gap-2 flex-shrink-0">
                  {/* Toggle active */}
                  <button
                    onClick={() => handleToggle(svc)}
                    title={svc.isActive ? "Ẩn dịch vụ" : "Hiện dịch vụ"}
                    className={`w-10 h-6 rounded-full relative transition-all cursor-pointer ${
                      svc.isActive ? "bg-purple-500" : "bg-slate-300"
                    }`}
                  >
                    <div className={`w-4 h-4 bg-white rounded-full absolute top-1 shadow transition-all ${
                      svc.isActive ? "left-5" : "left-1"
                    }`} />
                  </button>
                  {/* Sửa */}
                  <button
                    onClick={() => openEdit(svc)}
                    title="Chỉnh sửa dịch vụ"
                    style={{
                      display: "flex", alignItems: "center", gap: 4,
                      padding: "6px 10px", borderRadius: 8, border: "1.5px solid #bfdbfe",
                      background: "#eff6ff", color: "#2563eb", fontWeight: 600, fontSize: 12,
                      cursor: "pointer", whiteSpace: "nowrap",
                    }}
                    onMouseEnter={e => e.currentTarget.style.background = "#dbeafe"}
                    onMouseLeave={e => e.currentTarget.style.background = "#eff6ff"}
                  >
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                      <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                      <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                    </svg>
                    Sửa
                  </button>
                  {/* Xóa */}
                  <button
                    onClick={() => handleDelete(svc.id)}
                    title="Xóa dịch vụ"
                    style={{
                      display: "flex", alignItems: "center", gap: 4,
                      padding: "6px 10px", borderRadius: 8, border: "1.5px solid #fca5a5",
                      background: "#fff", color: "#ef4444", fontWeight: 600, fontSize: 12,
                      cursor: "pointer", whiteSpace: "nowrap",
                    }}
                    onMouseEnter={e => e.currentTarget.style.background = "#fef2f2"}
                    onMouseLeave={e => e.currentTarget.style.background = "#fff"}
                  >
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                      <polyline points="3 6 5 6 21 6" />
                      <path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6" />
                      <path d="M10 11v6M14 11v6" />
                      <path d="M9 6V4h6v2" />
                    </svg>
                    Xóa
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Add/Edit modal */}
        {showForm && (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm">
            <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md p-6 mx-4">
              <h3 className="text-lg font-bold text-slate-800 mb-4">{editId ? "Sửa dịch vụ" : "Thêm dịch vụ mới"}</h3>

              <div className="space-y-3">
                <div>
                  <label className="block text-xs font-semibold text-slate-600 mb-1">Tên dịch vụ *</label>
                  <input
                    value={form.serviceName}
                    onChange={e => setForm(p => ({ ...p, serviceName: e.target.value }))}
                    className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm outline-none focus:border-purple-400"
                    placeholder="VD: Cho thuê củ sạc"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-600 mb-1">Mô tả</label>
                  <input
                    value={form.description}
                    onChange={e => setForm(p => ({ ...p, description: e.target.value }))}
                    className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm outline-none focus:border-purple-400"
                    placeholder="VD: Củ sạc USB-C 65W"
                  />
                </div>
                <div className="flex gap-3">
                  <div className="flex-1">
                    <label className="block text-xs font-semibold text-slate-600 mb-1">Giá (VND) *</label>
                    <input
                      type="number"
                      value={form.price}
                      onChange={e => setForm(p => ({ ...p, price: e.target.value }))}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm outline-none focus:border-purple-400"
                      placeholder="0"
                      min="0"
                    />
                  </div>
                  <div className="flex-1">
                    <label className="block text-xs font-semibold text-slate-600 mb-1">Số lượng kho</label>
                    <input
                      type="number"
                      value={form.totalStock}
                      onChange={e => setForm(p => ({ ...p, totalStock: e.target.value }))}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm outline-none focus:border-purple-400"
                      placeholder="Để trống = không giới hạn"
                      min="0"
                    />
                  </div>
                </div>
                {/* isRental toggle */}
                <div className="flex items-center gap-2">
                  <label className="text-xs font-semibold text-slate-600">Loại dịch vụ:</label>
                  <button
                    type="button"
                    onClick={() => setForm(p => ({ ...p, isRental: !p.isRental }))}
                    className={`w-10 h-6 rounded-full relative transition cursor-pointer ${form.isRental ? "bg-blue-500" : "bg-orange-400"}`}
                  >
                    <div className={`w-4 h-4 bg-white rounded-full absolute top-1 transition-all ${form.isRental ? "left-5" : "left-1"}`} />
                  </button>
                  <span className="text-xs font-semibold">
                    {form.isRental
                      ? <span className="text-blue-600"> Cho thuê (trả lại)</span>
                      : <span className="text-orange-500"> Bán / Tiêu hóa</span>
                    }
                  </span>
                </div>
                {editId && (
                  <div className="flex items-center gap-2">
                    <label className="text-xs font-semibold text-slate-600">Hiển thị:</label>
                    <button
                      onClick={() => setForm(p => ({ ...p, isActive: !p.isActive }))}
                      className={`w-10 h-6 rounded-full relative transition cursor-pointer ${form.isActive ? "bg-purple-500" : "bg-slate-300"}`}
                    >
                      <div className={`w-4 h-4 bg-white rounded-full absolute top-1 transition-all ${form.isActive ? "left-5" : "left-1"}`} />
                    </button>
                    <span className="text-xs text-slate-500">{form.isActive ? "Đang hiện" : "Đang ẩn"}</span>
                  </div>
                )}
              </div>

              {formError && (
                <div className="mt-3 text-xs text-red-500 font-medium bg-red-50 rounded-lg px-3 py-2 border border-red-200">
                  ️ {formError}
                </div>
              )}

              <div className="flex gap-2 mt-5">
                <button
                  onClick={handleSave}
                  disabled={saving}
                  className="flex-1 py-2.5 rounded-xl bg-purple-500 text-white font-semibold text-sm hover:bg-purple-600 transition cursor-pointer disabled:opacity-50"
                >
                  {saving ? "Đang lưu..." : editId ? "Cập nhật" : "Thêm"}
                </button>
                <button
                  onClick={() => setShowForm(false)}
                  className="flex-1 py-2.5 rounded-xl border border-slate-200 text-slate-600 font-semibold text-sm hover:bg-slate-50 transition cursor-pointer"
                >
                  Hủy
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
