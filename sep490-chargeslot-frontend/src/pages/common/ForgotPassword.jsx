import { Button } from "@/components/ui/button";
import { Link, useNavigate } from "react-router-dom";

export default function ForgotPassword() {
  const navigate = useNavigate();

  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center">
      <form
        className="max-w-[500px] w-full bg-white rounded-md shadow-md"
        onSubmit={(e) => {
          e.preventDefault();
          navigate("/verifyOtp", {
            state: { purpose: "forgot" },
          });
        }}
      >
        <div className="p-8">
          <h1 className="text-xl font-bold mb-5">Quên mật khẩu</h1>

          <div className="flex flex-col mb-5">
            <label>Số điện thoại</label>
            <input
              className="h-10 px-4 border"
              placeholder="Nhập số điện thoại..."
            />
          </div>

          <Button className="w-full h-12 bg-orange-500 mb-5">
            Gửi OTP
          </Button>

          <div className="text-center">
            <Link to="/login" className="text-blue-500 hover:underline">
              Quay lại đăng nhập
            </Link>
          </div>
        </div>
      </form>
    </div>
  );
}
