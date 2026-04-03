import { Button } from "@/components/ui/button";
import { useAuthStore } from "@/stores/authStore";
import { useNavigate } from "react-router-dom";
import { useState } from "react";
import { adminAccountApi } from "@/services/api";
import { showToast } from "@/components/Toast";

const DEFAULT_AVATAR =
  "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'%3E%3Ccircle cx='50' cy='50' r='50' fill='%23f97316'/%3E%3Ccircle cx='50' cy='38' r='16' fill='%23fff'/%3E%3Cellipse cx='50' cy='75' rx='28' ry='20' fill='%23fff'/%3E%3C/svg%3E";

const maskPhone = (phone) =>
  phone ? `**** **** ${phone.slice(-2)}` : "";

export default function AdminProfile() {
  const navigate = useNavigate();
  const { phoneNumber: storedPhoneNumber } = useAuthStore();
  const phoneNumber =
    storedPhoneNumber || localStorage.getItem("phoneNumber") || "";
  // Admin chưa có server-side avatar API — chỉ lưu localStorage
  const [avatarSrc] = useState(
    () => getStoredAvatarDataUrl(phoneNumber) || DEFAULT_AVATAR
  );

  const [secModal, setSecModal] = useState(null); // 'setup' | 'reset-request' | 'reset-confirm'
  const [secForm, setSecForm] = useState({ currentPass: "", newSecPass: "", otp: "" });
  const [secLoading, setSecLoading] = useState(false);

  async function handleSetupSecPass(e) {
    e.preventDefault();
    if (!secForm.currentPass || !secForm.newSecPass) return showToast.error("Vui lòng nhập đủ trường");
    setSecLoading(true);
    try {
      await adminAccountApi.setupSecondaryPassword(secForm.currentPass, secForm.newSecPass);
      showToast.success("Thiết lập mật khẩu cấp 2 thành công!");
      setSecModal(null);
    } catch (err) {
      showToast.error(err.message || "Lỗi thiết lập");
    } finally {
      setSecLoading(false);
    }
  }

  async function handleResetSecReq() {
    setSecLoading(true);
    try {
      await adminAccountApi.resetSecondaryPasswordRequest();
      showToast.success("OTP đã được gửi! Vui lòng kiểm tra.");
      setSecModal("reset-confirm");
    } catch (err) {
      showToast.error(err.message || "Lỗi yêu cầu khôi phục");
    } finally {
      setSecLoading(false);
    }
  }

  async function handleResetSecConfirm(e) {
    e.preventDefault();
    if (!secForm.otp || !secForm.newSecPass) return showToast.error("Vui lòng nhập đủ trường");
    setSecLoading(true);
    try {
      await adminAccountApi.resetSecondaryPasswordConfirm(secForm.otp, secForm.newSecPass);
      showToast.success("Khôi phục mật khẩu cấp 2 thành công!");
      setSecModal(null);
    } catch (err) {
      showToast.error(err.message || "Lỗi khôi phục");
    } finally {
      setSecLoading(false);
    }
  }

  return (
    <div className="min-h-[calc(100vh-64px)] px-4 py-10 pt-24" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #f1f5f9 50%, #e8ecf1 100%)" }}>
      <div className="max-w-4xl mx-auto">
        {/* Profile Header Card */}
        <div
          className="relative rounded-2xl overflow-hidden shadow-xl mb-8"
          style={{ background: "linear-gradient(135deg, #ff7e29 0%, #f97316 50%, #ea580c 100%)" }}
        >
          <div className="absolute inset-0 opacity-10">
            <div className="absolute top-0 right-0 w-64 h-64 bg-white rounded-full -translate-y-32 translate-x-32" />
            <div className="absolute bottom-0 left-0 w-48 h-48 bg-white rounded-full translate-y-24 -translate-x-24" />
          </div>

          <div className="relative px-8 py-10 flex flex-col md:flex-row items-center gap-6">
            <div className="relative">
              <img
                src={avatarSrc}
                alt="Avatar"
                className="w-28 h-28 rounded-full object-cover border-4 border-white/40 shadow-2xl"
                style={{ boxShadow: "0 8px 32px rgba(0,0,0,0.2)" }}
              />
              <div className="absolute -bottom-1 -right-1 w-8 h-8 bg-green-400 rounded-full border-3 border-white flex items-center justify-center">
                <svg className="w-4 h-4 text-white" fill="currentColor" viewBox="0 0 20 20">
                  <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                </svg>
              </div>
            </div>
            <div className="text-center md:text-left">
              <h1 className="text-2xl font-bold text-white mb-1">
                Hồ sơ quản trị viên
              </h1>
              <p className="text-white/80 text-sm">
                {maskPhone(phoneNumber) || "Chưa cập nhật số điện thoại"}
              </p>
              <span className="inline-block mt-2 px-3 py-1 text-xs font-semibold rounded-full bg-white/20 text-white backdrop-blur-sm">
                🛡️ Quản trị viên
              </span>
            </div>
          </div>
        </div>

        {/* Info Card */}
        <div
          className="rounded-2xl shadow-lg overflow-hidden"
          style={{
            background: "rgba(255,255,255,0.9)",
            backdropFilter: "blur(20px)",
          }}
        >
          <div className="px-8 py-6 border-b border-gray-100">
            <h2 className="text-lg font-bold text-gray-800 flex items-center gap-2">
              <svg className="w-5 h-5 text-orange-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
              </svg>
              Thông tin cá nhân
            </h2>
          </div>

          <div className="px-8 py-6">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
              <InfoCard icon="🏷️" label="Vai trò" value="Quản trị viên" />
              <InfoCard icon="📱" label="Số điện thoại" value={maskPhone(phoneNumber) || "—"} />
            </div>
          </div>

          {/* Actions */}
          <div className="px-8 py-5 bg-gray-50/50 border-t border-gray-100 flex flex-col sm:flex-row gap-3">
            <Button
              className="flex-1 h-11 bg-orange-500 hover:bg-orange-600 rounded-xl font-medium shadow-md shadow-orange-200 transition-all hover:shadow-lg hover:shadow-orange-300"
              onClick={() => navigate("/admin/edit-admin-profile")}
            >
              <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
              </svg>
              Chỉnh sửa hồ sơ
            </Button>
            <Button
              variant="outline"
              className="flex-1 h-11 border-red-300 text-red-500 hover:bg-red-50 rounded-xl font-medium transition-all"
              onClick={() => navigate("/admin/change-password")}
            >
              <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
              </svg>
              Thay đổi mật khẩu
            </Button>
            <Button
              variant="outline"
              className="flex-1 h-11 border-purple-300 text-purple-600 hover:bg-purple-50 rounded-xl font-medium transition-all"
              onClick={() => { setSecModal("setup"); setSecForm({ currentPass: "", newSecPass: "", otp: "" }); }}
            >
              <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
              </svg>
              Mật khẩu cấp 2
            </Button>
          </div>
        </div>
      </div>

      {/* Secondary Password Modal */}
      {secModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl w-full max-w-sm p-6 shadow-2xl">
            <h2 className="text-xl font-bold text-gray-800 mb-4">
              {secModal === "setup" ? "Thiết lập Mật khẩu Cấp 2" : "Khôi phục Mật khẩu Cấp 2"}
            </h2>
            {secModal === "setup" ? (
              <form onSubmit={handleSetupSecPass} className="flex flex-col gap-4">
                <div>
                  <label className="text-sm font-medium text-gray-700">Mật khẩu đăng nhập</label>
                  <input type="password" value={secForm.currentPass} onChange={e => setSecForm({ ...secForm, currentPass: e.target.value })} className="mt-1 w-full p-2 border rounded-lg outline-none focus:border-orange-500" required />
                </div>
                <div>
                  <label className="text-sm font-medium text-gray-700">Mật khẩu cấp 2 (Mới)</label>
                  <input type="password" value={secForm.newSecPass} onChange={e => setSecForm({ ...secForm, newSecPass: e.target.value })} className="mt-1 w-full p-2 border rounded-lg outline-none focus:border-orange-500" required />
                </div>
                <div className="flex gap-2 mt-2">
                  <Button type="submit" disabled={secLoading} className="flex-1 bg-orange-500 hover:bg-orange-600">Lưu</Button>
                  <Button type="button" onClick={() => setSecModal(null)} className="flex-1 bg-gray-100 text-gray-700 hover:bg-gray-200">Hủy</Button>
                </div>
                <button type="button" onClick={handleResetSecReq} className="text-sm text-blue-500 hover:underline mt-2">
                  Quên mật khẩu cấp 2?
                </button>
              </form>
            ) : (
              <form onSubmit={handleResetSecConfirm} className="flex flex-col gap-4">
                <p className="text-sm text-gray-600">Chúng tôi đã gửi mã OTP. Vui lòng kiểm tra tin nhắn.</p>
                <div>
                  <label className="text-sm font-medium text-gray-700">Mã OTP</label>
                  <input type="text" value={secForm.otp} onChange={e => setSecForm({ ...secForm, otp: e.target.value })} className="mt-1 w-full p-2 border rounded-lg outline-none focus:border-orange-500" required />
                </div>
                <div>
                  <label className="text-sm font-medium text-gray-700">Mật khẩu cấp 2 (Mới)</label>
                  <input type="password" value={secForm.newSecPass} onChange={e => setSecForm({ ...secForm, newSecPass: e.target.value })} className="mt-1 w-full p-2 border rounded-lg outline-none focus:border-orange-500" required />
                </div>
                <div className="flex gap-2 mt-2">
                  <Button type="submit" disabled={secLoading} className="flex-1 bg-orange-500 hover:bg-orange-600">Xác nhận</Button>
                  <Button type="button" onClick={() => setSecModal(null)} className="flex-1 bg-gray-100 text-gray-700 hover:bg-gray-200">Hủy</Button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

function InfoCard({ icon, label, value }) {
  return (
    <div className="flex items-start gap-3 p-4 rounded-xl bg-gray-50/80 hover:bg-gray-100/80 transition-colors">
      <span className="text-xl leading-none mt-0.5">{icon}</span>
      <div className="min-w-0 flex-1">
        <p className="text-xs text-gray-400 uppercase tracking-wide font-medium mb-0.5">{label}</p>
        <p className="font-medium text-gray-800 truncate">{value}</p>
      </div>
    </div>
  );
}

function getStoredAvatarDataUrl(phoneNumber) {
  if (!phoneNumber) return "";
  try {
    const map = JSON.parse(localStorage.getItem("userInfoByPhone") || "{}");
    const normalized = normalizePhoneForKey(phoneNumber);
    return map?.[normalized]?.avatarDataUrl || map?.[phoneNumber]?.avatarDataUrl || "";
  } catch {
    return "";
  }
}

function normalizePhoneForKey(rawPhone) {
  const phone = String(rawPhone || "").trim().replaceAll(" ", "");
  if (!phone) return "";
  if (phone.startsWith("+84")) return `0${phone.slice(3)}`;
  return phone;
}
