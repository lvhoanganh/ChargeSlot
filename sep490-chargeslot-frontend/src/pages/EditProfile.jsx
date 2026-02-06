import { Button } from "@/components/ui/button";
import { Link } from "react-router-dom";
import { useState } from "react";

const DEFAULT_AVATAR =
  "https://avatarngau.sbs/wp-content/uploads/2025/07/avatar-vo-danh-va-sach.jpg";

// Giả lập role từ backend
const USER_ROLE = "DRIVER"; // DRIVER | OWNER | ADMIN

const ROLE_LABEL = {
  DRIVER: "Tài xế",
  OWNER: "Chủ trạm",
  ADMIN: "Quản trị viên",
};

export default function EditProfile() {
  const [avatar, setAvatar] = useState(DEFAULT_AVATAR);

  return (
    <div className="min-h-[calc(100vh-64px)] bg-[#f3f4f5] px-4 py-10 pt-24">
      <div className="max-w-5xl mx-auto bg-white rounded-xl shadow-md">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-10 p-10">
          
          {/* LEFT – AVATAR */}
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
                onChange={(e) =>
                  e.target.files &&
                  setAvatar(URL.createObjectURL(e.target.files[0]))
                }
              />
            </label>
          </div>

          {/* RIGHT – FORM */}
          <div className="md:col-span-2 space-y-10">
            <h1 className="text-2xl font-bold">Chỉnh sửa hồ sơ</h1>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-10 gap-y-6">

              {/* VAI TRÒ – KHÔNG CHO SỬA */}
              <div>
                <label className="text-gray-500 text-sm mb-1 block">
                  Vai trò
                </label>
                <input
                  value={ROLE_LABEL[USER_ROLE]}
                  disabled
                  className="w-full h-11 px-4 border rounded-md bg-gray-100 text-gray-500 cursor-not-allowed"
                />
                <p className="text-xs text-gray-400 mt-1">
                  Vai trò do hệ thống cấp và không thể thay đổi
                </p>
              </div>

              {/* HỌ TÊN */}
              <Input label="Họ và tên" placeholder="Nhập họ tên" />

              {/* SỐ ĐIỆN THOẠI */}
              <div>
                <label className="text-gray-500 text-sm mb-1 block">
                  Số điện thoại
                </label>
                <div className="flex items-center justify-between h-11 px-4 border rounded-md bg-gray-50">
                  <span className="font-medium">0123 **** 789</span>
                  <Link
                    to="/change-phone"
                    className="text-sm text-orange-500"
                  >
                    Đổi số
                  </Link>
                </div>
              </div>

              {/* EMAIL */}
              <Input label="Email" placeholder="example@email.com" />

              {/* GIỚI TÍNH */}
              <Select label="Giới tính" />

              {/* NGÀY SINH */}
              <Input label="Ngày sinh" type="date" />

              {/* ĐỊA CHỈ */}
              <Input
                label="Địa chỉ liên hệ"
                placeholder="Nhập địa chỉ"
                fullWidth
              />
            </div>

            {/* ACTION */}
            <div className="flex gap-4 pt-6">
              <Link to="/profile" className="w-1/2">
                <Button variant="outline" className="w-full h-12">
                  Huỷ
                </Button>
              </Link>

              <Button className="w-1/2 h-12 bg-green-500 hover:bg-green-600">
                Lưu thay đổi
              </Button>
            </div>
          </div>

        </div>
      </div>
    </div>
  );
}

/* ===== COMPONENTS ===== */

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

function Select({ label }) {
  return (
    <div>
      <label className="text-gray-500 text-sm mb-1 block">{label}</label>
      <select className="w-full h-11 px-4 border rounded-md">
        <option value="">Chọn</option>
        <option>Nam</option>
        <option>Nữ</option>
        <option>Khác</option>
      </select>
    </div>
  );
}
