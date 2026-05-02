import React, { useEffect, useState } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { authApi, ownerKycApi, ownerContractApi } from "@/services/api";
import { showToast } from "@/components/Toast";

const KycContainer = ({ children }) => (
  <div className="min-h-screen bg-slate-100 flex justify-center pt-24 pb-12 px-4 sm:px-6">
    <div className="max-w-5xl w-full bg-white rounded-3xl shadow-sm ring-1 ring-slate-200 p-6 sm:p-10">
      <div className="text-center mb-8">
        <h1 className="text-2xl sm:text-3xl font-bold text-slate-900">Xác thực danh tính chủ trạm</h1>
        <p className="text-slate-500 mt-2 max-w-2xl mx-auto">Theo quy định pháp luật, chủ trạm sạc điện cần xác thực định danh trước khi bắt đầu kinh doanh.</p>
      </div>
      {children}
    </div>
  </div>
);

export default function OwnerKycGuard({ children }) {
  const navigate = useNavigate();
  const location = useLocation();
  const [loading, setLoading] = useState(true);
  const [kycStatus, setKycStatus] = useState(null);
  const [kycRejectReason, setKycRejectReason] = useState(null);
  const [contractStatus, setContractStatus] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [agreed, setAgreed] = useState(false);

  useEffect(() => {
    fetchMe();
  }, []);

  async function fetchMe() {
    setLoading(true);
    try {
      const me = await authApi.getMe();
      const status = me.kycStatus || "Unverified";
      setKycStatus(status);
      setKycRejectReason(me.kycRejectReason);

      // Nếu KYC đã Approved hoặc PendingUpdate → kiểm tra trạng thái hợp đồng
      // BE tự động tạo contract (Pending) ngay khi Admin duyệt KYC
      if (status === "Approved" || status === "PendingUpdate") {
        try {
          const contract = await ownerContractApi.get();
          setContractStatus(contract?.status || null);
        } catch {
          // Chưa có hợp đồng hoặc lỗi → bỏ qua, không block
          setContractStatus(null);
        }
      }
    } catch (err) {
      showToast.error("Lỗi xác minh danh tính: " + (err.message || "Không rõ"));
    } finally {
      setLoading(false);
    }
  }

  const handleSubmit = async (e) => {
    e.preventDefault();
    const formData = new FormData(e.target);

    // Helper: kiểm tra giá trị masked từ BE (có chứa dấu *)
    const isMasked = (val) => typeof val === "string" && val.includes("*");

    // Validation
    const idCard = formData.get("IdCardNumber")?.trim();
    // Nếu chuỗi chứa "*" → là masked value, bỏ qua validate — BE tự giữ nguyên trong DB
    if (!isMasked(idCard) && !/^\d{12}$/.test(idCard)) {
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
    // Tương tự — nếu masked thì bỏ qua validate
    if (!isMasked(businessLicense) && !/^\d{10}$/.test(businessLicense)) {
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

  // Approved hoặc PendingUpdate → kiểm tra hợp đồng trước khi cho phép dùng
  if (kycStatus === "Approved" || kycStatus === "PendingUpdate") {
    // Nếu hợp đồng đang Pending (chưa ký) và KHÔNG đang ở trang ký → block, yêu cầu ký trước
    if (contractStatus === "Pending" && !location.pathname.startsWith("/owner/contract")) {
      return (
        <div className="min-h-screen bg-gradient-to-br from-orange-50 via-amber-50 to-yellow-50 flex items-center justify-center pt-20 pb-12 px-4">
          <div className="max-w-lg w-full bg-white rounded-3xl shadow-xl ring-1 ring-orange-100 p-8 text-center">
            <div className="w-20 h-20 bg-orange-100 rounded-full flex items-center justify-center mx-auto mb-6">
              <svg width="40" height="40" fill="none" stroke="#ea580c" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.8} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
            </div>
            <h2 className="text-2xl font-bold text-slate-900 mb-2">Ký hợp đồng hợp tác</h2>
            <p className="text-slate-500 leading-relaxed mb-2">
              Hồ sơ KYC của bạn đã được <strong className="text-green-600">phê duyệt thành công</strong>!
            </p>
            <p className="text-slate-500 leading-relaxed mb-6">
              Để chính thức bắt đầu kinh doanh trên ChargeSlot, bạn cần đọc và ký hợp đồng hợp tác điện tử trước khi sử dụng các tính năng.
            </p>
            <div className="bg-orange-50 border border-orange-200 rounded-2xl p-4 mb-6 text-sm text-orange-800 text-left">
              <div className="flex items-start gap-2">
                <span className="text-lg flex-shrink-0">ℹ️</span>
                <div>
                  <strong className="block mb-0.5">Lưu ý:</strong>
                  Hợp đồng hợp tác có hiệu lực ngay sau khi ký và trạm sạc của bạn sẽ được kích hoạt đầy đủ.
                </div>
              </div>
            </div>
            <button
              onClick={() => navigate("/owner/contract")}
              className="w-full py-3.5 rounded-xl bg-gradient-to-r from-orange-500 to-orange-600 hover:from-orange-600 hover:to-orange-700 text-white font-bold text-base transition shadow-lg shadow-orange-500/20 flex items-center justify-center gap-2"
            >
              <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
              </svg>
              Đọc và ký hợp đồng ngay
            </button>
          </div>
        </div>
      );
    }

    if ((contractStatus === "Terminated" || contractStatus === "Expired") && !location.pathname.startsWith("/owner/contract") && !location.pathname.startsWith("/owner/wallet") && !location.pathname.startsWith("/owner/kyc")) {
      return (
        <div className="min-h-screen bg-gradient-to-br from-red-50 via-rose-50 to-red-50 flex items-center justify-center pt-20 pb-12 px-4">
          <div className="max-w-lg w-full bg-white rounded-3xl shadow-xl ring-1 ring-red-100 p-8 text-center">
            <div className="w-20 h-20 bg-red-100 rounded-full flex items-center justify-center mx-auto mb-6">
              <svg width="40" height="40" fill="none" stroke="#dc2626" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.8} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
            <h2 className="text-2xl font-bold text-slate-900 mb-2">
              {contractStatus === "Expired" ? "Hợp đồng đã hết hạn" : "Hợp đồng đã chấm dứt"}
            </h2>
            <p className="text-slate-500 leading-relaxed mb-6">
              Hợp đồng hợp tác của bạn đã {contractStatus === "Expired" ? "hết hạn" : "bị chấm dứt"}. Toàn bộ trạm sạc đã ngừng hoạt động và bạn không thể quản lý trạm nhưng <strong className="text-slate-700">vẫn có thể rút số dư ví điện tử</strong> của mình.
            </p>
            <div className="flex flex-col gap-3">
              <button
                onClick={() => navigate("/owner/wallet")}
                className="w-full py-3.5 rounded-xl border-2 border-slate-200 text-slate-800 hover:bg-slate-50 font-bold text-base transition flex items-center justify-center gap-2"
              >
                <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z" />
                </svg>
                Vào ví rút tiền
              </button>
              <button
                onClick={() => navigate("/owner/contract")}
                className="w-full py-3.5 rounded-xl bg-red-600 text-white hover:bg-red-700 font-bold text-base transition flex items-center justify-center gap-2"
              >
                <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                </svg>
                Xem chi tiết hợp đồng
              </button>
            </div>
          </div>
        </div>
      );
    }
    // Hợp đồng đã ký hoặc không có hợp đồng → cho phép dùng bình thường
    return children;
  }

  if (kycStatus === "Pending") {
    return (
      <KycContainer>
        <div className="text-center py-12">
          <div className="text-6xl mb-6"></div>
          <h2 className="text-xl sm:text-2xl font-bold text-amber-600 mb-2">Hồ sơ đang chờ duyệt</h2>
          <p className="text-slate-600 max-w-xl mx-auto leading-relaxed">
            Hồ sơ của bạn đã được tiếp nhận và đang trong quá trình xét duyệt bởi Ban Quản Trị. Quá trình này thường mất từ 1-2 ngày làm việc.
            Vui lòng kiên nhẫn.
          </p>
          <button onClick={fetchMe} className="mt-8 px-6 py-3 bg-slate-100 text-slate-700 font-semibold rounded-xl hover:bg-slate-200 transition">
            Cập nhật trạng thái
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
          <span className="text-xl">️</span>
          <div>
            <h3 className="font-bold mb-1">Hồ sơ bị từ chối</h3>
            <p className="text-sm">{kycRejectReason || "Vui lòng cập nhật lại thông tin chính xác hơn."}</p>
          </div>
        </div>
      )}

      <form onSubmit={handleSubmit} className="grid grid-cols-1 lg:grid-cols-12 gap-8 lg:gap-12">
        {/* Left Column: Info Inputs */}
        <div className="lg:col-span-7 space-y-6">
          <h3 className="text-lg font-bold text-slate-800 border-b border-slate-100 pb-3"> Thông tin cơ bản</h3>

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
          {/* <div className="bg-orange-50 border border-orange-100 rounded-2xl p-5">
            <h3 className="text-lg font-bold text-orange-800 border-b border-orange-200/60 pb-3 mb-4">️ Tải lên hình ảnh xác thực</h3>
            <div className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-orange-700 mb-1.5">Mặt trước Căn cước công dân <span className="text-red-500">*</span></label>
                <input type="file" name="FrontIdCardImage" accept="image/*" required className="block w-full text-sm text-slate-600 bg-white border border-slate-200 rounded-xl file:mr-3 file:py-2.5 file:px-4 file:border-0 file:text-sm file:font-semibold file:bg-orange-100 file:text-orange-700 hover:file:bg-orange-200 transition cursor-pointer" />
              </div>
              <div>
                <label className="block text-xs font-semibold text-orange-700 mb-1.5">Mặt sau Căn cước công dân <span className="text-red-500">*</span></label>
                <input type="file" name="BackIdCardImage" accept="image/*" required className="block w-full text-sm text-slate-600 bg-white border border-slate-200 rounded-xl file:mr-3 file:py-2.5 file:px-4 file:border-0 file:text-sm file:font-semibold file:bg-orange-100 file:text-orange-700 hover:file:bg-orange-200 transition cursor-pointer" />
              </div> */}
          <div className="space-y-4">
            <div>
              <label className="block text-xs font-semibold text-orange-700 mb-1.5">Ảnh Giấy phép Đăng ký kinh doanh <span className="text-red-500">*</span></label>
              <input type="file" name="BusinessLicenseImage" accept="image/*" required className="block w-full text-sm text-slate-600 bg-white border border-slate-200 rounded-xl file:mr-3 file:py-2.5 file:px-4 file:border-0 file:text-sm file:font-semibold file:bg-orange-100 file:text-orange-700 hover:file:bg-orange-200 transition cursor-pointer" />
            </div>
          </div>
          {/* </div>
          </div> */}

          <div className="pt-2">
            <div className="flex items-start gap-2.5 mb-4">
              <input
                type="checkbox"
                id="kyc-agree-checkbox"
                checked={agreed}
                onChange={e => setAgreed(e.target.checked)}
                onClick={e => e.stopPropagation()}
                className="mt-0.5 flex-shrink-0 accent-orange-500 cursor-pointer"
                style={{ width: "18px", height: "18px" }}
              />
              <label
                htmlFor="kyc-agree-checkbox"
                className="text-sm text-slate-600 font-medium cursor-pointer select-none"
              >
                Bằng việc gửi thông tin, tôi cam kết các giấy tờ đều là ảnh chụp bản gốc hợp lệ.
              </label>
            </div>
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
