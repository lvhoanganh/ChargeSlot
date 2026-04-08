import React, { useEffect, useState } from "react";
import { authApi, ownerKycApi } from "@/services/api";
import { showToast } from "@/components/Toast";

export default function OwnerKycGuard({ children }) {
  const [loading, setLoading] = useState(true);
  const [kycStatus, setKycStatus] = useState(null);
  const [kycRejectReason, setKycRejectReason] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [agreed, setAgreed] = useState(false);

  useEffect(() => {
    fetchMe();
  }, []);

  async function fetchMe() {
    setLoading(true);
    try {
      // We can use getMe or getStatus, getMe gets everything
      // But getStatus gets the specific OwnerKycProfileDto which might have the existing images info if we need it
      // We will just use getMe to get kycStatus fast, then if Unverified/Rejected we show form
      const me = await authApi.getMe();
      setKycStatus(me.kycStatus || "Unverified");
      setKycRejectReason(me.kycRejectReason);
    } catch (err) {
      showToast.error("Lỗi xác minh danh tính: " + (err.message || "Không rõ"));
    } finally {
      setLoading(false);
    }
  }

  const handleSubmit = async (e) => {
    e.preventDefault();
    const formData = new FormData(e.target);

    // Validation
    const idCard = formData.get("IdCardNumber")?.trim();
    if (!/^\d{12}$/.test(idCard)) {
      showToast.error("Số CCCD/CMND không hợp lệ. Vui lòng nhập đúng 12 chữ số.");
      return;
    }

    const idCardDate = formData.get("IdCardDate")?.trim();
    let formattedDate = idCardDate;
    const dateMatch = idCardDate.match(/^(\d{4})-(0[1-9]|1[012])-(0[1-9]|[12][0-9]|3[01])$/);
    if (!dateMatch) {
      showToast.error("Khung ngày cấp Căn cước công dân chưa hợp lệ. Vui lòng chọn từ trên lịch.");
      return;
    } else {
      formattedDate = `${dateMatch[3]}/${dateMatch[2]}/${dateMatch[1]}`;
      formData.set("IdCardDate", formattedDate); // Format it back to DD/MM/YYYY for backend/Admin
    }

    const businessLicense = formData.get("BusinessLicenseNumber")?.trim();
    if (!/^\d{10}$/.test(businessLicense)) {
      showToast.error("Mã số Đăng ký kinh doanh không hợp lệ. Vui lòng nhập chính xác 10 chữ số.");
      return;
    }

    setSubmitting(true);
    try {
      await ownerKycApi.submit(formData);
      showToast.success("Đã nộp hồ sơ, vui lòng chờ duyệt!");
      await fetchMe();
    } catch (err) {
      showToast.error(err.message || "Có lỗi xảy ra khi nộp hồ sơ");
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="min-h-[60vh] flex items-center justify-center">
        <div className="text-center">
          <div className="w-10 h-10 border-4 border-orange-200 border-t-orange-500 rounded-full animate-spin mx-auto mb-4"></div>
          <p className="text-slate-500">Đang kiểm tra trạng thái xác thực...</p>
        </div>
      </div>
    );
  }

  // Approved -> Continue to original content
  if (kycStatus === "Approved") {
    return children;
  }

  // Common wrapper for KYC Screens
  const KycContainer = ({ children }) => (
    <div className="min-h-screen bg-slate-100 flex justify-center pt-24 pb-12 px-4 sm:px-6">
      <div className="max-w-5xl w-full bg-white rounded-3xl shadow-sm ring-1 ring-slate-200 p-6 sm:p-10">
        <div className="text-center mb-8">
          <div className="w-16 h-16 bg-gradient-to-br from-orange-400 to-orange-600 rounded-2xl flex items-center justify-center mx-auto mb-4 text-white text-3xl shadow-orange-500/30 shadow-lg">
            🛡️
          </div>
          <h1 className="text-2xl sm:text-3xl font-bold text-slate-900">Xác thực danh tính chủ trạm</h1>
          <p className="text-slate-500 mt-2 max-w-2xl mx-auto">Theo quy định pháp luật, chủ trạm sạc điện cần xác thực định danh trước khi bắt đầu kinh doanh.</p>
        </div>
        {children}
      </div>
    </div>
  );

  if (kycStatus === "Pending") {
    return (
      <KycContainer>
        <div className="text-center py-12">
          <div className="text-6xl mb-6">⏳</div>
          <h2 className="text-xl sm:text-2xl font-bold text-amber-600 mb-2">Hồ sơ đang chờ duyệt</h2>
          <p className="text-slate-600 max-w-xl mx-auto leading-relaxed">
            Hồ sơ của bạn đã được tiếp nhận và đang trong quá trình xét duyệt bởi Ban Quản Trị. Quá trình này thường mất từ 1-2 ngày làm việc.
            Vui lòng kiên nhẫn.
          </p>
          <button onClick={fetchMe} className="mt-8 px-6 py-3 bg-slate-100 text-slate-700 font-semibold rounded-xl hover:bg-slate-200 transition">
            🔄 Cập nhật trạng thái
          </button>
        </div>
      </KycContainer>
    );
  }

  // Unverified or Rejected
  return (
    <KycContainer>
      {kycStatus === "Rejected" && (
        <div className="mb-8 p-4 bg-red-50 border border-red-200 rounded-xl flex gap-3 text-red-700 items-start max-w-3xl mx-auto">
          <span className="text-xl">⚠️</span>
          <div>
            <h3 className="font-bold mb-1">Hồ sơ bị từ chối</h3>
            <p className="text-sm">{kycRejectReason || "Vui lòng cập nhật lại thông tin chính xác hơn."}</p>
          </div>
        </div>
      )}

      <form onSubmit={handleSubmit} className="grid grid-cols-1 lg:grid-cols-12 gap-8 lg:gap-12">
        {/* Left Column: Info Inputs */}
        <div className="lg:col-span-7 space-y-6">
          <h3 className="text-lg font-bold text-slate-800 border-b border-slate-100 pb-3">📝 Thông tin cơ bản</h3>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-1.5">Số Căn cước công dân (CCCD/CMND) <span className="text-red-500">*</span></label>
              <input type="text" name="IdCardNumber" required className="w-full px-4 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:ring-2 focus:ring-orange-500 focus:border-orange-500 outline-none transition" placeholder="Ví dụ: 079200001234" />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-1.5">Ngày cấp Căn cước công dân <span className="text-red-500">*</span></label>
              <input type="date" name="IdCardDate" required className="w-full px-4 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:ring-2 focus:ring-orange-500 focus:border-orange-500 outline-none transition text-slate-700" />
            </div>
          </div>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-1.5">Mã số thuế cá nhân / doanh nghiệp <span className="text-red-500">*</span></label>
            <input type="text" name="TaxCode" required className="w-full px-4 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:ring-2 focus:ring-orange-500 focus:border-orange-500 outline-none transition" placeholder="Mã số thuế" />
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-1.5">Hộ kinh doanh / Doanh nghiệp <span className="text-red-500">*</span></label>
              <input type="text" name="BusinessName" required className="w-full px-4 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:ring-2 focus:ring-orange-500 focus:border-orange-500 outline-none transition" placeholder="Tên đơn vị đăng ký kinh doanh" />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-1.5">Mã số Đăng ký kinh doanh (ĐKKD) <span className="text-red-500">*</span></label>
              <input type="text" name="BusinessLicenseNumber" required className="w-full px-4 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:ring-2 focus:ring-orange-500 focus:border-orange-500 outline-none transition" placeholder="Trùng với Mã số thuế nếu là cá nhân" />
            </div>
          </div>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-1.5">Địa chỉ đăng ký kinh doanh <span className="text-red-500">*</span></label>
            <textarea name="Address" required rows="3" className="w-full px-4 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:ring-2 focus:ring-orange-500 focus:border-orange-500 outline-none transition resize-none"></textarea>
          </div>
        </div>

        {/* Right Column: Files & Submit */}
        <div className="lg:col-span-5 space-y-6">
          <div className="bg-orange-50 border border-orange-100 rounded-2xl p-5">
            <h3 className="text-lg font-bold text-orange-800 border-b border-orange-200/60 pb-3 mb-4">🖼️ Tải lên hình ảnh xác thực</h3>
            <div className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-orange-700 mb-1.5">Mặt trước Căn cước công dân <span className="text-red-500">*</span></label>
                <input type="file" name="FrontIdCardImage" accept="image/*" required className="block w-full text-sm text-slate-600 bg-white border border-slate-200 rounded-xl file:mr-3 file:py-2.5 file:px-4 file:border-0 file:text-sm file:font-semibold file:bg-orange-100 file:text-orange-700 hover:file:bg-orange-200 transition cursor-pointer" />
              </div>
              <div>
                <label className="block text-xs font-semibold text-orange-700 mb-1.5">Mặt sau Căn cước công dân <span className="text-red-500">*</span></label>
                <input type="file" name="BackIdCardImage" accept="image/*" required className="block w-full text-sm text-slate-600 bg-white border border-slate-200 rounded-xl file:mr-3 file:py-2.5 file:px-4 file:border-0 file:text-sm file:font-semibold file:bg-orange-100 file:text-orange-700 hover:file:bg-orange-200 transition cursor-pointer" />
              </div>
              <div>
                <label className="block text-xs font-semibold text-orange-700 mb-1.5">Ảnh Giấy phép Đăng ký kinh doanh <span className="text-red-500">*</span></label>
                <input type="file" name="BusinessLicenseImage" accept="image/*" required className="block w-full text-sm text-slate-600 bg-white border border-slate-200 rounded-xl file:mr-3 file:py-2.5 file:px-4 file:border-0 file:text-sm file:font-semibold file:bg-orange-100 file:text-orange-700 hover:file:bg-orange-200 transition cursor-pointer" />
              </div>
            </div>
          </div>

          <div className="pt-2">
            <label className="flex items-start gap-2.5 mb-4 cursor-pointer">
              <input
                type="checkbox"
                checked={agreed}
                onChange={e => setAgreed(e.target.checked)}
                className="mt-0.5 flex-shrink-0 w-4.5 h-4.5 accent-orange-500 cursor-pointer"
              />
              <span className="text-sm text-slate-600 font-medium">Bằng việc gửi thông tin, tôi cam kết các giấy tờ đều là ảnh chụp bản gốc hợp lệ.</span>
            </label>
            <button type="submit" disabled={submitting || !agreed} className="w-full flex items-center justify-center gap-2 py-4 px-6 rounded-xl bg-gradient-to-r from-orange-500 to-orange-600 hover:from-orange-600 hover:to-orange-700 text-white font-bold text-lg transition shadow-xl shadow-orange-500/20 disabled:opacity-70 disabled:cursor-not-allowed disabled:shadow-none">
              {submitting ? (
                <>
                  <div className="w-6 h-6 border-2 border-white/30 border-t-white rounded-full animate-spin"></div>
                  Đang tải hồ sơ lên...
                </>
              ) : "Gửi thông tin phê duyệt"}
            </button>
          </div>
        </div>
      </form>
    </KycContainer>
  );
}
