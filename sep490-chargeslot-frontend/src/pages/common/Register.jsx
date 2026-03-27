import { Button } from "@/components/ui/button";
import { Link, useNavigate } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import { instance } from "@/lib/httpRequest";
import { useAuthStore } from "@/stores/authStore";
import { useState } from "react";
import { showToast } from "@/components/Toast";
import ChargeSlotLogo from "@/components/ChargeSlotLogo";

const sendOtp = async (phoneNumber) => {
  const res = await instance.post(
    `${import.meta.env.VITE_BASE_URL}/Auth/register/send-otp`,
    { phoneNumber },
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
      const msg = error?.response?.data?.message || "";
      if (msg.toLowerCase().includes("already registered")) {
        showToast.error("Số điện thoại đã tồn tại. Vui lòng nhập số khác.");
      } else {
        showToast.error(msg || "Gửi OTP thất bại. Vui lòng thử lại.");
      }
    },
  });

  const handleSubmit = (e) => {
    e.preventDefault();
    sendOtpMutation.mutate(phone);
  };

  return (
    <div className="cs-auth-wrapper">
      {/* Left Branding Panel */}
      <div className="cs-auth-left">
        <div className="cs-auth-left__content">
          <div className="cs-animate-fadeInUp" style={{ marginBottom: 24 }}>
            <ChargeSlotLogo size={56} dark />
          </div>
          <h1 className="cs-auth-left__title cs-animate-fadeInUp-delay-1">
            Tạo tài khoản<br />chỉ 3 bước đơn giản
          </h1>
          <p className="cs-auth-left__desc cs-animate-fadeInUp-delay-2">
            Nhập số điện thoại → Xác thực OTP → Điền thông tin. Hoàn tất trong chưa đầy 1 phút!
          </p>
          <div className="cs-auth-left__features cs-animate-fadeInUp-delay-3">
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">📱</span>
              <span>Bước 1: Nhập số điện thoại của bạn</span>
            </div>
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">🔑</span>
              <span>Bước 2: Xác thực mã OTP qua SMS</span>
            </div>
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">✏️</span>
              <span>Bước 3: Điền tên, mật khẩu và chọn vai trò</span>
            </div>
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">✅</span>
              <span>Sẵn sàng sử dụng ngay sau khi đăng ký</span>
            </div>
          </div>
        </div>
      </div>

      {/* Right Form Panel */}
      <div className="cs-auth-right">
        <form className="cs-auth-form" onSubmit={handleSubmit}>
          <div className="cs-auth-form__logo">
            <ChargeSlotLogo size={38} showText />
          </div>
          <h2 className="cs-auth-form__title">Đăng ký tài khoản</h2>
          <p className="cs-auth-form__subtitle">
            Nhập số điện thoại để bắt đầu tạo tài khoản ChargeSlot.
          </p>

          <div className="cs-auth-input-group">
            <label>Số điện thoại</label>
            <input
              className="cs-auth-input"
              placeholder="090xxxxxxx"
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
            />
          </div>

          <button
            type="submit"
            className="cs-auth-submit"
            disabled={sendOtpMutation.isPending}
          >
            {sendOtpMutation.isPending ? "Đang gửi..." : "Gửi OTP đăng ký"}
          </button>

          <p style={{ textAlign: "center", marginTop: 24, fontSize: 14, color: "#64748b" }}>
            Đã có tài khoản?{" "}
            <Link to="/login" className="cs-auth-link">
              Đăng nhập
            </Link>
          </p>
        </form>
      </div>
    </div>
  );
}
