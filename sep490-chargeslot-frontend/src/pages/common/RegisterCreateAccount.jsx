import { Link, useNavigate } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import { instance } from "@/lib/httpRequest";
import { useAuthStore } from "@/stores/authStore";
import { useState } from "react";
import { showToast } from "@/components/Toast";
import ChargeSlotLogo from "@/components/ChargeSlotLogo";

const register = async ({ phoneNumber, fullName, password, role }) => {
  const res = await instance.post(
    `${import.meta.env.VITE_BASE_URL}/Auth/register`,
    { phoneNumber, fullName, password, role },
  );
  return res.data;
};

export default function RegisterCreateAccount() {
  const [fullName, setFullName] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState("Driver");
  const { phoneNumber } = useAuthStore();
  const navigate = useNavigate();

  const registerMutation = useMutation({
    mutationFn: register,
    onSuccess: () => {
      showToast.success("Đăng ký tài khoản thành công!");
      navigate("/login");
    },
    onError: (error) => {
      console.error("Failed to register:", error);
      showToast.error("Đăng ký tài khoản thất bại. Vui lòng thử lại.");
    },
  });

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!phoneNumber) {
      showToast.warning("Vui lòng quay lại trang đăng ký để nhập số điện thoại.");
      navigate("/register");
      return;
    }
    registerMutation.mutate({ phoneNumber, fullName, password, role });
  };

  return (
    <div className="cs-auth-wrapper">
      <div className="cs-auth-left">
        <div className="cs-auth-left__content">
          <div className="cs-animate-fadeInUp" style={{ marginBottom: 24 }}>
            <ChargeSlotLogo size={56} dark />
          </div>
          <h1 className="cs-auth-left__title cs-animate-fadeInUp-delay-1">
            Hoàn tất đăng ký
          </h1>
          <p className="cs-auth-left__desc cs-animate-fadeInUp-delay-2">
            Chỉ còn một bước nữa! Điền thông tin cá nhân để bắt đầu sử dụng ChargeSlot.
          </p>
          <div className="cs-auth-left__features cs-animate-fadeInUp-delay-3">
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">🚗</span>
              <span>Tài xế — Đặt lịch sạc xe</span>
            </div>
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">🏢</span>
              <span>Chủ trạm — Quản lý trạm sạc</span>
            </div>
          </div>
        </div>
      </div>

      <div className="cs-auth-right">
        <form className="cs-auth-form" onSubmit={handleSubmit}>
          <div className="cs-auth-form__logo">
            <ChargeSlotLogo size={38} showText />
          </div>
          <h2 className="cs-auth-form__title">Tạo tài khoản</h2>
          <p className="cs-auth-form__subtitle">
            Số điện thoại: <strong style={{ color: "#f97316" }}>{phoneNumber}</strong>
          </p>

          <div className="cs-auth-input-group">
            <label>Họ và tên</label>
            <input
              className="cs-auth-input"
              placeholder="Nhập họ và tên"
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              required
            />
          </div>

          <div className="cs-auth-input-group">
            <label>Mật khẩu</label>
            <input
              type="password"
              className="cs-auth-input"
              placeholder="Nhập mật khẩu"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </div>

          <div className="cs-auth-input-group">
            <label>Vai trò</label>
            <select
              className="cs-auth-input"
              value={role}
              onChange={(e) => setRole(e.target.value)}
              required
              style={{ cursor: "pointer" }}
            >
              <option value="Driver">🚗 Tài xế (Driver)</option>
              <option value="Owner">🏢 Chủ trạm (Owner)</option>
            </select>
          </div>

          <button
            type="submit"
            className="cs-auth-submit"
            disabled={registerMutation.isPending}
          >
            {registerMutation.isPending ? "Đang đăng ký..." : "Hoàn tất đăng ký"}
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
