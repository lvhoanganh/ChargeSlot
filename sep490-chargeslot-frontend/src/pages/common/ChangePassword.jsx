import { Button } from "@/components/ui/button";
import { useNavigate } from "react-router-dom";

export default function ChangePassword() {
  const navigate = useNavigate();
  const role = localStorage.getItem("role") || "";
  const backPath = getBackPathByRole(role);

  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center">
      <form className="max-w-[500px] w-full bg-white rounded-md shadow-md">
        <div className="p-8">
          <h1 className="text-xl font-bold mb-6">
            Đổi mật khẩu
          </h1>

          <div className="flex flex-col mb-5">
            <label className="text-gray-500">Mật khẩu hiện tại</label>
            <input
              type="password"
              className="w-full h-10 px-4 border outline-none"
              placeholder="Nhập mật khẩu hiện tại..."
            />
          </div>

          <div className="flex flex-col mb-5">
            <label className="text-gray-500">Mật khẩu mới</label>
            <input
              type="password"
              className="w-full h-10 px-4 border outline-none"
              placeholder="Nhập mật khẩu mới..."
            />
          </div>

          <div className="flex flex-col mb-6">
            <label className="text-gray-500">Xác nhận mật khẩu mới</label>
            <input
              type="password"
              className="w-full h-10 px-4 border outline-none"
              placeholder="Nhập lại mật khẩu mới..."
            />
          </div>

          <div className="flex gap-3">
            <div className="w-1/2">
              <Button
                type="button"
                className="w-full h-12 bg-gray-300 hover:bg-gray-400 text-black"
                onClick={() => navigate(backPath)}
              >
                Huỷ
              </Button>
            </div>

            <Button className="w-1/2 h-12 bg-red-500 hover:bg-red-600">
              Cập nhật
            </Button>
          </div>
        </div>
      </form>
    </div>
  );
}

function getBackPathByRole(role) {
  const r = String(role || "").trim().toLowerCase();
  if (r === "driver") return "/driver/driver-profile";
  if (r === "owner") return "/owner/owner-profile";
  return "/";
}
