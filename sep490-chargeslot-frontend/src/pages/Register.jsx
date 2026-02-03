import { Button } from "@/components/ui/button";
import { Link, useNavigate } from "react-router-dom";

export default function Register() {
  const navigate = useNavigate();

  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center">
      <form
        className="max-w-[500px] w-full bg-white rounded-md shadow-md"
        onSubmit={(e) => {
          e.preventDefault();
          navigate("/verifyOtp", {
            state: {
              purpose: "register",
              role: "Driver", // UI mock
            },
          });
        }}
      >
        <div className="p-8">
          <h1 className="text-xl font-bold mb-5">Đăng ký tài khoản</h1>

          <div className="flex flex-col mb-5">
            <label>Số điện thoại</label>
            <input className="h-10 px-4 border" placeholder="090xxxxxxx" />
          </div>

          <div className="flex flex-col mb-6">
            <label className="mb-2">Vai trò</label>
            <select className="h-10 border px-3">
              <option>Driver</option>
              <option>Owner</option>
            </select>
          </div>

          <Button type="submit" className="w-full h-12 bg-orange-500 mb-5">
            Gửi OTP đăng ký
          </Button>

          <div className="text-center">
            <Link to="/login" className="text-blue-500">
              Đã có tài khoản? Đăng nhập
            </Link>
          </div>
        </div>
      </form>
    </div>
  );
}
