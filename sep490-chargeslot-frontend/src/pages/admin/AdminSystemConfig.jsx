import { useState, useEffect } from "react";
import { adminConfigApi } from "@/services/api";
import { showToast } from "@/components/Toast";

const CONFIG_GROUPS = [
  {
    title: " Tài chính & Phí",
    fields: [
      { key: "vAT_Rate", label: "Thuế VAT (%)", hint: "VD: 0.08 = 8%", type: "decimal" },
      { key: "platform_Fee_Rate", label: "Phí nền tảng (%)", hint: "VD: 0.05 = 5%", type: "decimal" },
      { key: "loyalty_Earn_Rate", label: "Tỉ lệ tích điểm (%)", hint: "VD: 0.05 = 5%", type: "decimal" },
    ],
  },
  {
    title: " Chính sách Hoàn tiền",
    fields: [
      { key: "refundPolicy100_Hrs", label: "Hoàn 100% (giờ trước)", hint: "Hủy trước bao nhiêu giờ được hoàn 100%", type: "int" },
      { key: "refundPolicy50_Hrs", label: "Hoàn 50% (giờ trước)", hint: "Hủy trước bao nhiêu giờ được hoàn 50%", type: "int" },
    ],
  },
  {
    title: "️ Thời gian & Cửa sổ",
    fields: [
      { key: "payment_Expiry_Minutes", label: "Thanh toán hết hạn (phút)", hint: "Thời gian chờ thanh toán trước khi hủy", type: "int" },
      { key: "checkIn_Window_Minutes", label: "Cửa sổ check-in (phút)", hint: "Trước giờ đặt bao nhiêu phút được check in", type: "int" },
      { key: "noShow_Grace_Minutes", label: "Gia hạn no-show (phút)", hint: "Thời gian chờ trước khi bị đánh dấu no-show", type: "int" },
      { key: "slot_Buffer_Minutes", label: "Buffer giữa 2 slot (phút)", hint: "Thời gian cách nhau giữa 2 lịch đặt", type: "int" },
      { key: "oTP_Expiry_Minutes", label: "OTP hết hạn (phút)", hint: "Thời gian hiệu lực của mã OTP", type: "int" },
      { key: "oTP_Cooldown_Seconds", label: "Cooldown OTP xác thực Email (giây)", hint: "Thời gian chờ tối thiểu giữa 2 lần gửi OTP email (dùng cho luồng đổi email, KHÔNG liên quan đến OTP SMS Firebase)", type: "int" },
      { key: "min_Booking_Lead_Minutes", label: "Đặt trước tối thiểu (phút)", hint: "Số phút tối thiểu phải đặt trước giờ sạc", type: "int" },
    ],
  },
  {
    title: " Tự động xác nhận",
    fields: [
      { key: "withdraw_AutoConfirm_Hours", label: "Auto-confirm rút tiền (giờ)", hint: "Giờ chờ tự động xác nhận rút tiền nếu User không phản hồi (1-168)", type: "int" },
      { key: "invoice_AutoConfirm_Hours", label: "Auto-confirm hóa đơn (giờ)", hint: "Giờ chờ tự động xác nhận hóa đơn nếu Driver không phản hồi (1-168)", type: "int" },
      { key: "reminder_Window_Hours", label: "Cửa sổ nhắc nhở (giờ)", hint: "Gửi nhắc nhở trước deadline bao nhiêu giờ (1-24)", type: "int" },
    ],
  },
  {
    title: "️ Tranh chấp",
    fields: [
      { key: "dispute_Limit_Per_Month", label: "Giới hạn tranh chấp/tháng", hint: "Số lần tạo dispute tối đa mỗi tháng", type: "int" },
      { key: "dispute_OwnerEvidence_Hours", label: "Chủ trạm nộp bằng chứng (giờ)", hint: "Thời hạn owner phản hồi dispute", type: "int" },
      { key: "dispute_AdminReview_Hours", label: "Admin xét xử (giờ)", hint: "Thời hạn admin giải quyết dispute", type: "int" },
    ],
  },
];

