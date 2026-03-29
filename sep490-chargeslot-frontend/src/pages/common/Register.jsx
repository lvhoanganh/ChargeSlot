import { useState, useEffect } from "react";
import { Link, useNavigate } from "react-router-dom";
import { authApi } from "@/services/api";
import { showToast } from "@/components/Toast";
import ChargeSlotLogo from "@/components/ChargeSlotLogo";

import { RecaptchaVerifier, signInWithPhoneNumber } from "firebase/auth";
import { auth } from "@/firebase";

export default function Register() {
  // Field states
  const [phoneNumber, setPhoneNumber] = useState("");
  const [formattedPhone, setFormattedPhone] = useState(""); // +84xxxxxxxxx
  const [otp, setOtp] = useState("");
  const [fullName, setFullName] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState("Driver");
  
  // Flow states
  const [step, setStep] = useState(1); // 1: PHONE, 2: OTP, 3: INFO
  const [confirmationResult, setConfirmationResult] = useState(null);
  const [idToken, setIdToken] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  const navigate = useNavigate();

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
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleSendOtp = async (e) => {
    e.preventDefault();
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
      
      // Chuyển sang bước 3: Nhập thông tin bổ sung
      setStep(3);
      showToast.success("Xác thực SĐT thành công!");
    } catch (error) {
      console.error("Lỗi xác nhận OTP:", error);
      if (error.code === 'auth/invalid-verification-code') {
        showToast.error("Mã OTP không đúng.");
      } else {
        showToast.error("Xác nhận OTP thất bại. Thử lại.");
      }
    } finally {
      setIsLoading(false);
    }
  };

  const handleCompleteRegister = async (e) => {
    e.preventDefault();
    if (!fullName || !password) {
      showToast.error("Vui lòng nhập đầy đủ thông tin");
      return;
    }
    if (password.length < 6) {
      showToast.error("Mật khẩu phải có ít nhất 6 ký tự");
      return;
    }

    setIsLoading(true);
    try {
      // Gửi toàn bộ data lên backend tạo tài khoản mới
      // Dùng formattedPhone (+84...) thay vì phoneNumber gốc
      await authApi.register({
        phoneNumber: formattedPhone,
        fullName,
        password,
        role,
        firebaseIdToken: idToken,
      });
      
      showToast.success("Đăng ký thành công! Vui lòng đăng nhập.");
      navigate("/login");
    } catch (error) {
      console.error("Lỗi đăng ký backend:", error);
      showToast.error(typeof error === "string" ? error : (error.message || "Đăng ký thất bại. Vui lòng thử lại."));
    } finally {
      setIsLoading(false);
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
            Tạo tài khoản<br />chỉ 3 bước đơn giản
          </h1>
          <p className="cs-auth-left__desc cs-animate-fadeInUp-delay-2">
            Nhập số điện thoại → Xác thực mã OTP → Thiết lập mật khẩu và vai trò. Giao dịch an toàn và bảo mật!
          </p>
          <div className="cs-auth-left__features cs-animate-fadeInUp-delay-3">
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">📱</span>
              <span>Bước 1: Nhập Số điện thoại nhận OTP Firebase</span>
            </div>
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">🔑</span>
              <span>Bước 2: Hệ thống gửi mã OTP qua SMS thật</span>
            </div>
            <div className="cs-auth-left__feature">
              <span className="cs-auth-left__feature-icon">🚗</span>
              <span>Bước 3: Chọn làm Chủ Trạm hoặc Tài Xế</span>
            </div>
          </div>
        </div>
      </div>

      {/* Right Form Panel */}
      <div className="cs-auth-right">
        {/* THẺ FIREBASE RECAPTCHA: Bắt buộc để ngoài cùng rễ để vĩnh viễn không bị Unmount => Chống lỗi removed 0 */}
        <div id="recaptcha-container"></div>

        {step === 1 && (
          <form className="cs-auth-form" onSubmit={handleSendOtp}>
            <div className="cs-auth-form__logo">
              <ChargeSlotLogo size={38} showText />
            </div>
            <h2 className="cs-auth-form__title">Đăng ký tài khoản</h2>
            <p className="cs-auth-form__subtitle">
              Nhập số điện thoại để bắt đầu nhận mã OTP Firebase.
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
              Đã có tài khoản?{" "}
              <Link to="/login" className="cs-auth-link">
                Đăng nhập
              </Link>
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
              Mã bảo mật đã được gửi tới số: <strong style={{color:"#f97316"}}>{phoneNumber}</strong>
            </p>

            <div className="cs-auth-input-group">
              <label>Mã OTP</label>
              <input
                className="cs-auth-input"
                placeholder="Nhập 6 số từ tin nhắn"
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

            <p style={{ textAlign: "center", marginTop: 24, fontSize: 14 }}>
              <button 
                type="button" 
                className="cs-auth-link" 
                style={{ background: 'none', border: 'none', padding: 0, cursor: 'pointer', color: "#64748b" }}
                onClick={() => {
                  setStep(1);
                  setOtp("");
                  // Reset reCAPTCHA để có thể gửi OTP lại
                  initRecaptcha();
                }}
              >
                 Chỉnh sửa số điện thoại
              </button>
            </p>
          </form>
        )}

        {step === 3 && (
          <form className="cs-auth-form" onSubmit={handleCompleteRegister}>
            <div className="cs-auth-form__logo">
              <ChargeSlotLogo size={38} showText />
            </div>
            <h2 className="cs-auth-form__title">Hoàn tất thủ tục</h2>
            <p className="cs-auth-form__subtitle">
              Xác thực hoàn tất! Vui lòng khởi tạo mật khẩu và vai trò.
            </p>

            <div className="cs-auth-input-group">
              <label>Họ và Tên</label>
              <input
                className="cs-auth-input"
                placeholder="Nhập họ và tên..."
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                disabled={isLoading}
                required
              />
            </div>

            <div className="cs-auth-input-group">
              <label>Mật khẩu</label>
              <input
                type="password"
                className="cs-auth-input"
                placeholder="Ít nhất 6 ký tự"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                disabled={isLoading}
                required
                minLength={6}
              />
            </div>

            <div className="cs-auth-input-group">
              <label>Vai trò</label>
              <select
                className="cs-auth-input"
                value={role}
                onChange={(e) => setRole(e.target.value)}
                disabled={isLoading}
                required
                style={{ cursor: "pointer" }}
              >
                <option value="Driver">🚗 Tài xế (Driver)</option>
                <option value="Owner">🏢 Chủ trạm (Owner)</option>
              </select>
            </div>

            <button type="submit" className="cs-auth-submit" disabled={isLoading || !fullName || !password}>
              {isLoading ? "Đang xử lý..." : "Hoàn tất đăng ký"}
            </button>
          </form>
        )}
      </div>
    </div>
  );
}
