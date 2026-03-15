import { Button } from "@/components/ui/button";
import { instance } from "@/lib/httpRequest";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

const DEFAULT_AVATAR =
  "https://avatarngau.sbs/wp-content/uploads/2025/07/avatar-vo-danh-va-sach.jpg";

const maskPhone = (phone) =>
  phone ? `${phone.slice(0, 4)} **** ${phone.slice(-3)}` : "";

export default function DriverProfile() {
  const fullName = localStorage.getItem("fullName") || "";
  const phoneNumber = localStorage.getItem("phoneNumber") || "";
  const avatarSrc = getStoredAvatarDataUrl(phoneNumber) || DEFAULT_AVATAR;

  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      setError("");
      try {
        const data = await getDriverProfile();
        if (!cancelled) setProfile(data);
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

  return (
    <div className="min-h-[calc(100vh-64px)] bg-[#f3f4f5] px-4 py-10 pt-24">
      <div className="max-w-5xl mx-auto bg-white rounded-xl shadow-md">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-10 p-10">
          <div className="flex flex-col items-center">
            <img
              src={avatarSrc}
              alt="Avatar"
              className="w-40 h-40 rounded-full object-cover border-4 border-gray-100"
            />
            <p className="mt-6 text-xl font-bold text-center">
              {fullName || "Chưa cập nhật tên"}
            </p>
          </div>

          <div className="md:col-span-2 space-y-10">
            <section>
              <h1 className="text-2xl font-bold mb-6">Thông tin cá nhân</h1>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-10 gap-y-6">
                <Info label="Vai trò" value="Tài xế" />
                <Info
                  label="Số điện thoại"
                  value={maskPhone(phoneNumber) || "-"}
                />
                <Info label="Loại xe" value={profile?.vehicleType || "-"} />
                <Info label="Biển số" value={profile?.licensePlate || "-"} />
                <Info label="Số giấy phép" value={profile?.licenseNumber || "-"} />
              </div>

              {loading && (
                <div className="mt-6 p-4 bg-gray-50 border border-gray-200 rounded-lg">
                  <p className="text-sm text-gray-600">Đang tải hồ sơ...</p>
                </div>
              )}

              {!!error && (
                <div className="mt-6 p-4 bg-red-50 border border-red-200 rounded-lg">
                  <p className="text-sm text-red-700">{error}</p>
                </div>
              )}

              {!loading && !error && profile == null && (
                <div className="mt-6 p-4 bg-orange-50 border border-orange-200 rounded-lg">
                  <p className="text-sm text-orange-700">
                    Hồ sơ của bạn chưa hoàn thiện. Vui lòng cập nhật thông tin.
                  </p>
                </div>
              )}
            </section>

            <section className="flex gap-4 pt-4">
              <Link to="/driver/update-driver-profile" className="w-1/2">
                <Button className="w-full h-12 bg-orange-500 hover:bg-orange-600">
                  Chỉnh sửa hồ sơ
                </Button>
              </Link>

              <Link to="/change-password" className="w-1/2">
                <Button
                  variant="outline"
                  className="w-full h-12 border-red-500 text-red-500 hover:bg-red-50"
                >
                Thay đổi mật khẩu
                </Button>
              </Link>
            </section>
          </div>
        </div>
      </div>
    </div>
  );
}

function Info({ label, value, fullWidth = false }) {
  return (
    <div className={fullWidth ? "sm:col-span-2" : ""}>
      <p className="text-gray-500 text-sm mb-1">{label}</p>
      <p className="font-medium">{value}</p>
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
    return map?.[phoneNumber]?.avatarDataUrl || "";
  } catch {
    return "";
  }
}
