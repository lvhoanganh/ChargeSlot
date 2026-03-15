import { Button } from "@/components/ui/button";
import { Link } from "react-router-dom";

const DEFAULT_AVATAR =
  "https://avatarngau.sbs/wp-content/uploads/2025/07/avatar-vo-danh-va-sach.jpg";

const maskPhone = (phone) =>
  phone ? `${phone.slice(0, 4)} **** ${phone.slice(-3)}` : "";

const ROLE_LABEL = {
  DRIVER: "Tài xế",
  OWNER: "Chủ trạm",
  ADMIN: "Quản trị viên",
};

export default function Profile() {
  const user = {
    name: "",
    phone: "0123456789",
    role: "DRIVER",
    email: "",
    gender: "",
    dob: "",
    address: "",
  };

  const vehicles = [];
  const isDriver = user.role === "DRIVER";

  return (
    <div className="min-h-[calc(100vh-64px)] bg-[#f3f4f5] px-4 py-10 pt-24">
      <div className="max-w-5xl mx-auto bg-white rounded-xl shadow-md">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-10 p-10">
          <div className="flex flex-col items-center">
            <img
              src={DEFAULT_AVATAR}
              alt="Avatar"
              className="w-40 h-40 rounded-full object-cover border-4 border-gray-100"
            />
            <p className="mt-6 text-xl font-bold text-center">
              {user.name || "Chưa cập nhật tên"}
            </p>
          </div>
          <div className="md:col-span-2 space-y-10">
            <section>
              <h1 className="text-2xl font-bold mb-6">Thông tin cá nhân</h1>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-10 gap-y-6">
                <Info label="Vai trò" value={ROLE_LABEL[user.role]} />
                <Info label="Số điện thoại" value={maskPhone(user.phone)} />

                {user.email && <Info label="Email" value={user.email} />}
                {user.gender && <Info label="Giới tính" value={user.gender} />}
                {user.dob && <Info label="Ngày sinh" value={user.dob} />}
                {user.address && (
                  <Info label="Địa chỉ" value={user.address} fullWidth />
                )}
              </div>

              {!user.name && (
                <div className="mt-6 p-4 bg-orange-50 border border-orange-200 rounded-lg">
                  <p className="text-sm text-orange-700">
                    💡 Hồ sơ của bạn chưa hoàn thiện. Vui lòng cập nhật thông
                    tin cá nhân.
                  </p>
                </div>
              )}
            </section>

            {isDriver && (
              <section className="pt-6 border-t border-gray-200">
                <div className="flex items-center justify-between mb-4">
                  <h2 className="text-lg font-semibold">
                    Phương tiện của bạn ({vehicles.length})
                  </h2>
                  <Link to="/manage-vehicles">
                    <Button
                      size="sm"
                      className="bg-orange-500 hover:bg-orange-600"
                    >
                      Quản lý
                    </Button>
                  </Link>
                </div>

                {vehicles.length === 0 && (
                  <p className="text-sm text-gray-500">
                    Bạn chưa đăng ký phương tiện nào.
                  </p>
                )}
              </section>
            )}
            <section className="flex gap-4 pt-4">
              <Link to="/edit-profile" className="w-1/2">
                <Button className="w-full h-12 bg-orange-500 hover:bg-orange-600">
                  Chỉnh sửa hồ sơ
                </Button>
              </Link>

              <Link to="/change-password" className="w-1/2">
                <Button
                  variant="outline"
                  className="w-full h-12 border-red-500 text-red-500 hover:bg-red-50"
                >
                  Đổi mật khẩu
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
