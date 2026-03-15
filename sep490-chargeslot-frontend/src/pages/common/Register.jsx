import { Button } from "@/components/ui/button";
import { Link, useNavigate } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import { instance } from "@/lib/httpRequest";
import { useAuthStore } from "@/stores/authStore";
import { useState } from "react";

const sendOtp = async (phoneNumber) => {
  const res = await instance.post(
    `${import.meta.env.VITE_BASE_URL}/Auth/register/send-otp`,
    {
      phoneNumber,
    },
  );
  return res.data;
};

export default function Register() {
  const [phone, setPhone] = useState("");
  const { setPhoneNumber } = useAuthStore();
  const navigate = useNavigate();
  const sendOtpMutation = useMutation({
    mutationFn: sendOtp,
    onSuccess: () => {
      setPhoneNumber(phone);
      navigate("/register/verify-otp");
    },
    onError: (error) => {
      console.error("Failed to send OTP:", error);
      alert("Gửi OTP thất bại. Vui lòng thử lại.");
    },
  });
  const handleSubmit = (e) => {
    e.preventDefault();
    sendOtpMutation.mutate(phone);
  };
  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center">
      <form
        className="max-w-[500px] w-full bg-white rounded-md shadow-md"
        onSubmit={handleSubmit}
      >
        <div className="p-8">
          <h1 className="text-xl font-bold mb-5">Đăng ký tài khoản</h1>
          <div className="flex flex-col mb-5">
            <label>Số điện thoại</label>
            <input
              className="h-10 px-4 border"
              placeholder="090xxxxxxx"
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
            />
          </div>
          <Button
            type="submit"
            className="w-full h-12 bg-orange-500 mb-5 cursor-pointer hover:bg-green-500"
          >
            Gửi OTP đăng ký
          </Button>
          <div className="text-center hover:underline">
            <Link to="/login" className="text-blue-500">
              Đã có tài khoản? Đăng nhập
            </Link>
          </div>
        </div>
      </form>
    </div>
  );
}
