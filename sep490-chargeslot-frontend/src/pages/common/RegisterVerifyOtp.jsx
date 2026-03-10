import { Button } from "@/components/ui/button";
import { Link, useNavigate } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import { instance } from "@/lib/httpRequest";
import { useAuthStore } from "@/stores/authStore";
import { useState } from "react";

const verifyOtp = async ({ phoneNumber, otp }) => {
  const res = await instance.post(
    `${import.meta.env.VITE_BASE_URL}/Auth/register/verify-otp`,
    {
      phoneNumber,
      otp,
    },
  );
  return res.data;
};

export default function RegisterVerifyOtp() {
  const [otp, setOtp] = useState("");
  const { phoneNumber } = useAuthStore();
  const navigate = useNavigate();

  const verifyOtpMutation = useMutation({
    mutationFn: verifyOtp,
    onSuccess: () => {
      alert("Xác thực OTP thành công!");
      navigate("/register/create-account");
    },
    onError: (error) => {
      console.error("Failed to verify OTP:", error);
      alert("Xác thực OTP thất bại. Vui lòng thử lại.");
    },
  });

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!phoneNumber) {
      alert("Vui lòng quay lại trang đăng ký để nhập số điện thoại.");
      navigate("/register");
      return;
    }
    verifyOtpMutation.mutate({ phoneNumber, otp });
  };

  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center">
      <form
        className="max-w-[500px] w-full bg-white rounded-md shadow-md"
        onSubmit={handleSubmit}
      >
        <div className="p-8">
          <h1 className="text-xl font-bold mb-5">Xác thực OTP</h1>
          <p className="mb-5 text-gray-600">
            Mã OTP đã được gửi đến số điện thoại: <strong>{phoneNumber}</strong>
          </p>
          <div className="flex flex-col mb-5">
            <label>Mã OTP</label>
            <input
              className="h-10 px-4 border"
              placeholder="Nhập mã OTP"
              value={otp}
              onChange={(e) => setOtp(e.target.value)}
              maxLength={6}
            />
          </div>
          <Button
            type="submit"
            className="w-full h-12 bg-orange-500 mb-5 cursor-pointer hover:bg-green-500"
            disabled={verifyOtpMutation.isPending}
          >
            {verifyOtpMutation.isPending ? "Đang xác thực..." : "Xác thực OTP"}
          </Button>
          <div className="text-center hover:underline">
            <Link to="/register" className="text-blue-500">
              Quay lại trang đăng ký
            </Link>
          </div>
        </div>
      </form>
    </div>
  );
}
