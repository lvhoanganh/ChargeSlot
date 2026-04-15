import { Button } from "@/components/ui/button";
import { useNavigate } from "react-router-dom";
import { useAuthStore } from "@/stores/authStore";
import { useState } from "react";
import { adminProfileApi } from "@/services/api";

const DEFAULT_AVATAR =
  "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'%3E%3Ccircle cx='50' cy='50' r='50' fill='%23f97316'/%3E%3Ccircle cx='50' cy='38' r='16' fill='%23fff'/%3E%3Cellipse cx='50' cy='75' rx='28' ry='20' fill='%23fff'/%3E%3C/svg%3E";

const maskPhone = (phone) =>
  phone ? `**** **** ${phone.slice(-2)}` : "";

export default function EditAdminProfile() {
  const navigate = useNavigate();

  const { phoneNumber: storedPhoneNumber } = useAuthStore();
  const phoneNumber =
    storedPhoneNumber || localStorage.getItem("phoneNumber") || "";

  const [avatar, setAvatar] = useState(
    () => getStoredAvatarDataUrl(phoneNumber) || DEFAULT_AVATAR,
  );
  const [saving, setSaving] = useState(false);

  const handleAvatarChange = async (e) => {
    const file = e?.target?.files?.[0];
    if (!file) return;

    try {
      // Preview ngay lập tức
      const dataUrl = await readFileAsDataUrl(file);
      setAvatar(dataUrl);

      // Upload lên server (nếu BE có endpoint)
      try {
        const result = await adminProfileApi.uploadAvatar(file);
        if (result?.avatarUrl) {
          const fullUrl = result.avatarUrl.startsWith("/")
            ? `https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net${result.avatarUrl}`
            : result.avatarUrl;
          setAvatar(fullUrl);
          // Lưu server URL vào localStorage để đồng bộ
          saveUserInfoByPhone(phoneNumber, { avatarDataUrl: fullUrl });
          return;
        }
      } catch {
        // BE chưa có endpoint avatar admin → dùng local preview
      }
    } catch {
      // ignore
    }
  };

  const onSave = () => {
    setSaving(true);
    try {
      if (avatar && avatar !== DEFAULT_AVATAR) {
        saveUserInfoByPhone(phoneNumber, { avatarDataUrl: avatar });
      }
      navigate("/admin/admin-profile");
    } catch {
      // ignore
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="min-h-[calc(100vh-64px)] px-4 py-10 pt-24" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #f1f5f9 50%, #e8ecf1 100%)" }}>
      <div className="max-w-4xl mx-auto">
        <div
          className="relative rounded-2xl overflow-hidden shadow-xl mb-8"
          style={{ background: "linear-gradient(135deg, #ff7e29 0%, #f97316 50%, #ea580c 100%)" }}
        >
          <div className="absolute inset-0 opacity-10">
            <div className="absolute top-0 right-0 w-64 h-64 bg-white rounded-full -translate-y-32 translate-x-32" />
            <div className="absolute bottom-0 left-0 w-48 h-48 bg-white rounded-full translate-y-24 -translate-x-24" />
          </div>

          <div className="relative px-8 py-10 flex flex-col md:flex-row items-center gap-6">
            <div className="relative group">
              <img
                src={avatar}
                alt="Avatar"
                className="w-28 h-28 rounded-full object-cover border-4 border-white/40 shadow-2xl"
                style={{ boxShadow: "0 8px 32px rgba(0,0,0,0.2)" }}
              />
              <label className="absolute inset-0 rounded-full bg-black/40 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity cursor-pointer">
                <svg className="w-6 h-6 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 9a2 2 0 012-2h.93a2 2 0 001.664-.89l.812-1.22A2 2 0 0110.07 4h3.86a2 2 0 011.664.89l.812 1.22A2 2 0 0018.07 7H19a2 2 0 012 2v9a2 2 0 01-2 2H5a2 2 0 01-2-2V9z" />
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 13a3 3 0 11-6 0 3 3 0 016 0z" />
                </svg>
                <input
                  type="file"
                  hidden
                  accept="image/*"
                  onChange={handleAvatarChange}
                />
              </label>
            </div>
            <div className="text-center md:text-left">
              <h1 className="text-2xl font-bold text-white mb-1">
                Chỉnh sửa hồ sơ
              </h1>
              <p className="text-white/80 text-sm">
                {maskPhone(phoneNumber) || "Chưa cập nhật số điện thoại"}
              </p>
              <span className="inline-block mt-2 px-3 py-1 text-xs font-semibold rounded-full bg-white/20 text-white backdrop-blur-sm">
                ️ Quản trị viên
              </span>
            </div>
          </div>
        </div>
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
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
              </svg>
              Thông tin cá nhân
            </h2>
          </div>
          <div className="px-8 py-6">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
              <ReadOnlyField icon="️" label="Vai trò" value="Quản trị viên" />
              <ReadOnlyField icon="" label="Số điện thoại" value={phoneNumber || "—"} />
            </div>
          </div>
          <div className="px-8 py-5 bg-gray-50/50 border-t border-gray-100 flex flex-col sm:flex-row gap-3">
            <Button
              type="button"
              variant="outline"
              className="flex-1 h-11 rounded-xl font-medium transition-all"
              onClick={() => navigate("/admin/admin-profile")}
            >
              <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
              Hủy thay đổi
            </Button>
            <Button
              className="flex-1 h-11 bg-green-500 hover:bg-green-600 rounded-xl font-medium shadow-md shadow-green-200 transition-all hover:shadow-lg hover:shadow-green-300"
              onClick={onSave}
              disabled={saving}
            >
              {saving ? (
                <>
                  <div className="w-4 h-4 border-2 border-white/40 border-t-white rounded-full animate-spin mr-2" />
                  Đang lưu...
                </>
              ) : (
                <>
                  <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                  </svg>
                  Lưu thay đổi
                </>
              )}
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}

function ReadOnlyField({ icon, label, value }) {
  return (
    <div className="flex items-start gap-3 p-4 rounded-xl bg-gray-50/80">
      <span className="text-xl leading-none mt-0.5">{icon}</span>
      <div className="min-w-0 flex-1">
        <p className="text-xs text-gray-400 uppercase tracking-wide font-medium mb-0.5">{label}</p>
        <p className="font-medium text-gray-500 truncate">{value}</p>
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

function saveUserInfoByPhone(phoneNumber, patch) {
  if (!phoneNumber) return;
  try {
    const key = "userInfoByPhone";
    const prev = JSON.parse(localStorage.getItem(key) || "{}");
    const normalized = normalizePhoneForKey(phoneNumber);
    prev[phoneNumber] = { ...(prev[phoneNumber] || {}), ...(patch || {}) };
    if (normalized && normalized !== phoneNumber) {
      prev[normalized] = { ...(prev[normalized] || {}), ...(patch || {}) };
    }
    localStorage.setItem(key, JSON.stringify(prev));
  } catch {
    // ignore
  }
}

function normalizePhoneForKey(rawPhone) {
  const phone = String(rawPhone || "").trim().replaceAll(" ", "");
  if (!phone) return "";
  if (phone.startsWith("+84")) return `0${phone.slice(3)}`;
  return phone;
}

function readFileAsDataUrl(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error("Đọc file ảnh thất bại"));
    reader.onload = () => resolve(String(reader.result || ""));
    reader.readAsDataURL(file);
  });
}
