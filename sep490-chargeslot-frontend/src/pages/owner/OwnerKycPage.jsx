import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { authApi, ownerKycApi } from "@/services/api";
import { showToast } from "@/components/Toast";
import { formatDateVN } from "@/utils/dateVN";

/** Convert "DD/MM/YYYY" → "YYYY-MM-DD" để dùng trong input[type=date] */
function toInputDate(ddmmyyyy) {
  if (!ddmmyyyy) return "";
  const parts = ddmmyyyy.split("/");
  if (parts.length === 3) return `${parts[2]}-${parts[1]}-${parts[0]}`;
  return "";
}

function KycStatusBadge({ status }) {
  const map = {
    Unverified:    { label: "Chưa xác thực",              bg: "#f1f5f9", color: "#64748b", dot: "#94a3b8" },
    Pending:       { label: "Đang chờ duyệt",              bg: "#fffbeb", color: "#f59e0b", dot: "#f59e0b" },
    Approved:      { label: "✅ Đã xác thực",              bg: "#f0fdf4", color: "#16a34a", dot: "#16a34a" },
    Rejected:      { label: "❌ Bị từ chối",               bg: "#fef2f2", color: "#dc2626", dot: "#dc2626" },
    PendingUpdate: { label: "🔄 Chờ duyệt bản cập nhật",   bg: "#eff6ff", color: "#2563eb", dot: "#3b82f6" },
  };
  const cfg = map[status] || map.Unverified;
  return (
    <span style={{ background: cfg.bg, color: cfg.color, display: "inline-flex", alignItems: "center", gap: 6, padding: "4px 14px", borderRadius: 50, fontSize: 13, fontWeight: 700 }}>
      <span style={{ width: 7, height: 7, borderRadius: "50%", background: cfg.dot, display: "inline-block" }} />
      {cfg.label}
    </span>
  );
}

