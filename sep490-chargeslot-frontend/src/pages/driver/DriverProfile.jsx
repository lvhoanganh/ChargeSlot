import { Button } from "@/components/ui/button";
import { instance } from "@/lib/httpRequest";
import { authApi } from "@/services/api";
import { useAuthStore } from "@/stores/authStore";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { showToast } from "@/components/Toast";
import { ShieldCheck, Phone, Mail, CarFront, Hash, FileText } from "lucide-react";

const DEFAULT_AVATAR =
  "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'%3E%3Ccircle cx='50' cy='50' r='50' fill='%23f97316'/%3E%3Ccircle cx='50' cy='38' r='16' fill='%23fff'/%3E%3Cellipse cx='50' cy='75' rx='28' ry='20' fill='%23fff'/%3E%3C/svg%3E";

const maskPhone = (phone) =>
  phone ? `**** **** ${phone.slice(-2)}` : "";
const maskLicense = (license) =>
  license ? `**** **** ${license.slice(-4)}` : "";

export default function DriverProfile() {
  const navigate = useNavigate();
  const { phoneNumber: storedPhoneNumber } = useAuthStore();
  const phoneNumber =
    storedPhoneNumber || localStorage.getItem("phoneNumber") || "";

  const [profile, setProfile] = useState(null);
  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [avatarSrc, setAvatarSrc] = useState(
    () => getStoredAvatarDataUrl(phoneNumber) || DEFAULT_AVATAR
  );

  const [showOtpModal, setShowOtpModal] = useState(false);
  const [cooldown, setCooldown] = useState(0);

  useEffect(() => {
    let timer;
    if (showOtpModal && cooldown > 0) {
      timer = setInterval(() => setCooldown(c => c - 1), 1000);
    }
    return () => clearInterval(timer);
  }, [showOtpModal, cooldown]);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      setError("");
      try {
        const [data, meData] = await Promise.all([
          getDriverProfile(),
          authApi.getMe().catch(() => null),
        ]);
        if (!cancelled) {
          setProfile(data);
          if (meData?.email) setEmail(meData.email);
          if (meData?.pendingEmail) setProfile(prev => ({ ...prev, pendingEmail: meData.pendingEmail }));
          if (data?.avatarUrl) {
            const url = data.avatarUrl.startsWith("http")
              ? data.avatarUrl
              : `https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net${data.avatarUrl.startsWith("/") ? "" : "/"}${data.avatarUrl}`;
            setAvatarSrc(url);
          } else {
            const local = getStoredAvatarDataUrl(phoneNumber);
            if (local) setAvatarSrc(local);
          }
        }
      } catch (e) {
        if (!cancelled) setError(getApiErrorMessage(e, "Không thể tải thông tin hồ sơ"));
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, [phoneNumber]);

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
                Hồ sơ tài xế
              </h1>
              <p className="text-white/80 text-sm">
                {maskPhone(phoneNumber) || "Chưa cập nhật số điện thoại"}
              </p>
              <span className="inline-block mt-2 px-3 py-1 text-xs font-semibold rounded-full bg-white/20 text-white backdrop-blur-sm">
                 Tài xế
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
            {loading && (
              <div className="flex items-center justify-center py-8">
                <div className="w-8 h-8 border-4 border-orange-200 border-t-orange-500 rounded-full animate-spin" />
                <span className="ml-3 text-gray-500 text-sm">Đang tải hồ sơ...</span>
              </div>
            )}

            {!!error && (
              <div className="p-4 bg-red-50 border border-red-200 rounded-xl flex items-center gap-3">
                <svg className="w-5 h-5 text-red-500 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <p className="text-sm text-red-700">{error}</p>
              </div>
            )}

            {!loading && !error && profile == null && (
              <div className="p-4 bg-orange-50 border border-orange-200 rounded-xl flex items-center gap-3">
                <svg className="w-5 h-5 text-orange-500 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <p className="text-sm text-orange-700">
                  Hồ sơ của bạn chưa hoàn thiện. Vui lòng cập nhật thông tin.
                </p>
              </div>
            )}

            {!loading && !error && (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
                <InfoCard icon={<ShieldCheck className="text-gray-500" size={20} />} label="Vai trò" value="Tài xế" />
                <InfoCard icon={<Phone className="text-gray-500" size={20} />} label="Số điện thoại" value={maskPhone(phoneNumber) || "—"} />
                <InfoCard icon={<Mail className="text-gray-500" size={20} />} label="Email" value={email || "—"} />
                <InfoCard icon={<CarFront className="text-gray-500" size={20} />} label="Loại xe" value={profile?.vehicleType || "—"} />
                <InfoCard icon={<Hash className="text-gray-500" size={20} />} label="Biển số" value={profile?.licensePlate || "—"} />
                <InfoCard icon={<FileText className="text-gray-500" size={20} />} label="Số giấy phép" value={maskLicense(profile?.licenseNumber) || "—"} />
              </div>
            )}

            {!loading && !error && profile?.pendingEmail && (
              <div className="mt-6 p-4 bg-yellow-50 border border-yellow-200 rounded-xl flex items-center gap-3 shadow-sm">
                <svg className="w-5 h-5 text-yellow-500 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <div className="flex-[1]">
                  <p className="text-sm text-yellow-700 font-medium tracking-tight">Email đang chờ xác minh</p>
                  <p className="text-[13px] text-yellow-600 font-semibold">{profile.pendingEmail}</p>
                </div>
                <button
                  onClick={() => setShowOtpModal(true)}
                  className="px-3 py-1.5 bg-yellow-500 hover:bg-yellow-600 text-white text-xs font-bold rounded-lg shadow-sm transition-colors whitespace-nowrap"
                >
                  Gửi lại Link
                </button>
              </div>
            )}

            {!loading && !error && (profile?.strikeCount > 0 || profile?.disputeStrikes > 0) && (
              <div className="mt-6 p-4 bg-red-50 border border-red-200 rounded-xl flex items-center gap-3 shadow-sm relative overflow-hidden">
                <div className="absolute top-0 left-0 w-1 h-full bg-red-500"></div>
                <svg className="w-6 h-6 text-red-500 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                </svg>
                <div className="flex-[1]">
                  <p className="text-sm text-red-700 font-bold tracking-tight">Cảnh cáo vi phạm (Strike Warning)</p>
                  <p className="text-[13px] text-red-600 mt-0.5 leading-snug">
                    Tài khoản của bạn hiện có <strong>{profile.strikeCount || profile.disputeStrikes}</strong> vi phạm từ các khiếu nại.
                    Nếu đạt mốc 3 vi phạm, tài khoản có thể bị khóa. Vui lòng tuân thủ quy định!
                  </p>
                </div>
              </div>
            )}
          </div>

          {/* Actions */}
          <div className="px-8 py-5 bg-gray-50/50 border-t border-gray-100 flex flex-col sm:flex-row gap-3">
            <Button
              className="flex-1 h-11 bg-orange-500 hover:bg-orange-600 rounded-xl font-medium shadow-md shadow-orange-200 transition-all hover:shadow-lg hover:shadow-orange-300"
              onClick={() => navigate("/driver/update-driver-profile")}
            >
              <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
              </svg>
              Chỉnh sửa hồ sơ
            </Button>
            <Button
              variant="outline"
              className="flex-1 h-11 border-red-300 text-red-500 hover:bg-red-50 rounded-xl font-medium transition-all"
              onClick={() => navigate("/driver/change-password")}
            >
              <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
              </svg>
              Thay đổi mật khẩu
            </Button>
          </div>
        </div>
      </div>

      {/* OTP Modal */}
      {showOtpModal && (
        <div style={{ position: "fixed", inset: 0, zIndex: 9999, background: "rgba(15,23,42,0.6)", backdropFilter: "blur(4px)", display: "flex", alignItems: "center", justifyContent: "center", padding: 20 }}>
          <div style={{ background: "#fff", borderRadius: 24, width: "100%", maxWidth: 400, overflow: "hidden", boxShadow: "0 20px 40px rgba(0,0,0,0.2)", display: "flex", flexDirection: "column" }}>
            <div style={{ padding: "20px 24px", borderBottom: "1px solid #f1f5f9", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <h3 style={{ fontSize: 18, fontWeight: 700, color: "#1e293b", margin: 0, display: "flex", alignItems: "center", gap: 8 }}>
                 Xác thực email mới
              </h3>
            </div>
            <div style={{ padding: 24 }}>
              <p style={{ color: "#475569", fontSize: 14, marginBottom: 16, lineHeight: 1.5 }}>
                Một link xác thực đã được gửi đến:<br />
                <strong style={{ color: "#1e293b" }}>{profile?.pendingEmail}</strong>
              </p>
              <p style={{ color: "#64748b", fontSize: 13, marginBottom: 16 }}>
                Vui lòng kiểm tra hộp thư đến (và thư mục rác) để xác thực email. Link sẽ hết hạn sau 24 giờ.
              </p>
              <div style={{ textAlign: "center", marginTop: 16, fontSize: 13, color: "#64748b" }}>
                {cooldown > 0 ? (
                  <span>Gửi lại sau: {String(Math.floor(cooldown / 60)).padStart(2, '0')}:{String(cooldown % 60).padStart(2, '0')} ️</span>
                ) : (
                  <button onClick={() => { authApi.addEmail(profile.pendingEmail); setCooldown(45); }} style={{ background: "none", border: "none", color: "#f97316", fontWeight: 700, cursor: "pointer", textDecoration: "underline" }}>Gửi lại Link xác nhận</button>
                )}
              </div>
            </div>
            <div style={{ padding: "16px 24px", background: "#f8fafc", display: "flex", gap: 10 }}>
              <button onClick={() => setShowOtpModal(false)} style={{ flex: 1, padding: "12px 0", borderRadius: 12, border: "none", background: "linear-gradient(135deg, #f97316, #ea580c)", color: "#fff", fontWeight: 700, fontSize: 14, cursor: "pointer" }}>
                 Đã hiểu
              </button>
            </div>
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

async function getDriverProfile() {
  try {
    const res = await instance.get("/driver/profile");
    return res.data;
  } catch (e) {
    if (e?.response?.status === 404) return null;
    throw e;
  }
}

function getApiErrorMessage(error, fallback) {
  return (
    error?.response?.data?.message ||
    error?.response?.data?.error ||
    error?.message ||
    fallback
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