export default function AdminSystemConfig() {
  const [form, setForm] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [seeding, setSeeding] = useState(false);
  const [secModal, setSecModal] = useState(false);
  const [secPass, setSecPass] = useState("");

  useEffect(() => {
    loadConfigs();
  }, []);

  async function loadConfigs() {
    setLoading(true);
    try {
      const data = await adminConfigApi.getAll();
      setForm(data);
    } catch {
      showToast.error("Không thể tải cấu hình hệ thống");
      setForm(null);
    } finally {
      setLoading(false);
    }
  }

  async function handleSeed() {
    setSeeding(true);
    try {
      await adminConfigApi.seed();
      showToast.success("Đã khởi tạo cấu hình mặc định!");
      await loadConfigs();
    } catch (err) {
      showToast.error(err.message || "Lỗi khởi tạo");
    } finally {
      setSeeding(false);
    }
  }

  function handleChange(key, value) {
    setForm(prev => {
      // Find the actual key in the form object (case-insensitive) to prevent casing mismatches
      const actualKey = Object.keys(prev).find(k => k.toLowerCase() === key.toLowerCase()) || key;
      return { ...prev, [actualKey]: value };
    });
  }

  function getFieldValue(key) {
    if (!form) return "";
    const actualKey = Object.keys(form).find(k => k.toLowerCase() === key.toLowerCase());
    return actualKey != null && form[actualKey] != null ? form[actualKey] : "";
  }

  function handleSubmitClick(e) {
    e.preventDefault();
    setSecModal(true);
    setSecPass("");
  }

  async function confirmSave(e) {
    if (e && e.preventDefault) e.preventDefault();
    if (!secPass) return showToast.error("Vui lòng nhập mật khẩu cấp 2");
    setSaving(true);
    try {
      await adminConfigApi.update({ ...form, secondaryPassword: secPass });
      setSecModal(false);
      setSecPass("");
      showToast.success(" Cập nhật cấu hình thành công!");
      await loadConfigs();
    } catch (err) {
      showToast.error(err.message || "Lỗi cập nhật (Sai mật khẩu cấp 2?)");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <div className="min-h-screen bg-slate-100 pt-20 flex items-center justify-center">
        <div className="text-center">
          <div className="text-5xl mb-4 animate-spin">️</div>
          <p className="text-slate-500">Đang tải cấu hình...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-100 pt-20 px-4 md:px-8 pb-12">
      <div className="max-w-4xl mx-auto">

        {/* Header */}
        <div className="mb-6 rounded-2xl bg-white px-6 py-5 shadow-sm ring-1 ring-slate-200 flex items-center justify-between flex-wrap gap-3">
          <div>
            <p className="text-sm font-medium uppercase tracking-[0.18em] text-orange-500">Quản trị</p>
            <h1 className="mt-1 text-2xl font-bold text-slate-900">Cấu hình hệ thống</h1>
            <p className="mt-0.5 text-sm text-slate-500">Siêu tham số điều khiển hành vi của ChargeSlot.</p>
          </div>
          {!form && (
            <button
              onClick={handleSeed}
              disabled={seeding}
              className="px-4 py-2 rounded-xl bg-blue-500 text-white text-sm font-semibold hover:bg-blue-600 transition disabled:opacity-50 cursor-pointer"
            >
              {seeding ? "Đang khởi tạo..." : " Khởi tạo cấu hình mặc định"}
            </button>
          )}
        </div>

        {!form ? (
          <div className="rounded-2xl bg-white p-12 text-center shadow-sm ring-1 ring-slate-200">
            <div className="text-5xl mb-4">️</div>
            <p className="text-slate-500 mb-4">Chưa có cấu hình nào trong hệ thống.</p>
            <button
              onClick={handleSeed}
              disabled={seeding}
              className="px-6 py-2.5 rounded-xl bg-orange-500 text-white font-semibold hover:bg-orange-600 transition disabled:opacity-50 cursor-pointer"
            >
              {seeding ? "Đang khởi tạo..." : " Khởi tạo ngay"}
            </button>
          </div>
        ) : (
          <form onSubmit={handleSubmitClick} className="flex flex-col gap-6">
            {CONFIG_GROUPS.map(group => (
              <div key={group.title} className="rounded-2xl bg-white shadow-sm ring-1 ring-slate-200 overflow-hidden">
                <div className="px-6 py-4 border-b border-slate-100 bg-slate-50/60">
                  <h2 className="font-bold text-slate-800 text-base">{group.title}</h2>
                </div>
                <div className="px-6 py-5 grid grid-cols-1 sm:grid-cols-2 gap-5">
                  {group.fields.map(field => (
                    <div key={field.key}>
                      <label className="block text-sm font-semibold text-slate-700 mb-1">
                        {field.label}
                      </label>
                      <input
                        type="number"
                        step={field.type === "decimal" ? "0.01" : "1"}
                        min="0"
                        value={getFieldValue(field.key)}
                        onChange={e => handleChange(field.key, field.type === "decimal" ? parseFloat(e.target.value) : parseInt(e.target.value))}
                        className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm outline-none focus:border-orange-400 focus:ring-2 focus:ring-orange-100 transition"
                        required
                      />
                      {field.hint && (
                        <p className="text-xs text-slate-400 mt-1">{field.hint}</p>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            ))}

            <div className="flex justify-end">
              <button
                type="submit"
                className="px-8 py-3 rounded-xl bg-orange-500 text-white font-bold hover:bg-orange-600 transition shadow-md shadow-orange-200 cursor-pointer"
              >
                 Lưu thay đổi
              </button>
            </div>
          </form>
        )}

        {/* Modal MK Cấp 2 */}
        {secModal && (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
            <div className="bg-white rounded-2xl w-full max-w-sm p-6 shadow-2xl">
              <h2 className="text-xl font-bold text-gray-800 mb-1"> Bảo mật cấp 2</h2>
              <p className="text-sm text-gray-500 mb-4">Nhập mật khẩu cấp 2 để xác nhận thay đổi cấu hình hệ thống.</p>
              <form onSubmit={confirmSave} className="flex flex-col gap-4">
                <input
                  type="password"
                  autoFocus
                  placeholder="Nhập mật khẩu cấp 2..."
                  value={secPass}
                  onChange={e => setSecPass(e.target.value)}
                  className="w-full p-2.5 border rounded-lg outline-none focus:border-orange-500 text-sm"
                  required
                />
                <div className="flex gap-2">
                  <button type="submit" disabled={saving}
                    className="flex-1 py-2.5 font-semibold bg-orange-500 text-white hover:bg-orange-600 rounded-lg disabled:opacity-50 transition cursor-pointer">
                    {saving ? "Đang lưu..." : "Xác nhận"}
                  </button>
                  <button type="button" onClick={() => { setSecModal(false); setSecPass(""); }} disabled={saving}
                    className="flex-1 py-2.5 font-semibold bg-gray-100 text-gray-700 hover:bg-gray-200 rounded-lg transition cursor-pointer">
                    Hủy
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
