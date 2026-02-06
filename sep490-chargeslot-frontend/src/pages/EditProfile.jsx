import { Button } from "@/components/ui/button";
import { Link } from "react-router-dom";
import { useState } from "react";

const DEFAULT_AVATAR = "https://avatarngau.sbs/wp-content/uploads/2025/07/avatar-vo-danh-va-sach.jpg";

export default function EditProfile() {
  const [avatarPreview, setAvatarPreview] = useState(null);

  const handleAvatarChange = (e) => {
    const file = e.target.files[0];
    if (!file) return;
    setAvatarPreview(URL.createObjectURL(file));
  };

  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center">
      <form className="max-w-[500px] w-full bg-white rounded-md shadow-md">
        <div className="p-8">

          <h1 className="text-xl font-bold mb-6">
            Chỉnh sửa hồ sơ
          </h1>

          <div className="flex flex-col items-center mb-6">
            <div className="relative">
              <img
                src={avatarPreview || DEFAULT_AVATAR}
                alt="Avatar"
                className="w-24 h-24 rounded-full object-cover"
              />

              <label className="absolute bottom-0 right-0 bg-white p-2 rounded-full shadow cursor-pointer">
                📷
                <input
                  type="file"
                  accept="image/*"
                  hidden
                  onChange={handleAvatarChange}
                />
              </label>
            </div>
          </div>
          <div className="flex flex-col mb-5">
            <label className="text-gray-500">Họ và tên</label>
            <input
              type="text"
              defaultValue="Nguyễn Văn A"
              className="w-full h-10 px-4 border outline-none"
            />
          </div>
          <div className="flex flex-col mb-2">
            <label className="text-gray-500">Số điện thoại</label>
            <input
              type="text"
              defaultValue="0123 456 789"
              className="w-full h-10 px-4 border outline-none"
            />
            <span className="text-xs text-red-500 mt-1">
              Thay đổi số điện thoại sẽ yêu cầu đăng nhập lại
            </span>
          </div>
          <div className="flex flex-col mb-5">
            <label className="text-gray-500">Email</label>
            <input
              type="email"
              defaultValue="example@email.com"
              className="w-full h-10 px-4 border outline-none"
            />
          </div>

          <div className="flex gap-3 mt-6">
            <Link to="/profile" className="w-1/2">
              <Button
                type="button"
                className="w-full h-12 bg-gray-300 hover:bg-gray-400 text-black"
              >
                Huỷ
              </Button>
            </Link>

            <Button className="w-1/2 h-12 bg-green-500 hover:bg-green-600">
              Lưu thay đổi
            </Button>
          </div>

        </div>
      </form>
    </div>
  );
}
