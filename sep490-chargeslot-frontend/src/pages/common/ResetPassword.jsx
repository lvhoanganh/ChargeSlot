import { instance } from "@/lib/httpRequest";
import { resetPasswordSchema } from "@/schemas/resetPasswordSchema";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { useLocation, useNavigate } from "react-router-dom";
import ChargeSlotLogo from "@/components/ChargeSlotLogo";

const FORGOT_PHONE_KEY = "forgotPasswordPhoneNumber";

export default function ResetPassword() {
  const navigate = useNavigate();
  const location = useLocation();

  const role = localStorage.getItem("role") || "";
  const backPath = getBackPathByRole(role);

  const initialPhone = useMemo(() => {
    return (
      location.state?.phoneNumber ||
      localStorage.getItem(FORGOT_PHONE_KEY) ||
      localStorage.getItem("phoneNumber") ||
      ""
    );
  }, [location.state?.phoneNumber]);

  const [apiError, setApiError] = useState("");

  const form = useForm({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: {
      phoneNumber: initialPhone,
      newPassword: "",
      confirmPassword: "",
    },
    mode: "onTouched",
  });

  const resetPasswordMutation = useMutation({
    mutationFn: async ({ phoneNumber, newPassword }) => {
      const res = await instance.post(
        `${import.meta.env.VITE_BASE_URL}/Auth/reset-password`,
        { phoneNumber, newPassword },
      );
      return res.data;
    },
    onSuccess: () => {
      localStorage.removeItem(FORGOT_PHONE_KEY);
      if (role) navigate(backPath, { replace: true });
      else navigate("/login", { replace: true });
    },
    onError: (error) => {
      setApiError(
        getApiErrorMessage(
          error,
          "Không thể đặt lại mật khẩu. Vui lòng thử lại.",
        ),
      );
    },
  });

  const onSubmit = (values) => {
    setApiError("");
    resetPasswordMutation.mutate({
      phoneNumber: values.phoneNumber,
      newPassword: values.newPassword,
    });
  };

  return (
    <div className="cs-auth-wrapper">
      <div className="cs-auth-left">
        <div className="cs-auth-left__content">
          <div className="cs-animate-fadeInUp" style={{ marginBottom: 24 }}>
            <ChargeSlotLogo size={56} dark />
          </div>
          <h1 className="cs-auth-left__title cs-animate-fadeInUp-delay-1">
            Đặt mật khẩu mới
          </h1>
          <p className="cs-auth-left__desc cs-animate-fadeInUp-delay-2">
            Tạo mật khẩu mới an toàn cho tài khoản của bạn. Đảm bảo mật khẩu đủ mạnh để bảo vệ tài khoản.
          </p>
          <div className="cs-auth-left__features cs-animate-fadeInUp-delay-3">
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">🔐</span>
              <span>Sử dụng ít nhất 6 ký tự</span>
            </div>
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">🛡️</span>
              <span>Kết hợp chữ và số</span>
            </div>
          </div>
        </div>
      </div>

      <div className="cs-auth-right">
        <form className="cs-auth-form" onSubmit={form.handleSubmit(onSubmit)}>
          <div className="cs-auth-form__logo">
            <ChargeSlotLogo size={38} showText />
          </div>
          <h2 className="cs-auth-form__title">Đặt lại mật khẩu</h2>
          <p className="cs-auth-form__subtitle">
            Nhập mật khẩu mới cho tài khoản của bạn.
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

          <div className="cs-auth-input-group">
            <label>Mật khẩu mới</label>
            <input
              type="password"
              className="cs-auth-input"
              placeholder="Nhập mật khẩu mới..."
              {...form.register("newPassword")}
            />
            {!!form.formState.errors.newPassword?.message && (
              <p className="text-red-500 text-sm mt-1.5">
                {form.formState.errors.newPassword.message}
              </p>
            )}
          </div>

          <div className="cs-auth-input-group">
            <label>Xác nhận mật khẩu mới</label>
            <input
              type="password"
              className="cs-auth-input"
              placeholder="Nhập lại mật khẩu mới..."
              {...form.register("confirmPassword")}
            />
            {!!form.formState.errors.confirmPassword?.message && (
              <p className="text-red-500 text-sm mt-1.5">
                {form.formState.errors.confirmPassword.message}
              </p>
            )}
          </div>

          <div style={{ display: "flex", gap: 12, marginTop: 4 }}>
            <button
              type="button"
              onClick={() => navigate(role ? backPath : "/forgotPassword")}
              disabled={resetPasswordMutation.isPending}
              style={{
                flex: 1,
                height: 48,
                border: "1.5px solid #e5e7eb",
                borderRadius: 12,
                background: "white",
                color: "#374151",
                fontWeight: 600,
                fontSize: 15,
                cursor: "pointer",
                transition: "all 0.2s",
              }}
            >
              Huỷ
            </button>
            <button
              type="submit"
              className="cs-auth-submit"
              style={{ flex: 1, marginTop: 0 }}
              disabled={resetPasswordMutation.isPending}
            >
              {resetPasswordMutation.isPending ? "Đang cập nhật..." : "Cập nhật"}
            </button>
          </div>
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
