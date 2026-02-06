import { Button } from "@/components/ui/button";
import { Link, useNavigate } from "react-router-dom";

export default function ChangePhone() {
  const navigate = useNavigate();

  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center px-4">
      <div className="max-w-[500px] w-full bg-white rounded-xl shadow-md">
        <div className="p-8">
          <h1 className="text-xl font-bold mb-4">
            Đổi số điện thoại
          </h1>

          <p className="text-sm text-gray-500 mb-6">
            Nhập số điện thoại mới. Mã OTP sẽ được gửi để xác thực.
          </p>

          <div className="flex flex-col mb-6">
            <label className="text-gray-500 mb-1">
              Số điện thoại mới
            </label>
            <input
              type="tel"
              placeholder="Nhập số điện thoại"
              className="h-11 px-4 border rounded-md outline-none"
            />
          </div>

          <Button
            onClick={() =>
              navigate("/verifyOtp", {
                state: { purpose: "change-phone" },
              })
            }
            className="w-full h-12 bg-orange-500 hover:bg-orange-600 mb-4"
          >
            Gửi mã OTP
          </Button>

          <Link
            to="/edit-profile"
            className="block text-center text-sm text-gray-500 hover:underline"
          >
            ← Quay lại chỉnh sửa hồ sơ
          </Link>
        </div>
      </div>
    </div>
  );
}
