import { Button } from "@/components/ui/button";
import { Link } from "react-router-dom";

const DEFAULT_AVATAR = "https://avatarngau.sbs/wp-content/uploads/2025/07/avatar-vo-danh-va-sach.jpg";

export default function Profile() {
  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center">
      <div className="max-w-[600px] w-full bg-white rounded-md shadow-md">
        <div className="p-8">

          <div className="flex flex-col items-center mb-6">
            <img
              src={DEFAULT_AVATAR}
              alt="Avatar"
              className="w-24 h-24 rounded-full object-cover"
            />
            <p className="mt-3 font-semibold">Nguyễn Văn A</p>
          </div>

          <h1 className="text-xl font-bold mb-6">
            Thông tin cá nhân
          </h1>

          <div className="space-y-4">
            <Info label="Số điện thoại" value="0123 456 789" />
            <Info label="Email" value="example@email.com" />
          </div>

          <div className="mt-8 flex gap-4">
            <Link to="/edit-profile" className="w-1/2">
              <Button className="w-full h-12 bg-orange-500 hover:bg-green-500">
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
          </div>

        </div>
      </div>
    </div>
  );
}

function Info({ label, value }) {
  return (
    <div>
      <p className="text-gray-500">{label}</p>
      <p className="font-medium">{value}</p>
    </div>
  );
}