export default function OwnerKycPage() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [profile, setProfile] = useState(null);
  const [kycStatus, setKycStatus] = useState(null);
  const [updateMode, setUpdateMode] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [agreed, setAgreed] = useState(false);
  // submitMode: true khi user muốn nộp lần đầu / nộp lại (Rejected) / cập nhật (Approved)
  const [submitMode, setSubmitMode] = useState(false);

  useEffect(() => {
    loadProfile();
  }, []);

  async function loadProfile() {
    setLoading(true);
    try {
      const data = await ownerKycApi.getStatus();
      setProfile(data);
      setKycStatus(data.kycStatus || "Unverified");
    } catch {
      // Fallback về getMe
      try {
        const me = await authApi.getMe();
        setKycStatus(me.kycStatus || "Unverified");
      } catch (err) {
        showToast.error("Không thể tải thông tin hồ sơ: " + (err.message || "Lỗi không xác định"));
      }
    } finally {
      setLoading(false);
    }
  }

  const handleSubmit = async (e) => {
    e.preventDefault();
    const formData = new FormData(e.target);

    const idCard = formData.get("IdCardNumber")?.trim();
    if (!/^\d{12}$/.test(idCard)) {
      showToast.error("Số CCCD/CMND không hợp lệ. Vui lòng nhập đúng 12 chữ số.");
      return;
    }

    const idCardDate = formData.get("IdCardDate")?.trim();
    const dateMatch = idCardDate?.match(/^(\d{4})-(0[1-9]|1[012])-(0[1-9]|[12][0-9]|3[01])$/);
    if (!dateMatch) {
      showToast.error("Ngày cấp CCCD chưa hợp lệ. Vui lòng chọn từ lịch.");
      return;
    }
    formData.set("IdCardDate", `${dateMatch[3]}/${dateMatch[2]}/${dateMatch[1]}`);

    const businessLicense = formData.get("BusinessLicenseNumber")?.trim();
    if (!/^\d{10}$/.test(businessLicense)) {
      showToast.error("Mã số ĐKKD không hợp lệ. Vui lòng nhập chính xác 10 chữ số.");
      return;
    }

    setSubmitting(true);
    try {
      await ownerKycApi.submit(formData);
      const isUpdate = kycStatus === "Approved";
      showToast.success(isUpdate
        ? "Đã gửi yêu cầu cập nhật hồ sơ! Vui lòng chờ Admin xét duyệt."
        : kycStatus === "Rejected"
          ? "Đã gửi lại hồ sơ KYC! Vui lòng chờ Admin xét duyệt."
          : "Đã nộp hồ sơ KYC! Vui lòng chờ Admin xét duyệt.");
      setUpdateMode(false);
      setSubmitMode(false);
      setAgreed(false);
      await loadProfile();
    } catch (err) {
      showToast.error(err.message || "Có lỗi xảy ra khi gửi hồ sơ");
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="min-h-[60vh] flex items-center justify-center">
        <div className="text-center">
          <div className="w-10 h-10 border-4 border-orange-200 border-t-orange-500 rounded-full animate-spin mx-auto mb-4" />
          <p className="text-slate-500">Đang tải thông tin hồ sơ...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-50 pt-24 pb-16 px-4 sm:px-6">
      <div className="max-w-4xl mx-auto">

        {/* Header */}
        <div className="flex items-center gap-3 mb-6">
          <button
            onClick={() => navigate(-1)}
            className="p-2 rounded-xl hover:bg-slate-200 transition text-slate-500"
          >
            ←
          </button>
          <div>
            <h1 className="text-2xl font-bold text-slate-900">Hồ sơ của tôi</h1>
            <p className="text-sm text-slate-500 mt-0.5">Quản lý thông tin xác thực danh tính chủ trạm</p>
          </div>
        </div>

        {/* Status Card */}
        <div className="bg-white rounded-2xl shadow-sm ring-1 ring-slate-200 p-6 mb-6">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div>
              <p className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1.5">Trạng thái xác thực</p>
              <KycStatusBadge status={kycStatus} />
              {profile?.kycSubmittedAt && (
                <p className="text-xs text-slate-400 mt-2">
                  Gửi lúc: {formatDateVN(profile.kycSubmittedAt)}
                  {profile?.kycReviewedAt && ` · Duyệt: ${formatDateVN(profile.kycReviewedAt)}`}
                </p>
              )}
            </div>

            {/* Action button based on status */}
            {kycStatus === "Approved" && !updateMode && !submitMode && (
              <button
                onClick={() => { setUpdateMode(true); setSubmitMode(true); setAgreed(false); }}
                className="flex items-center gap-2 px-5 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-xl transition shadow-sm"
              >
                🔄 Cập nhật hồ sơ
              </button>
            )}
            {kycStatus === "Rejected" && !submitMode && (
              <button
                onClick={() => { setSubmitMode(true); setAgreed(false); }}
                className="flex items-center gap-2 px-5 py-2.5 bg-red-600 hover:bg-red-700 text-white text-sm font-semibold rounded-xl transition shadow-sm"
              >
                📎 Nộp lại hồ sơ
              </button>
            )}
            {kycStatus === "Unverified" && !submitMode && (
              <button
                onClick={() => { setSubmitMode(true); setAgreed(false); }}
                className="flex items-center gap-2 px-5 py-2.5 bg-green-600 hover:bg-green-700 text-white text-sm font-semibold rounded-xl transition shadow-sm"
              >
                📄 Nộp hồ sơ KYC
              </button>
            )}
            {kycStatus === "PendingUpdate" && (
              <div className="text-sm text-blue-600 font-semibold bg-blue-50 px-4 py-2 rounded-xl border border-blue-200">
                Đang chờ Admin xét duyệt bản cập nhật
              </div>
            )}
            {kycStatus === "Pending" && (
              <div className="text-sm text-amber-600 font-semibold bg-amber-50 px-4 py-2 rounded-xl border border-amber-200">
                Đang chờ Admin xét duyệt hồ sơ lần đầu
              </div>
            )}
          </div>

          {/* Reject reason */}
          {profile?.kycRejectReason && (kycStatus === "Rejected" || kycStatus === "Approved") && (
            <div className="mt-4 p-4 bg-red-50 border border-red-200 rounded-xl flex gap-3 text-red-700 text-sm">
              <span className="text-lg flex-shrink-0">⚠️</span>
              <div>
                <strong className="block mb-0.5">Lý do từ chối gần nhất:</strong>
                {profile.kycRejectReason}
              </div>
            </div>
          )}

          {/* PendingUpdate banner */}
          {kycStatus === "PendingUpdate" && (
            <div className="mt-4 p-4 bg-blue-50 border border-blue-200 rounded-xl flex gap-3 text-blue-800 text-sm">
              <span className="text-lg flex-shrink-0">🔄</span>
              <div>
                <strong className="block mb-0.5">Yêu cầu cập nhật đang chờ duyệt</strong>
                Bạn vẫn có thể sử dụng hệ thống bình thường trong thời gian chờ.
                Nếu bị từ chối, thông tin cũ sẽ được tự động khôi phục.
              </div>
            </div>
          )}
          {/* Current Info (nếu đã có profile) */}
          {/* Chỉ hiện khi không mở form, và đã có dữ liệu KYC */}
        </div>

        {profile && !submitMode && (kycStatus === "Approved" || kycStatus === "PendingUpdate" || kycStatus === "Rejected") && (
          <div className="bg-white rounded-2xl shadow-sm ring-1 ring-slate-200 p-6 mb-6">
            <h2 className="text-base font-bold text-slate-800 mb-4 flex items-center gap-2">
              📋 Thông tin hiện tại
              {kycStatus === "PendingUpdate" && (
                <span className="text-xs font-normal text-blue-600 bg-blue-50 px-2 py-0.5 rounded-full border border-blue-200">Bản cập nhật đang chờ duyệt</span>
              )}
            </h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-sm">
              <InfoRow label="Tên doanh nghiệp" value={profile.businessName} />
              <InfoRow label="Mã số thuế" value={profile.taxCode} />
              <InfoRow label="Số CCCD/CMND" value={profile.idCardNumber} />
              <InfoRow label="Ngày cấp CCCD" value={profile.idCardDate} />
              <InfoRow label="Số GPKD" value={profile.businessLicenseNumber} />
              <InfoRow label="Địa chỉ ĐKKD" value={profile.address} className="sm:col-span-2" />
            </div>

            {/* Ảnh xác thực */}
            {(profile.frontIdCardUrl || profile.backIdCardUrl || profile.businessLicenseUrl) && (
              <div className="mt-5">
                <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-3">Hình ảnh xác thực</p>
                <div className="grid grid-cols-3 gap-3">
                  {profile.frontIdCardUrl && (
                    <a href={profile.frontIdCardUrl} target="_blank" rel="noreferrer" className="block rounded-xl overflow-hidden border border-slate-200 hover:shadow-md transition">
                      <div className="bg-slate-100 px-3 py-1.5 text-xs font-semibold text-slate-500 border-b border-slate-200">CCCD Mặt Trước</div>
                      <div style={{ height: 120, background: `url(${profile.frontIdCardUrl}) center / cover no-repeat`, backgroundColor: "#f8fafc" }} />
                    </a>
                  )}
                  {profile.backIdCardUrl && (
                    <a href={profile.backIdCardUrl} target="_blank" rel="noreferrer" className="block rounded-xl overflow-hidden border border-slate-200 hover:shadow-md transition">
                      <div className="bg-slate-100 px-3 py-1.5 text-xs font-semibold text-slate-500 border-b border-slate-200">CCCD Mặt Sau</div>
                      <div style={{ height: 120, background: `url(${profile.backIdCardUrl}) center / cover no-repeat`, backgroundColor: "#f8fafc" }} />
                    </a>
                  )}
                  {profile.businessLicenseUrl && (
                    <a href={profile.businessLicenseUrl} target="_blank" rel="noreferrer" className="block rounded-xl overflow-hidden border border-slate-200 hover:shadow-md transition">
                      <div className="bg-slate-100 px-3 py-1.5 text-xs font-semibold text-slate-500 border-b border-slate-200">Giấy phép KD</div>
                      <div style={{ height: 120, background: `url(${profile.businessLicenseUrl}) center / cover no-repeat`, backgroundColor: "#f8fafc" }} />
                    </a>
                  )}
                </div>
              </div>
            )}
          </div>
        )}

        {/* Form nộp / cập nhật hồ sơ */}
        {submitMode && (
          <div className="bg-white rounded-2xl shadow-sm ring-1 ring-slate-200 p-6 sm:p-8">
            <div className="flex items-center justify-between mb-6">
              <h2 className="text-lg font-bold text-slate-900">
                {kycStatus === "Approved" ? "🔄 Cập nhật hồ sơ"
                  : kycStatus === "Rejected" ? "📎 Nộp lại hồ sơ"
                  : "📄 Nộp hồ sơ KYC"}
              </h2>
              <button onClick={() => { setSubmitMode(false); setUpdateMode(false); setAgreed(false); }} className="text-slate-400 hover:text-slate-600 text-sm font-medium">
                ✕ Hủy
              </button>
            </div>

            <div className={`mb-5 p-4 rounded-xl text-sm border ${
              kycStatus === "Rejected"
                ? "bg-red-50 border-red-200 text-red-800"
                : "bg-blue-50 border-blue-200 text-blue-800"
            }`}>
              {kycStatus === "Rejected" ? (
                <>
                  <strong>⚠️ Hồ sơ của bạn đã bị từ chối.</strong> Vui lòng xem lý do từ chối ở trên và nộp lại hồ sơ với thông tin chính xác.
                  <strong className="block mt-1">Bắt buộc tải lại tất cả 3 ảnh khi nộp lại.</strong>
                </>
              ) : kycStatus === "Approved" ? (
                <>
                  <strong>Lưu ý:</strong> Hồ sơ cũ vẫn có hiệu lực trong khi chờ duyệt bản cập nhật.
                  Nếu bị từ chối, thông tin cũ sẽ được tự động khôi phục.
                  <strong className="block mt-1">Bắt buộc tải lại tất cả ảnh khi cập nhật.</strong>
                </>
              ) : (
                <>
                  <strong>Lưu ý:</strong> Sau khi nộp hồ sơ, Admin sẽ xét duyệt trong vòng 1-3 ngày làm việc.
                  <strong className="block mt-1">Bắt buộc tải đủ cả 3 ảnh xác thực.</strong>
                </>
              )}
            </div>

            <form onSubmit={handleSubmit} className="grid grid-cols-1 lg:grid-cols-12 gap-8">
              {/* Left: Text fields */}
              <div className="lg:col-span-7 space-y-5">
                <h3 className="text-base font-bold text-slate-800 border-b border-slate-100 pb-3">📝 Thông tin cơ bản</h3>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">Số CCCD/CMND <span className="text-red-500">*</span></label>
                    <input
                      type="text" name="IdCardNumber" required
                      defaultValue={profile?.idCardNumber || ""}
                      className="w-full px-4 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none transition"
                      placeholder="12 chữ số"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">Ngày cấp CCCD <span className="text-red-500">*</span></label>
                    <input
                      type="date" name="IdCardDate" required
                      defaultValue={toInputDate(profile?.idCardDate)}
                      className="w-full px-4 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none transition text-slate-700"
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-1.5">Mã số thuế <span className="text-red-500">*</span></label>
                  <input
                    type="text" name="TaxCode" required
                    defaultValue={profile?.taxCode || ""}
                    className="w-full px-4 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none transition"
                    placeholder="Mã số thuế"
                  />
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">Tên doanh nghiệp <span className="text-red-500">*</span></label>
                    <input
                      type="text" name="BusinessName" required
                      defaultValue={profile?.businessName || ""}
                      className="w-full px-4 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none transition"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">Số GPKD <span className="text-red-500">*</span></label>
                    <input
                      type="text" name="BusinessLicenseNumber" required
                      defaultValue={profile?.businessLicenseNumber || ""}
                      className="w-full px-4 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none transition"
                      placeholder="10 chữ số"
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-1.5">Địa chỉ ĐKKD <span className="text-red-500">*</span></label>
                  <textarea
                    name="Address" required rows="3"
                    defaultValue={profile?.address || ""}
                    className="w-full px-4 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none transition resize-none"
                  />
                </div>
              </div>

              {/* Right: File uploads */}
              <div className="lg:col-span-5 space-y-5">
                <div className="bg-blue-50 border border-blue-100 rounded-2xl p-5">
                  <h3 className="text-base font-bold text-blue-800 border-b border-blue-200/60 pb-3 mb-4">🖼️ Tải lại ảnh xác thực</h3>
                  <p className="text-xs text-blue-600 mb-4 bg-blue-100 rounded-lg px-3 py-2 font-medium">
                    ⚠️ Phải tải lại tất cả 3 ảnh. Không được bỏ trống.
                  </p>
                  <div className="space-y-4">
                    <div>
                      <label className="block text-xs font-semibold text-blue-800 mb-1.5">Mặt trước CCCD <span className="text-red-500">*</span></label>
                      <input type="file" name="FrontIdCardImage" accept="image/*" required className="block w-full text-sm text-slate-600 bg-white border border-slate-200 rounded-xl file:mr-3 file:py-2 file:px-4 file:border-0 file:text-sm file:font-semibold file:bg-blue-100 file:text-blue-700 hover:file:bg-blue-200 transition cursor-pointer" />
                    </div>
                    <div>
                      <label className="block text-xs font-semibold text-blue-800 mb-1.5">Mặt sau CCCD <span className="text-red-500">*</span></label>
                      <input type="file" name="BackIdCardImage" accept="image/*" required className="block w-full text-sm text-slate-600 bg-white border border-slate-200 rounded-xl file:mr-3 file:py-2 file:px-4 file:border-0 file:text-sm file:font-semibold file:bg-blue-100 file:text-blue-700 hover:file:bg-blue-200 transition cursor-pointer" />
                    </div>
                    <div>
                      <label className="block text-xs font-semibold text-blue-800 mb-1.5">Ảnh Giấy phép KD <span className="text-red-500">*</span></label>
                      <input type="file" name="BusinessLicenseImage" accept="image/*" required className="block w-full text-sm text-slate-600 bg-white border border-slate-200 rounded-xl file:mr-3 file:py-2 file:px-4 file:border-0 file:text-sm file:font-semibold file:bg-blue-100 file:text-blue-700 hover:file:bg-blue-200 transition cursor-pointer" />
                    </div>
                  </div>
                </div>

                <div>
                  <div className="flex items-start gap-2.5 mb-4">
                    <input
                      type="checkbox"
                      id="kyc-update-agree"
                      checked={agreed}
                      onChange={e => setAgreed(e.target.checked)}
                      className="mt-0.5 flex-shrink-0 accent-blue-600 cursor-pointer"
                      style={{ width: "18px", height: "18px" }}
                    />
                    <label htmlFor="kyc-update-agree" className="text-sm text-slate-600 font-medium cursor-pointer select-none">
                      Tôi xác nhận thông tin và ảnh cung cấp là chính xác và hợp lệ.
                    </label>
                  </div>

                  <button
                    type="submit"
                    disabled={submitting || !agreed}
                    className="w-full flex items-center justify-center gap-2 py-3.5 px-6 rounded-xl bg-gradient-to-r from-blue-600 to-blue-700 hover:from-blue-700 hover:to-blue-800 text-white font-bold text-base transition shadow-lg shadow-blue-500/20 disabled:opacity-70 disabled:cursor-not-allowed"
                  >
                    {submitting ? (
                      <>
                        <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                        Đang gửi...
                      </>
                    ) : "🔄 Gửi yêu cầu cập nhật"}
                  </button>
                </div>
              </div>
            </form>
          </div>
        )}

      </div>
    </div>
  );
}

function InfoRow({ label, value, className = "" }) {
  return (
    <div className={`bg-slate-50 rounded-xl p-3 ${className}`}>
      <p className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-0.5">{label}</p>
      <p className="font-semibold text-slate-800">{value || "—"}</p>
    </div>
  );
}
