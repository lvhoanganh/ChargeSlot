import { instance } from "@/lib/httpRequest";
import { forgotPasswordSchema } from "@/schemas/forgotPasswordSchema";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router-dom";
import ChargeSlotLogo from "@/components/ChargeSlotLogo";
import { showToast } from "@/components/Toast";

const FORGOT_PHONE_KEY = "forgotPasswordPhoneNumber";

export default function ForgotPassword() {
  const navigate = useNavigate();
  const role = localStorage.getItem("role") || "";
  const backPath = getBackPathByRole(role);
  const [apiError, setApiError] = useState("");

  const form = useForm({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: {
      phoneNumber: localStorage.getItem(FORGOT_PHONE_KEY) || "",
    },
    mode: "onTouched",
  });

  const sendOtpMutation = useMutation({
    mutationFn: async (phoneNumber) => {
      const res = await instance.post(
        `${import.meta.env.VITE_BASE_URL}/Auth/forgot-password/send-otp`,
        { phoneNumber },
      );
      return res.data;
    },
    onSuccess: (_, phoneNumber) => {
      localStorage.setItem(FORGOT_PHONE_KEY, phoneNumber);
      navigate("/verifyOtp", {
        state: { purpose: "forgot", phoneNumber },
      });
    },
    onError: (error) => {
      const msg = error?.response?.data?.message || "";
      if (msg.toLowerCase().includes("not exist")) {
        showToast.error("Số điện thoại không tồn tại.");
      } else {
        showToast.error(msg || "Gửi OTP thất bại. Vui lòng thử lại.");
      }
    },
  });

  const onSubmit = (values) => {
    setApiError("");
    sendOtpMutation.mutate(values.phoneNumber);
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
            Khôi phục mật khẩu
          </h1>
          <p className="cs-auth-left__desc cs-animate-fadeInUp-delay-2">
            Đừng lo lắng! Nhập số điện thoại đã đăng ký và chúng tôi sẽ gửi mã OTP để bạn đặt lại mật khẩu.
          </p>
          <div className="cs-auth-left__features cs-animate-fadeInUp-delay-3">
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">📱</span>
              <span>Nhập số điện thoại đã đăng ký</span>
            </div>
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">🔑</span>
              <span>Nhận mã OTP xác thực</span>
            </div>
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">✅</span>
              <span>Đặt mật khẩu mới an toàn</span>
            </div>
          </div>
        </div>
      </div>

      {/* Right Form Panel */}
      <div className="cs-auth-right">
        <form className="cs-auth-form" onSubmit={form.handleSubmit(onSubmit)}>
          <div className="cs-auth-form__logo">
            <ChargeSlotLogo size={38} showText />
          </div>
          <h2 className="cs-auth-form__title">Quên mật khẩu</h2>
          <p className="cs-auth-form__subtitle">
            Nhập số điện thoại để nhận mã OTP khôi phục.
          </p>

          {!!apiError && (
            <div className="cs-auth-error">{apiError}</div>
          )}

          <div className="cs-auth-input-group">
            <label>Số điện thoại</label>
            <input
              className="cs-auth-input"
              placeholder="090xxxxxxx"
              {...form.register("phoneNumber")}
            />
            {!!form.formState.errors.phoneNumber?.message && (
              <p className="text-red-500 text-sm mt-1.5">
                {form.formState.errors.phoneNumber.message}
              </p>
            )}
          </div>

          <button
            type="submit"
            className="cs-auth-submit"
            disabled={sendOtpMutation.isPending}
          >
            {sendOtpMutation.isPending ? "Đang gửi..." : "Gửi OTP"}
          </button>

          <p style={{ textAlign: "center", marginTop: 24, fontSize: 14, color: "#64748b" }}>
            {role ? (
              <button
                type="button"
                className="cs-auth-link"
                style={{ background: "none", border: "none", cursor: "pointer" }}
                onClick={() => navigate(backPath)}
              >
                Quay lại hồ sơ
              </button>
            ) : (
              <Link to="/login" className="cs-auth-link">
                Quay lại đăng nhập
              </Link>
            )}
          </p>
        </form>
      </div>
    </div>
  );
}

function getBackPathByRole(role) {
  const r = String(role || "").trim().toLowerCase();
  if (r === "driver") return "/driver/driver-profile";
  if (r === "owner") return "/owner/owner-profile";
  return "/";
}

function getApiErrorMessage(error, fallback) {
  return (
    error?.response?.data?.message ||
    error?.response?.data?.error ||
    error?.message ||
    fallback
  );
}
