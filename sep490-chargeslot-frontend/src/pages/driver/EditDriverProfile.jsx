import { Button } from "@/components/ui/button";
import { useNavigate } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { driverEditProfileSchema } from "@/schemas/driverEditProfileSchema";
import { useAuthStore } from "@/stores/authStore";
import { instance } from "@/lib/httpRequest";
import { useEffect, useState } from "react";
import { driverProfileApi } from "@/services/api";

const DEFAULT_AVATAR =
  "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'%3E%3Ccircle cx='50' cy='50' r='50' fill='%23f97316'/%3E%3Ccircle cx='50' cy='38' r='16' fill='%23fff'/%3E%3Cellipse cx='50' cy='75' rx='28' ry='20' fill='%23fff'/%3E%3C/svg%3E";

const maskPhone = (phone) =>
  phone ? `**** **** ${phone.slice(-2)}` : "";

export default function EditDriverProfile() {
  const navigate = useNavigate();

  const { phoneNumber: storedPhoneNumber } = useAuthStore();
  const phoneNumber =
    storedPhoneNumber || localStorage.getItem("phoneNumber") || "";

  const [avatar, setAvatar] = useState(
    () => getStoredAvatarDataUrl(phoneNumber) || DEFAULT_AVATAR,
  );

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(driverEditProfileSchema),
    defaultValues: {
      fullName: "",
      vehicleType: "",
      licensePlate: "",
      licenseNumber: "",
    },
  });

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      setError("");
      try {
        const p = await getDriverProfile();
        if (cancelled) return;
        reset({
          fullName: p?.fullName || "",
          vehicleType: p?.vehicleType || "",
          licensePlate: p?.licensePlate || "",
          licenseNumber: p?.licenseNumber || "",
        });
        // Load server avatar if available
        if (p?.avatarUrl) {
          const url = p.avatarUrl.startsWith("/") ? `https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net${p.avatarUrl}` : p.avatarUrl;
          setAvatar(url);
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
  }, []);

  const handleAvatarChange = async (e) => {
    const file = e?.target?.files?.[0];
    if (!file) return;

    try {
      // Preview immediately
      const dataUrl = await readFileAsDataUrl(file);
      setAvatar(dataUrl);

      // Upload to server
      const result = await driverProfileApi.uploadAvatar(file);
      if (result?.avatarUrl) {
        const fullUrl = result.avatarUrl.startsWith("/") ? `https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net${result.avatarUrl}` : result.avatarUrl;
        setAvatar(fullUrl);
      }
    } catch {
      // keep local preview
    }
  };

  const onSubmit = async (values) => {
    setSaving(true);
    setError("");
    try {
      if (avatar && avatar !== DEFAULT_AVATAR) {
        saveUserInfoByPhone(phoneNumber, { avatarDataUrl: avatar });
      }

      const ln = normalizeOptionalText(values?.licenseNumber);

      await upsertDriverProfile({
        vehicleType: normalizeOptionalText(values?.vehicleType) || null,
        licensePlate: normalizeOptionalText(values?.licensePlate) || null,
        licenseNumber: ln || null,
      });

      navigate("/driver/driver-profile");
    } catch (e) {
      setError(getApiErrorMessage(e, "Lưu thất bại"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="min-h-[calc(100vh-64px)] px-4 py-10 pt-24" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #f1f5f9 50%, #e8ecf1 100%)" }}>
      <div className="max-w-4xl mx-auto">
        {/* Header Card */}
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
                🚗 Tài xế
              </span>
            </div>
          </div>
        </div>

        {/* Form Card */}
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

          <form onSubmit={handleSubmit(onSubmit)}>
            <div className="px-8 py-6">
              {loading && (
                <div className="flex items-center justify-center py-8">
                  <div className="w-8 h-8 border-4 border-orange-200 border-t-orange-500 rounded-full animate-spin" />
                  <span className="ml-3 text-gray-500 text-sm">Đang tải hồ sơ...</span>
                </div>
              )}

              {!!error && (
                <div className="mb-6 p-4 bg-red-50 border border-red-200 rounded-xl flex items-center gap-3">
                  <svg className="w-5 h-5 text-red-500 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                  <p className="text-sm text-red-700">{error}</p>
                </div>
              )}

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
                <ReadOnlyField icon="🏷️" label="Vai trò" value="Tài xế" />
                <ReadOnlyField icon="📱" label="Số điện thoại" value={phoneNumber || "—"} />

                <InputField
                  icon="🚙"
                  label="Loại xe"
                  placeholder="Ví dụ: Xe máy, Ô tô..."
                  error={errors.vehicleType?.message}
                  {...register("vehicleType")}
                />
                <InputField
                  icon="🔢"
                  label="Biển số"
                  placeholder="Nhập biển số"
                  error={errors.licensePlate?.message}
                  {...register("licensePlate")}
                />
                <InputField
                  icon="📄"
                  label="Số giấy phép (12 số)"
                  placeholder="Nhập số giấy phép"
                  inputMode="numeric"
                  error={errors.licenseNumber?.message}
                  {...register("licenseNumber")}
                />
              </div>
            </div>

            {/* Actions */}
            <div className="px-8 py-5 bg-gray-50/50 border-t border-gray-100 flex flex-col sm:flex-row gap-3">
              <Button
                type="button"
                variant="outline"
                className="flex-1 h-11 rounded-xl font-medium transition-all"
                onClick={() => navigate("/driver/driver-profile")}
              >
                <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
                Hủy thay đổi
              </Button>
              <Button
                className="flex-1 h-11 bg-green-500 hover:bg-green-600 rounded-xl font-medium shadow-md shadow-green-200 transition-all hover:shadow-lg hover:shadow-green-300"
                type="submit"
                disabled={saving || loading}
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
          </form>
        </div>
      </div>
    </div>
  );
}

import { forwardRef } from "react";

const InputField = forwardRef(function InputField({ icon, label, error, fullWidth = false, ...props }, ref) {
  return (
    <div className={`${fullWidth ? "sm:col-span-2" : ""}`}>
      <div className="flex items-start gap-3 p-4 rounded-xl bg-gray-50/80 hover:bg-gray-100/60 transition-colors">
        <span className="text-xl leading-none mt-2.5">{icon}</span>
        <div className="min-w-0 flex-1">
          <label className="text-xs text-gray-400 uppercase tracking-wide font-medium mb-1.5 block">{label}</label>
          <input
            ref={ref}
            {...props}
            className="w-full h-10 px-3 border border-gray-200 rounded-lg outline-none bg-white focus:border-orange-400 focus:ring-2 focus:ring-orange-100 transition-all text-sm"
          />
          {!!error && <p className="text-red-500 text-xs mt-1.5">{error}</p>}
        </div>
      </div>
    </div>
  );
});

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

async function getDriverProfile() {
  try {
    const res = await instance.get("/driver/profile");
    return res.data;
  } catch (e) {
    if (e?.response?.status === 404) return null;
    throw e;
  }
}

async function upsertDriverProfile(payload) {
  await instance.put("/driver/profile", payload);
}

function getApiErrorMessage(error, fallback) {
  return (
    error?.response?.data?.message ||
    error?.response?.data?.error ||
    error?.message ||
    fallback
  );
}

function normalizeOptionalText(value) {
  if (value == null) return "";
  const text = String(value).trim();
  if (!text) return "";
  const upper = text.toUpperCase();
  if (upper === "N/A" || upper === "NA") return "";
  return text;
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
    reader.onerror = () => reject(new Error("read-failed"));
    reader.onload = () => resolve(String(reader.result || ""));
    reader.readAsDataURL(file);
  });
}
