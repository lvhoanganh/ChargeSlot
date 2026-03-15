import { Button } from "@/components/ui/button";
import { instance } from "@/lib/httpRequest";
import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";

const DEFAULT_AVATAR =
  "https://avatarngau.sbs/wp-content/uploads/2025/07/avatar-vo-danh-va-sach.jpg";

export default function EditOwnerProfile() {
  const navigate = useNavigate();

  const phoneNumber = localStorage.getItem("phoneNumber") || "";

  const [fullName, setFullName] = useState(() => getStoredFullName(phoneNumber));
  const [avatar, setAvatar] = useState(
    () => getStoredAvatarDataUrl(phoneNumber) || DEFAULT_AVATAR,
  );

  const [businessName, setBusinessName] = useState("");
  const [taxCode, setTaxCode] = useState("");

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      setError("");
      try {
        const p = await getOwnerProfile();
        if (cancelled) return;
        setBusinessName(p?.businessName || "");
        setTaxCode(normalizeOptionalText(p?.taxCode));
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
      if (!fn) throw new Error("Vui lòng nhập họ và tên");

      const bn = normalizeOptionalText(businessName);
      if (!bn) throw new Error("Vui lòng nhập tên doanh nghiệp");

      const tc = normalizeOptionalText(taxCode);
      if (!/^\d{10}$/.test(tc)) {
        throw new Error("Mã số thuế phải đúng 10 chữ số");
      }
      localStorage.setItem("fullName", fn);
      saveUserInfoByPhone(phoneNumber, { fullName: fn });

      await upsertOwnerProfile({
        businessName: bn,
        taxCode: tc,
      });

      navigate("/owner/owner-profile");
    } catch (e) {
      setError(getApiErrorMessage(e, "Lưu thất bại"));
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
              <ReadOnly label="Vai trò" value="Chủ trạm" />
              <ReadOnly label="Số điện thoại" value={phoneNumber || ""} />

              <Input
                label="Họ và tên"
                placeholder="Nhập họ và tên"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                fullWidth
              />

              <Input
                label="Tên doanh nghiệp"
                placeholder="Nhập tên doanh nghiệp"
                value={businessName}
                onChange={(e) => setBusinessName(e.target.value)}
              />
              <Input
                label="Mã số thuế (10 chữ số)"
                placeholder="Nhập mã số thuế"
                value={taxCode}
                onChange={(e) => setTaxCode(e.target.value)}
                inputMode="numeric"
              />
            </div>

            {loading && (
              <div className="p-4 bg-gray-50 border border-gray-200 rounded-lg">
                <p className="text-sm text-gray-600">Đang tải...</p>
              </div>
            )}

            {!!error && (
              <div className="p-4 bg-red-50 border border-red-200 rounded-lg">
                <p className="text-sm text-red-700">{error}</p>
              </div>
            )}

            <div className="flex gap-4 pt-6">
              <Link to="/owner/owner-profile" className="w-1/2">
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

async function getOwnerProfile() {
  try {
    const res = await instance.get("/owner/profile");
    return res.data;
  } catch (e) {
    if (e?.response?.status === 404) return null;
    throw e;
  }
}

async function upsertOwnerProfile(payload) {
  await instance.put("/owner/profile", payload);
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

