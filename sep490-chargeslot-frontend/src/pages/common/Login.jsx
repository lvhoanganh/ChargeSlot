import { Button } from "@/components/ui/button";
import { Link, useNavigate } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { loginSchema } from "@/schemas/loginSchema";
import { useAuthStore } from "@/stores/authStore";
import { showToast } from "@/components/Toast";
import ChargeSlotLogo from "@/components/ChargeSlotLogo";

export default function Login() {
  const navigate = useNavigate();
  const { login } = useAuthStore();

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm({
    resolver: zodResolver(loginSchema),
  });

  const onSubmit = async (data) => {
    try {
      const result = await login(data.phoneNumber, data.password);
      const role = result?.role;

      if (role === "Admin") {
        navigate("/admin/manage-users");
      } else if (role === "Owner") {
        navigate("/stations");
      } else {
        navigate("/");
      }
    } catch (err) {
      showToast.error(typeof err === "string" ? err : "Đăng nhập thất bại.");
    }
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
            Sạc xe thông minh,<br />mọi lúc mọi nơi
          </h1>
          <p className="cs-auth-left__desc cs-animate-fadeInUp-delay-2">
            Nền tảng đặt lịch sạc xe điện hàng đầu Việt Nam. Tiện lợi, nhanh chóng và tiết kiệm.
          </p>
          <div className="cs-auth-left__features cs-animate-fadeInUp-delay-3">
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">🗺️</span>
              <span>Tìm trạm sạc gần bạn trên bản đồ</span>
            </div>
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">📅</span>
              <span>Đặt lịch sạc trước, không lo hàng chờ</span>
            </div>
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">💰</span>
              <span>Thanh toán tiện lợi qua ví điện tử</span>
            </div>
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">⭐</span>
              <span>Tích điểm thưởng mỗi lần sạc xe</span>
            </div>
          </div>
        </div>
      </div>

      {/* Right Form Panel */}
      <div className="cs-auth-right">
        <form className="cs-auth-form" onSubmit={handleSubmit(onSubmit)}>
          <div className="cs-auth-form__logo">
            <ChargeSlotLogo size={38} showText />
          </div>
          <h2 className="cs-auth-form__title">Đăng nhập</h2>
          <p className="cs-auth-form__subtitle">
            Chào mừng bạn quay trở lại! Vui lòng nhập thông tin đăng nhập.
          </p>

          <div className="cs-auth-input-group">
            <label>Số điện thoại</label>
            <input
              type="text"
              className="cs-auth-input"
              placeholder="Nhập số điện thoại..."
              {...register("phoneNumber")}
            />
            {errors.phoneNumber && (
              <p className="text-red-500 text-sm mt-1.5">
                {errors.phoneNumber.message}
              </p>
            )}
          </div>

          <div className="cs-auth-input-group">
            <label>Mật khẩu</label>
            <input
              type="password"
              className="cs-auth-input"
              placeholder="Nhập mật khẩu..."
              {...register("password")}
            />
            {errors.password && (
              <p className="text-red-500 text-sm mt-1.5">
                {errors.password.message}
              </p>
            )}
          </div>

          <div style={{ textAlign: "right", marginBottom: 20 }}>
            <Link to="/forgotPassword" className="cs-auth-link" style={{ fontSize: 13 }}>
              Quên mật khẩu?
            </Link>
          </div>

          <button type="submit" className="cs-auth-submit" disabled={isSubmitting}>
            {isSubmitting ? "Đang đăng nhập..." : "Đăng nhập"}
          </button>

          <p style={{ textAlign: "center", marginTop: 24, fontSize: 14, color: "#64748b" }}>
            Chưa có tài khoản?{" "}
            <Link to="/register" className="cs-auth-link">
              Đăng ký ngay
            </Link>
          </p>
        </form>
      </div>
    </div>
  );
}
