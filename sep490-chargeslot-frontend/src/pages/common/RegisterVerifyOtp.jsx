import { Link, useNavigate } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import { instance } from "@/lib/httpRequest";
import { useAuthStore } from "@/stores/authStore";
import { useState, useEffect } from "react";
import { showToast } from "@/components/Toast";
import ChargeSlotLogo from "@/components/ChargeSlotLogo";

const OTP_DURATION = 60;

const verifyOtp = async ({ phoneNumber, otp }) => {
  const res = await instance.post(
    `${import.meta.env.VITE_BASE_URL}/Auth/register/verify-otp`,
    { phoneNumber, otp },
  );
  return res.data;
};

const resendOtp = async (phoneNumber) => {
  const res = await instance.post(
    `${import.meta.env.VITE_BASE_URL}/Auth/register/send-otp`,
    { phoneNumber },
  );
  return res.data;
};

export default function RegisterVerifyOtp() {
  const [otp, setOtp] = useState("");
  const [countdown, setCountdown] = useState(OTP_DURATION);
  const { phoneNumber } = useAuthStore();
  const navigate = useNavigate();

  useEffect(() => {
    if (countdown <= 0) return;
    const timer = setInterval(() => setCountdown((s) => s - 1), 1000);
    return () => clearInterval(timer);
  }, [countdown]);

  const verifyOtpMutation = useMutation({
    mutationFn: verifyOtp,
    onSuccess: () => {
      showToast.success("Xác thực OTP thành công!");
      navigate("/register/create-account");
    },
    onError: (error) => {
      console.error("Failed to verify OTP:", error);
      showToast.error("Xác thực OTP thất bại. Vui lòng thử lại.");
    },
  });

  const resendOtpMutation = useMutation({
    mutationFn: resendOtp,
    onSuccess: () => {
      showToast.success("Đã gửi lại mã OTP!");
      setCountdown(OTP_DURATION);
    },
    onError: (error) => {
      console.error("Failed to resend OTP:", error);
      showToast.error("Gửi lại OTP thất bại. Vui lòng thử lại.");
    },
  });

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!phoneNumber) {
      showToast.warning("Vui lòng quay lại trang đăng ký để nhập số điện thoại.");
      navigate("/register");
      return;
    }
    verifyOtpMutation.mutate({ phoneNumber, otp });
  };

  const minutes = String(Math.floor(countdown / 60)).padStart(2, "0");
  const seconds = String(countdown % 60).padStart(2, "0");
  const progress = countdown / OTP_DURATION;
  const isExpired = countdown <= 0;

  return (
    <div className="cs-auth-wrapper">
      <div className="cs-auth-left">
        <div className="cs-auth-left__content">
          <div className="cs-animate-fadeInUp" style={{ marginBottom: 24 }}>
            <ChargeSlotLogo size={56} dark />
          </div>
          <h1 className="cs-auth-left__title cs-animate-fadeInUp-delay-1">
            Xác thực tài khoản
          </h1>
          <p className="cs-auth-left__desc cs-animate-fadeInUp-delay-2">
            Chúng tôi đã gửi mã OTP đến số điện thoại của bạn. Vui lòng kiểm tra và nhập mã để tiếp tục.
          </p>
        </div>
      </div>

      <div className="cs-auth-right">
        <form className="cs-auth-form" onSubmit={handleSubmit}>
          <div className="cs-auth-form__logo">
            <ChargeSlotLogo size={38} showText />
          </div>
          <h2 className="cs-auth-form__title">Xác thực OTP</h2>
          <p className="cs-auth-form__subtitle">
            Mã OTP đã được gửi đến số: <strong style={{ color: "#f97316" }}>{phoneNumber}</strong>
          </p>

          {/* Countdown Timer */}
          <div className="cs-otp-countdown">
            <div className={`cs-otp-countdown__circle ${isExpired ? "cs-otp-countdown__circle--expired" : ""}`}>
              <svg viewBox="0 0 80 80" className="cs-otp-countdown__svg">
                <circle cx="40" cy="40" r="34" className="cs-otp-countdown__track" />
                <circle
                  cx="40" cy="40" r="34"
                  className="cs-otp-countdown__progress"
                  style={{
                    strokeDasharray: `${2 * Math.PI * 34}`,
                    strokeDashoffset: `${2 * Math.PI * 34 * (1 - progress)}`,
                  }}
                />
              </svg>
              <span className="cs-otp-countdown__time">{minutes}:{seconds}</span>
            </div>
            <p className="cs-otp-countdown__label">
              {isExpired ? "Mã OTP đã hết hạn" : "Thời gian còn lại"}
            </p>
          </div>

          <div className="cs-auth-input-group">
            <label>Mã OTP</label>
            <input
              className="cs-auth-input"
              placeholder="Nhập mã OTP 6 số"
              value={otp}
              onChange={(e) => setOtp(e.target.value)}
              maxLength={6}
              style={{ letterSpacing: "6px", textAlign: "center", fontSize: 20, fontWeight: 700 }}
              disabled={isExpired}
            />
          </div>

          <button
            type="submit"
            className="cs-auth-submit"
            disabled={verifyOtpMutation.isPending || isExpired}
          >
            {verifyOtpMutation.isPending ? "Đang xác thực..." : isExpired ? "Mã đã hết hạn" : "Xác thực OTP"}
          </button>

          {isExpired && (
            <div style={{ textAlign: "center", marginTop: 16 }}>
              <button
                type="button"
                className="cs-otp-resend-btn"
                onClick={() => resendOtpMutation.mutate(phoneNumber)}
                disabled={resendOtpMutation.isPending}
              >
                {resendOtpMutation.isPending ? "Đang gửi..." : "🔄 Gửi lại mã OTP"}
              </button>
            </div>
          )}

          <p style={{ textAlign: "center", marginTop: 24, fontSize: 14, color: "#64748b" }}>
            <Link to="/register" className="cs-auth-link">
              Quay lại trang đăng ký
            </Link>
          </p>
        </form>
      </div>
    </div>
  );
}
