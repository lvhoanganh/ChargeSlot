import { instance } from "@/lib/httpRequest";
import { verifyOtpSchema } from "@/schemas/verifyOtpSchema";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { useLocation, useNavigate } from "react-router-dom";
import ChargeSlotLogo from "@/components/ChargeSlotLogo";
import { showToast } from "@/components/Toast";

const OTP_DURATION = 60;

const FORGOT_PHONE_KEY = "forgotPasswordPhoneNumber";

export default function VerifyOtp() {
  const navigate = useNavigate();
  const location = useLocation();

  const purpose = location.state?.purpose || "forgot";
  const [cooldownSeconds, setCooldownSeconds] = useState(0);
  const [countdown, setCountdown] = useState(OTP_DURATION);

  const phoneNumber = useMemo(() => {
    return (
      location.state?.phoneNumber ||
      localStorage.getItem(FORGOT_PHONE_KEY) ||
      ""
    );
  }, [location.state?.phoneNumber]);

  const form = useForm({
    resolver: zodResolver(verifyOtpSchema),
    defaultValues: { otp: "" },
    mode: "onTouched",
  });

  useEffect(() => {
    if (!phoneNumber) {
      navigate("/forgotPassword", { replace: true });
    }
  }, [phoneNumber, navigate]);

  useEffect(() => {
    if (cooldownSeconds <= 0) return;
    const t = setInterval(() => setCooldownSeconds((s) => s - 1), 1000);
    return () => clearInterval(t);
  }, [cooldownSeconds]);

  useEffect(() => {
    if (countdown <= 0) return;
    const t = setInterval(() => setCountdown((s) => s - 1), 1000);
    return () => clearInterval(t);
  }, [countdown]);

  const verifyOtpMutation = useMutation({
    mutationFn: async ({ phoneNumber, otp }) => {
      const url =
        purpose === "register"
          ? `${import.meta.env.VITE_BASE_URL}/Auth/register/verify-otp`
          : `${import.meta.env.VITE_BASE_URL}/Auth/forgot-password/verify-otp`;

      const res = await instance.post(url, { phoneNumber, otp });
      return res.data;
    },
    onSuccess: () => {
      showToast.success("Xác thực OTP thành công!");
      if (purpose === "register") {
        navigate("/register/create-account", { replace: true });
        return;
      }
      navigate("/reset-password", { replace: true, state: { phoneNumber } });
    },
    onError: (error) => {
      const msg = error?.response?.data?.message || "";
      showToast.error(msg || "Xác thực OTP thất bại. Vui lòng thử lại.");
    },
  });

  const resendOtpMutation = useMutation({
    mutationFn: async (phoneNumber) => {
      const url =
        purpose === "register"
          ? `${import.meta.env.VITE_BASE_URL}/Auth/register/send-otp`
          : `${import.meta.env.VITE_BASE_URL}/Auth/forgot-password/send-otp`;

      const res = await instance.post(url, { phoneNumber });
      return res.data;
    },
    onSuccess: () => {
      showToast.success("Đã gửi lại mã OTP!");
      setCooldownSeconds(60);
      setCountdown(OTP_DURATION);
    },
    onError: (error) => {
      const msg = error?.response?.data?.message || "";
      showToast.error(msg || "Gửi lại OTP thất bại. Vui lòng thử lại.");
    },
  });

  const title =
    purpose === "register" ? "Xác thực OTP – Đăng ký" : "Xác thực OTP – Quên mật khẩu";

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
            Xác thực mã OTP
          </h1>
          <p className="cs-auth-left__desc cs-animate-fadeInUp-delay-2">
            Mã xác thực đã được gửi đến số điện thoại của bạn. Vui lòng kiểm tra tin nhắn.
          </p>
          <div className="cs-auth-left__features cs-animate-fadeInUp-delay-3">
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon"></span>
              <span>Kiểm tra tin nhắn SMS</span>
            </div>
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon"></span>
              <span>Nhập mã OTP 6 số</span>
            </div>
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon"></span>
              <span>Gửi lại mã nếu không nhận được</span>
            </div>
          </div>
        </div>
      </div>

      <div className="cs-auth-right">
        <form
          className="cs-auth-form"
          onSubmit={form.handleSubmit((values) => {
            verifyOtpMutation.mutate({ phoneNumber, otp: values.otp });
          })}
        >
          <div className="cs-auth-form__logo">
            <ChargeSlotLogo size={38} showText />
          </div>
          <h2 className="cs-auth-form__title">{title}</h2>
          <p className="cs-auth-form__subtitle">
            Mã OTP đã gửi đến: <strong style={{ color: "#f97316" }}>{phoneNumber}</strong>
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
              placeholder="123456"
              maxLength={6}
              {...form.register("otp")}
              style={{ letterSpacing: "6px", textAlign: "center", fontSize: 20, fontWeight: 700 }}
            />
            {!!form.formState.errors.otp?.message && (
              <p className="text-red-500 text-sm mt-1.5">
                {form.formState.errors.otp.message}
              </p>
            )}
          </div>

          <button
            type="submit"
            className="cs-auth-submit"
            disabled={verifyOtpMutation.isPending || isExpired}
          >
            {verifyOtpMutation.isPending ? "Đang xác nhận..." : isExpired ? "Mã đã hết hạn" : "Xác nhận OTP"}
          </button>

          <div style={{ textAlign: "center", marginTop: 20 }}>
            <button
              type="button"
              className="cs-auth-link"
              style={{ background: "none", border: "none", cursor: "pointer", fontSize: 14 }}
              onClick={() => resendOtpMutation.mutate(phoneNumber)}
              disabled={resendOtpMutation.isPending || cooldownSeconds > 0}
            >
              {cooldownSeconds > 0
                ? `Gửi lại OTP (${cooldownSeconds}s)`
                : resendOtpMutation.isPending
                  ? "Đang gửi..."
                  : "Gửi lại OTP"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function getApiErrorMessage(error, fallback) {
  return (
    error?.response?.data?.message ||
    error?.response?.data?.error ||
    error?.message ||
    fallback
  );
}
