import { Button } from "@/components/ui/button";
import { useLocation, useNavigate } from "react-router-dom";

export default function VerifyOtp() {
  const navigate = useNavigate();
  const location = useLocation();

  const purpose = location.state?.purpose || "register";

  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center">
      <form
        className="max-w-[500px] w-full bg-white rounded-md shadow-md"
        onSubmit={(e) => {
          e.preventDefault();
          navigate("/setPassword");
        }}
      >
        <div className="p-8">
          <h1 className="text-xl font-bold mb-4">
            {purpose === "forgot"
              ? "Xác thực OTP – Quên mật khẩu"
              : "Xác thực OTP – Đăng ký"}
          </h1>

          <div className="flex flex-col mb-5">
            <label>Mã OTP</label>
            <input className="h-10 px-4 border" placeholder="123456" />
          </div>

          <Button className="w-full h-12 bg-orange-500 mb-4">
            Xác nhận OTP
          </Button>

          <button type="button" className="text-blue-500 w-full">
            Gửi lại OTP
          </button>
        </div>
      </form>
    </div>
  );
}
