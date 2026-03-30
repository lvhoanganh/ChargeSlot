import { useState, useEffect, useRef } from "react";
import { Link, useNavigate } from "react-router-dom";
import { authApi } from "@/services/api";
import { showToast } from "@/components/Toast";
import ChargeSlotLogo from "@/components/ChargeSlotLogo";

import { RecaptchaVerifier, signInWithPhoneNumber } from "firebase/auth";
import { auth } from "@/firebase";

export default function ForgotPassword() {
  const navigate = useNavigate();
  const role = localStorage.getItem("role") || "";

  // Field states
  const [phoneNumber, setPhoneNumber] = useState("");
  const [formattedPhone, setFormattedPhone] = useState(""); // +84xxxxxxxxx
  const [otp, setOtp] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  
  // Flow states
  const [step, setStep] = useState(1); // 1: PHONE, 2: OTP, 3: NEW PASSWORD
  const [confirmationResult, setConfirmationResult] = useState(null);
  const [idToken, setIdToken] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  // OTP countdown
  const OTP_TTL = 60; // seconds
  const [otpCountdown, setOtpCountdown] = useState(OTP_TTL);
  const [resendLoading, setResendLoading] = useState(false);
  const countdownRef = useRef(null);

  // Khởi tạo hoặc tái tạo recaptchaVerifier
  const initRecaptcha = () => {
    if (window.recaptchaVerifier) {
      try { window.recaptchaVerifier.clear(); } catch (_) {}
      window.recaptchaVerifier = null;
    }
    try {
      window.recaptchaVerifier = new RecaptchaVerifier(auth, 'recaptcha-container', {
        'size': 'invisible',
      });
    } catch (e) {
      console.error("Lỗi khởi tạo RecaptchaVerifier", e);
    }
  };

  useEffect(() => {
    initRecaptcha();

    // Cleanup chống lỗi "removed: 0" khi Unmount
    return () => {
      if (window.recaptchaVerifier) {
        try { window.recaptchaVerifier.clear(); } catch (_) {}
        window.recaptchaVerifier = null;
      }
      clearInterval(countdownRef.current);
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Hàm gửi lại OTP
  const handleResendOtp = async () => {
    if (otpCountdown > 0 || resendLoading) return;
    setResendLoading(true);
    try {
      initRecaptcha();
      // Chờ một chút để recaptcha sẵn sàng
      await new Promise(r => setTimeout(r, 300));
      const appVerifier = window.recaptchaVerifier;
      const result = await signInWithPhoneNumber(auth, formattedPhone, appVerifier);
      setConfirmationResult(result);
      setOtp("");
      // Reset đếm ngược
      setOtpCountdown(OTP_TTL);
      clearInterval(countdownRef.current);
      countdownRef.current = setInterval(() => {
        setOtpCountdown(prev => {
          if (prev <= 1) { clearInterval(countdownRef.current); return 0; }
          return prev - 1;
        });
      }, 1000);
      showToast.success("Đã gửi lại mã OTP!");
    } catch (error) {
      console.error("Lỗi gửi lại OTP:", error);
      initRecaptcha();
      if (error.code === 'auth/too-many-requests') {
        showToast.error("Quá nhiều lần thử. Vui lòng chờ 15-30 phút.");
      } else {
        showToast.error("Không thể gửi lại OTP. Thử lại sau.");
      }
    } finally {
      setResendLoading(false);
    }
  };

  const handleSendOtp = async (e) => {
    if (e) e.preventDefault();
    if (!phoneNumber) {
      showToast.error("Vui lòng nhập số điện thoại");
      return;
    }

    // Format số điện thoại → +84 (lưu state để dùng cho backend)
    let phone = phoneNumber.trim();
    if (phone.startsWith("0")) {
      phone = "+84" + phone.slice(1);
    } else if (!phone.startsWith("+")) {
      phone = "+84" + phone;
    }
    setFormattedPhone(phone);

    setIsLoading(true);
    try {
      const appVerifier = window.recaptchaVerifier;
      const result = await signInWithPhoneNumber(auth, phone, appVerifier);
      setConfirmationResult(result);
      setStep(2);
      // Khởi động đếm ngược OTP
      setOtpCountdown(OTP_TTL);
      clearInterval(countdownRef.current);
      countdownRef.current = setInterval(() => {
        setOtpCountdown(prev => {
          if (prev <= 1) { clearInterval(countdownRef.current); return 0; }
          return prev - 1;
        });
      }, 1000);
      showToast.success("Mã OTP đã được gửi đến điện thoại (Bởi Firebase)!");
    } catch (error) {
      console.error("Lỗi gửi OTP Firebase:", error);
      // Reset reCAPTCHA sau khi lỗi để cho phép gửi lại
      initRecaptcha();
      if (error.code === 'auth/invalid-phone-number') {
        showToast.error("Số điện thoại không hợp lệ.");
      } else if (error.code === 'auth/billing-not-enabled') {
        showToast.error("Lỗi Google Firebase: Chưa bật thanh toán hoặc bị khóa số.");
      } else if (error.code === 'auth/too-many-requests') {
        showToast.error("Bạn thao tác quá nhiều lần! Vui lòng chờ 15-30 phút rồi thử lại.");
      } else {
        showToast.error("Không thể gửi OTP. Vui lòng thử lại sau.");
      }
    } finally {
      setIsLoading(false);
    }
  };

  const handleVerifyOtp = async (e) => {
    e.preventDefault();
    if (!otp || otp.length < 6) {
      showToast.error("Vui lòng nhập đủ 6 số OTP");
      return;
    }

    setIsLoading(true);
    try {
      // Xác nhận OTP với Firebase
      const userCredential = await confirmationResult.confirm(otp);
      
      // Lấy ID Token và giữ lại để dùng cho step 3
      const token = await userCredential.user.getIdToken();
      setIdToken(token);
      
      setStep(3);
      showToast.success("Xác thực SĐT thành công!");
    } catch (error) {
      console.error("Lỗi xác thực OTP:", error);
      if (error.code === 'auth/invalid-verification-code') {
        showToast.error("Mã OTP không đúng.");
      } else {
        showToast.error("Xác nhận OTP thất bại. Thử lại.");
      }
    } finally {
      setIsLoading(false);
    }
  };

  const handleCompleteReset = async (e) => {
    e.preventDefault();
    if (!newPassword || !confirmPassword) {
      showToast.error("Vui lòng nhập đầy đủ thông tin mật khẩu");
      return;
    }
    if (newPassword.length < 6) {
      showToast.error("Mật khẩu phải có ít nhất 6 ký tự");
      return;
    }
    if (newPassword !== confirmPassword) {
      showToast.error("Mật khẩu xác nhận không khớp");
      return;
    }

    setIsLoading(true);
    try {
      await authApi.resetPassword({
        phoneNumber: formattedPhone, // Dùng số +84 đã format
        newPassword,
        firebaseIdToken: idToken,
      });
      
      showToast.success("Đặt lại mật khẩu thành công!");
      if (role) {
        navigate(getBackPathByRole(role));
      } else {
        navigate("/login");
      }
    } catch (error) {
      console.error("Lỗi đổi mật khẩu:", error);
      showToast.error(error.message || "Đổi mật khẩu thất bại. Vui lòng thử lại.");
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="cs-auth-wrapper">
      <div className="cs-auth-left">
        <div className="cs-auth-left__content">
          <div className="cs-animate-fadeInUp" style={{ marginBottom: 24 }}>
            <ChargeSlotLogo size={56} dark />
          </div>
          <h1 className="cs-auth-left__title cs-animate-fadeInUp-delay-1">
            Khôi phục mật khẩu<br />nhanh gọn, an toàn
          </h1>
          <p className="cs-auth-left__desc cs-animate-fadeInUp-delay-2">
            Hệ thống xác thực chủ sở hữu qua hệ sinh thái Google Firebase. Mật khẩu của bạn được bảo mật tuyệt đối!
          </p>
          <div className="cs-auth-left__features cs-animate-fadeInUp-delay-3">
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">📱</span>
              <span>Bước 1: Nhập Số điện thoại đã đăng ký</span>
            </div>
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">🔑</span>
              <span>Bước 2: Hệ thống Firebase check SMS tự động</span>
            </div>
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">✅</span>
              <span>Bước 3: Đặt lại mật khẩu mới</span>
            </div>
          </div>
        </div>
      </div>

      <div className="cs-auth-right">
        {/* THẺ FIREBASE RECAPTCHA: Bắt buộc để ngoài cùng rễ để vĩnh viễn không bị Unmount => Chống lỗi removed 0 */}
        <div id="recaptcha-container"></div>

        {step === 1 && (
          <form className="cs-auth-form" onSubmit={handleSendOtp}>
            <div className="cs-auth-form__logo">
              <ChargeSlotLogo size={38} showText />
            </div>
            <h2 className="cs-auth-form__title">Quên mật khẩu</h2>
            <p className="cs-auth-form__subtitle">
              Nhập số điện thoại đã đăng ký để bắt đầu khôi phục mật khẩu.
            </p>

            <div className="cs-auth-input-group">
              <label>Số điện thoại</label>
              <input
                className="cs-auth-input"
                placeholder="Ví dụ: 090xxxxxxx"
                value={phoneNumber}
                onChange={(e) => setPhoneNumber(e.target.value)}
                disabled={isLoading}
              />
            </div>

            <button type="submit" className="cs-auth-submit" disabled={isLoading || !phoneNumber}>
              {isLoading ? "Đang xử lý..." : "Nhận mã OTP"}
            </button>

            <p style={{ textAlign: "center", marginTop: 24, fontSize: 14, color: "#64748b" }}>
              {role ? (
                <button
                  type="button"
                  className="cs-auth-link"
                  style={{ background: "none", border: "none", cursor: "pointer" }}
                  onClick={() => navigate(getBackPathByRole(role))}
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
        )}

        {step === 2 && (
          <form className="cs-auth-form" onSubmit={handleVerifyOtp}>
            <div className="cs-auth-form__logo">
              <ChargeSlotLogo size={38} showText />
            </div>
            <h2 className="cs-auth-form__title">Xác minh OTP</h2>
            <p className="cs-auth-form__subtitle">
              Mã bảo mật Firebase đã gửi tới: <strong style={{color:"#f97316"}}>{phoneNumber}</strong>
            </p>

            <div className="cs-auth-input-group">
              <label>Mã OTP</label>
              <input
                className="cs-auth-input"
                placeholder="Nhập 6 số"
                value={otp}
                onChange={(e) => setOtp(e.target.value)}
                maxLength={6}
                disabled={isLoading}
                style={{ letterSpacing: "4px", fontSize: "16px", textAlign: "center" }}
              />
            </div>

            <button type="submit" className="cs-auth-submit" disabled={isLoading || otp.length < 6}>
              {isLoading ? "Đang xác thực..." : "Tiếp tục"}
            </button>

            {/* Countdown + Resend */}
            <div style={{ textAlign: "center", marginTop: 20, display: "flex", flexDirection: "column", gap: 10, alignItems: "center" }}>

              {/* Vòng tròn đếm ngược */}
              <div className="cs-otp-countdown">
                <div className={`cs-otp-countdown__circle${otpCountdown === 0 ? ' cs-otp-countdown__circle--expired' : ''}`}>
                  <svg className="cs-otp-countdown__svg" viewBox="0 0 36 36">
                    <circle className="cs-otp-countdown__track" cx="18" cy="18" r="16" />
                    <circle
                      className="cs-otp-countdown__progress"
                      cx="18" cy="18" r="16"
                      strokeDasharray="100.53"
                      strokeDashoffset={100.53 - (otpCountdown / OTP_TTL) * 100.53}
                    />
                  </svg>
                  <span className="cs-otp-countdown__time">{otpCountdown}s</span>
                </div>
                <span className="cs-otp-countdown__label">
                  {otpCountdown > 0 ? "Mã OTP còn hiệu lực" : "Mã OTP đã hết hạn"}
                </span>
              </div>

              {/* Nút gửi lại */}
              <button
                type="button"
                className="cs-otp-resend-btn"
                disabled={otpCountdown > 0 || resendLoading}
                onClick={handleResendOtp}
                style={{ opacity: otpCountdown > 0 ? 0.45 : 1 }}
              >
                {resendLoading ? "Đang gửi lại..." : "🔄 Gửi lại mã OTP"}
              </button>

              {/* Chỉnh sửa SĐT */}
              <button
                type="button"
                style={{ background: 'none', border: 'none', padding: 0, cursor: 'pointer', color: "#94a3b8", fontSize: 13 }}
                onClick={() => {
                  clearInterval(countdownRef.current);
                  setStep(1);
                  setOtp("");
                  initRecaptcha();
                }}
              >
                Chỉnh sửa số điện thoại
              </button>
            </div>
          </form>
        )}

        {step === 3 && (
          <form className="cs-auth-form" onSubmit={handleCompleteReset}>
            <div className="cs-auth-form__logo">
              <ChargeSlotLogo size={38} showText />
            </div>
            <h2 className="cs-auth-form__title">Đặt lại mật khẩu</h2>
            <p className="cs-auth-form__subtitle">
              Tạo mật khẩu mới an toàn cho tài khoản của bạn.
            </p>

            <div className="cs-auth-input-group">
              <label>Mật khẩu mới</label>
              <input
                type="password"
                className="cs-auth-input"
                placeholder="Ít nhất 6 ký tự"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                disabled={isLoading}
                required
                minLength={6}
              />
            </div>

            <div className="cs-auth-input-group">
              <label>Xác nhận mật khẩu mới</label>
              <input
                type="password"
                className="cs-auth-input"
                placeholder="Nhập lại mật khẩu mới"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                disabled={isLoading}
                required
                minLength={6}
              />
            </div>

            <button type="submit" className="cs-auth-submit" disabled={isLoading || !newPassword || !confirmPassword}>
              {isLoading ? "Đang xử lý..." : "Cập nhật mật khẩu"}
            </button>
          </form>
        )}
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
