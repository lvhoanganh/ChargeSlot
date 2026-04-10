import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { apiFetch } from "@/services/api";
import { useAuthStore } from "@/stores/authStore";

export default function ChangePassword() {
  const navigate = useNavigate();
  const { logout } = useAuthStore();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showCurrent, setShowCurrent] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);
  const [countdown, setCountdown] = useState(3);

  // Auto-redirect to login after success
  useEffect(() => {
    if (!success) return;
    if (countdown <= 0) {
      logout();
      navigate("/login", { replace: true });
      return;
    }
    const timer = setTimeout(() => setCountdown(c => c - 1), 1000);
    return () => clearTimeout(timer);
  }, [success, countdown]);

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");

    if (!currentPassword.trim()) {
      setError("Vui lòng nhập mật khẩu hiện tại.");
      return;
    }
    if (newPassword.length < 6) {
      setError("Mật khẩu mới phải có ít nhất 6 ký tự.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("Mật khẩu xác nhận không khớp.");
      return;
    }
    if (currentPassword === newPassword) {
      setError("Mật khẩu mới phải khác mật khẩu hiện tại.");
      return;
    }

    setLoading(true);
    try {
      await apiFetch("/auth/change-password", {
        method: "POST",
        body: JSON.stringify({ currentPassword, newPassword }),
      });
      setSuccess(true);
    } catch (err) {
      const msg = err?.message || "Đổi mật khẩu thất bại.";
      if (msg.includes("Incorrect password") || msg.includes("incorrect")) {
        setError("Mật khẩu hiện tại không đúng.");
      } else {
        setError(msg);
      }
    } finally {
      setLoading(false);
    }
  }

  if (success) {
    return (
      <div className="min-h-[calc(100vh-64px)] flex items-center justify-center px-4 pt-20" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #f1f5f9 50%, #e8ecf1 100%)" }}>
        <div className="max-w-md w-full bg-white rounded-2xl shadow-xl p-8 text-center">
          <div className="w-16 h-16 mx-auto bg-green-100 rounded-full flex items-center justify-center mb-4">
            <svg className="w-8 h-8 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <h2 className="text-xl font-bold text-gray-800 mb-2">Đổi mật khẩu thành công!</h2>
          <p className="text-gray-500 text-sm mb-4">Mật khẩu của bạn đã được cập nhật.</p>
          <p className="text-sm text-orange-600 font-medium mb-6">
            Bạn sẽ được đăng xuất và chuyển về trang đăng nhập sau <span className="font-bold text-lg">{countdown}</span> giây...
          </p>
          <button
            onClick={() => { logout(); navigate("/login", { replace: true }); }}
            className="px-6 py-3 bg-orange-500 hover:bg-orange-600 text-white font-semibold rounded-xl transition-colors cursor-pointer"
          >
            Đăng nhập lại ngay
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-[calc(100vh-64px)] flex items-center justify-center px-4 pt-20" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #f1f5f9 50%, #e8ecf1 100%)" }}>
      <div className="max-w-md w-full">
        {/* Header */}
        <div className="text-center mb-6">
          <div className="w-16 h-16 mx-auto bg-orange-100 rounded-full flex items-center justify-center mb-3">
            <svg className="w-8 h-8 text-orange-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
            </svg>
          </div>
          <h1 className="text-2xl font-bold text-gray-800">Đổi mật khẩu</h1>
          <p className="text-gray-500 text-sm mt-1">Nhập mật khẩu hiện tại và mật khẩu mới</p>
        </div>

        {/* Form Card */}
        <div className="bg-white rounded-2xl shadow-xl overflow-hidden" style={{ backdropFilter: "blur(20px)" }}>
          <form onSubmit={handleSubmit}>
            <div className="p-6 space-y-5">
              {error && (
                <div className="p-3 bg-red-50 border border-red-200 rounded-xl flex items-center gap-2">
                  <svg className="w-5 h-5 text-red-500 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                  <p className="text-sm text-red-700">{error}</p>
                </div>
              )}

              {/* Current Password */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">🔒 Mật khẩu hiện tại</label>
                <div className="relative">
                  <input
                    type={showCurrent ? "text" : "password"}
                    value={currentPassword}
                    onChange={e => setCurrentPassword(e.target.value)}
                    placeholder="Nhập mật khẩu hiện tại"
                    className="w-full h-11 px-4 pr-12 border border-gray-200 rounded-xl outline-none focus:border-orange-400 focus:ring-2 focus:ring-orange-100 transition-all text-sm"
                    autoComplete="current-password"
                  />
                  <button
                    type="button"
                    onClick={() => setShowCurrent(!showCurrent)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 cursor-pointer"
                  >
                    {showCurrent ? (
                      <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.878 9.878L6.59 6.59m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" /></svg>
                    ) : (
                      <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" /><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" /></svg>
                    )}
                  </button>
                </div>
              </div>

              {/* New Password */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">🔑 Mật khẩu mới</label>
                <div className="relative">
                  <input
                    type={showNew ? "text" : "password"}
                    value={newPassword}
                    onChange={e => setNewPassword(e.target.value)}
                    placeholder="Ít nhất 6 ký tự"
                    className="w-full h-11 px-4 pr-12 border border-gray-200 rounded-xl outline-none focus:border-orange-400 focus:ring-2 focus:ring-orange-100 transition-all text-sm"
                    autoComplete="new-password"
                  />
                  <button
                    type="button"
                    onClick={() => setShowNew(!showNew)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 cursor-pointer"
                  >
                    {showNew ? (
                      <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.878 9.878L6.59 6.59m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" /></svg>
                    ) : (
                      <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" /><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" /></svg>
                    )}
                  </button>
                </div>
                {newPassword.length > 0 && newPassword.length < 6 && (
                  <p className="text-xs text-amber-500 mt-1">⚠️ Cần ít nhất 6 ký tự</p>
                )}
              </div>

              {/* Confirm Password */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">🔑 Xác nhận mật khẩu mới</label>
                <div className="relative">
                  <input
                    type={showConfirm ? "text" : "password"}
                    value={confirmPassword}
                    onChange={e => setConfirmPassword(e.target.value)}
                    placeholder="Nhập lại mật khẩu mới"
                    className="w-full h-11 px-4 pr-12 border border-gray-200 rounded-xl outline-none focus:border-orange-400 focus:ring-2 focus:ring-orange-100 transition-all text-sm"
                    autoComplete="new-password"
                  />
                  <button
                    type="button"
                    onClick={() => setShowConfirm(!showConfirm)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 cursor-pointer"
                  >
                    {showConfirm ? (
                      <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.878 9.878L6.59 6.59m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" /></svg>
                    ) : (
                      <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" /><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" /></svg>
                    )}
                  </button>
                </div>
                {confirmPassword.length > 0 && confirmPassword !== newPassword && (
                  <p className="text-xs text-red-500 mt-1">❌ Mật khẩu xác nhận không khớp</p>
                )}
                {confirmPassword.length > 0 && confirmPassword === newPassword && newPassword.length >= 6 && (
                  <p className="text-xs text-green-500 mt-1">✅ Mật khẩu khớp</p>
                )}
              </div>
            </div>

            {/* Actions */}
            <div className="px-6 py-4 bg-gray-50/50 border-t border-gray-100 flex gap-3">
              <button
                type="button"
                onClick={() => navigate(-1)}
                className="flex-1 h-11 border border-gray-200 rounded-xl font-medium text-gray-700 hover:bg-gray-100 transition-colors cursor-pointer"
              >
                Hủy
              </button>
              <button
                type="submit"
                disabled={loading || !currentPassword || newPassword.length < 6 || newPassword !== confirmPassword}
                className="flex-1 h-11 rounded-xl font-semibold text-white transition-all cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed shadow-md"
                style={{ background: "linear-gradient(135deg, #f97316, #ea580c)" }}
              >
                {loading ? (
                  <span className="flex items-center justify-center gap-2">
                    <span className="w-4 h-4 border-2 border-white/40 border-t-white rounded-full animate-spin" />
                    Đang xử lý...
                  </span>
                ) : (
                  "Đổi mật khẩu"
                )}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}
