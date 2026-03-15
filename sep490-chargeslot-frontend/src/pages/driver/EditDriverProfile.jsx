import { Button } from "@/components/ui/button";
import { instance } from "@/lib/httpRequest";
import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";

const DEFAULT_AVATAR =
  "https://avatarngau.sbs/wp-content/uploads/2025/07/avatar-vo-danh-va-sach.jpg";

export default function EditDriverProfile() {
  const navigate = useNavigate();

  const phoneNumber = localStorage.getItem("phoneNumber") || "";

  const [fullName, setFullName] = useState(() => getStoredFullName(phoneNumber));
  const [avatar, setAvatar] = useState(
    () => getStoredAvatarDataUrl(phoneNumber) || DEFAULT_AVATAR,
  );

  const [vehicleType, setVehicleType] = useState("");
  const [licensePlate, setLicensePlate] = useState("");
  const [licenseNumber, setLicenseNumber] = useState("");

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
        setVehicleType(p?.vehicleType || "");
        setLicensePlate(p?.licensePlate || "");
        setLicenseNumber(p?.licenseNumber || "");
      } catch (e) {
        if (!cancelled) setError(getApiErrorMessage(e, "Khong the tai thong tin ho so"));
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
      const dataUrl = await readFileAsDataUrl(file);
      setAvatar(dataUrl);
      saveUserInfoByPhone(phoneNumber, { avatarDataUrl: dataUrl });
    } catch {
      // ignore
    }
  };

  const onSave = async () => {
    setSaving(true);
    setError("");
    try {
      const fn = normalizeOptionalText(fullName);
      if (!fn) throw new Error("Vui long nhap ho va ten");

      // Persist name locally so Profile keeps showing it after edit.
      localStorage.setItem("fullName", fn);
      saveUserInfoByPhone(phoneNumber, { fullName: fn });

      const ln = normalizeOptionalText(licenseNumber);
      if (ln && !/^\d{12}$/.test(ln)) {
        throw new Error("So giay phep phai dung 12 chu so");
      }

      await upsertDriverProfile({
        vehicleType: normalizeOptionalText(vehicleType) || null,
        licensePlate: normalizeOptionalText(licensePlate) || null,
        licenseNumber: ln || null,
      });

      navigate("/driver/driver-profile");
    } catch (e) {
      setError(getApiErrorMessage(e, "Luu that bai"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="min-h-[calc(100vh-64px)] bg-[#f3f4f5] px-4 py-10 pt-24">
      <div className="max-w-5xl mx-auto bg-white rounded-xl shadow-md">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-10 p-10">
          <div className="flex flex-col items-center">
            <img
              src={avatar}
              alt="Avatar"
              className="w-40 h-40 rounded-full object-cover border-4 border-gray-100"
            />
            <label className="mt-4 text-sm text-orange-500 cursor-pointer">
              Đổi ảnh đại diện
              <input
                type="file"
                hidden
                accept="image/*"
                onChange={handleAvatarChange}
              />
            </label>
          </div>

          <div className="md:col-span-2 space-y-10">
            <h1 className="text-2xl font-bold">Chỉnh sửa hồ sơ</h1>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-10 gap-y-6">
              <ReadOnly label="Vai trò" value="Tài xế" />
              <ReadOnly label="Số điện thoại" value={phoneNumber || ""} />

              <Input
                label="Họ và tên"
                placeholder="Nhập họ và tên"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                fullWidth
              />

              <Input
                label="Loại xe"
                placeholder="Ví dụ: Xe máy, Ô tô..."
                value={vehicleType}
                onChange={(e) => setVehicleType(e.target.value)}
              />
              <Input
                label="Biển số"
                placeholder="Nhập biển số"
                value={licensePlate}
                onChange={(e) => setLicensePlate(e.target.value)}
              />
              <Input
                label="Số giấy phép (12 số)"
                placeholder="Nhập số giấy phép"
                value={licenseNumber}
                onChange={(e) => setLicenseNumber(e.target.value)}
                inputMode="numeric"
              />
            </div>

            {loading && (
              <div className="p-4 bg-gray-50 border border-gray-200 rounded-lg">
                <p className="text-sm text-gray-600">Dang tai...</p>
              </div>
            )}

            {!!error && (
              <div className="p-4 bg-red-50 border border-red-200 rounded-lg">
                <p className="text-sm text-red-700">{error}</p>
              </div>
            )}

            <div className="flex gap-4 pt-6">
              <Link to="/driver/driver-profile" className="w-1/2">
                <Button variant="outline" className="w-full h-12">
                  Hủy thay đổi
                </Button>
              </Link>
              <Button
                className="w-1/2 h-12 bg-green-500 hover:bg-green-600"
                onClick={onSave}
                disabled={saving || loading}
              >
                {saving ? "Đang lưu..." : "Lưu thay đổi"}
              </Button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function Input({ label, fullWidth = false, ...props }) {
  return (
    <div className={fullWidth ? "sm:col-span-2" : ""}>
      <label className="text-gray-500 text-sm mb-1 block">{label}</label>
      <input
        {...props}
        className="w-full h-11 px-4 border rounded-md outline-none"
      />
    </div>
  );
}

function ReadOnly({ label, value }) {
  return (
    <div>
      <label className="text-gray-500 text-sm mb-1 block">{label}</label>
      <input
        value={value}
        disabled
        className="w-full h-11 px-4 border rounded-md bg-gray-100 text-gray-500 cursor-not-allowed"
      />
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

function getStoredFullName(phoneNumber) {
  const direct = localStorage.getItem("fullName") || "";
  if (direct) return direct;
  if (!phoneNumber) return "";
  try {
    const map = JSON.parse(localStorage.getItem("userInfoByPhone") || "{}");
    return map?.[phoneNumber]?.fullName || "";
  } catch {
    return "";
  }
}

function getStoredAvatarDataUrl(phoneNumber) {
  if (!phoneNumber) return "";
  try {
    const map = JSON.parse(localStorage.getItem("userInfoByPhone") || "{}");
    return map?.[phoneNumber]?.avatarDataUrl || "";
  } catch {
    return "";
  }
}

function saveUserInfoByPhone(phoneNumber, patch) {
  if (!phoneNumber) return;
  try {
    const key = "userInfoByPhone";
    const prev = JSON.parse(localStorage.getItem(key) || "{}");
    prev[phoneNumber] = { ...(prev[phoneNumber] || {}), ...(patch || {}) };
    localStorage.setItem(key, JSON.stringify(prev));
  } catch {
    // ignore
  }
}

function readFileAsDataUrl(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error("read-failed"));
    reader.onload = () => resolve(String(reader.result || ""));
    reader.readAsDataURL(file);
  });
}

