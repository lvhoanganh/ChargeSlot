import { Button } from "@/components/ui/button";
import { useLocation, useNavigate } from "react-router-dom";
import { useState, useEffect } from "react";

export default function VerifyOtp() {
  const navigate = useNavigate();
  const location = useLocation();

  const purpose = location.state?.purpose || "register";
  const [success, setSuccess] = useState(false);

  useEffect(() => {
    if (!success) return;

    let timer;

    if (purpose === "register") {
      timer = setTimeout(() => {
        navigate("/setPassword", { replace: true });
      }, 1500);
    }

    if (purpose === "forgot") {
      timer = setTimeout(() => {
        navigate("/setPassword", { replace: true });
      }, 1500);
    }

    return () => clearTimeout(timer);
  }, [success, purpose, navigate]);

  const getTitle = () => {
    switch (purpose) {
      case "forgot":
        return "Xác thực OTP – Quên mật khẩu";
      default:
        return "Xác thực OTP – Đăng ký";
    }
  };

  const getSuccessContent = () => {
    switch (purpose) {
      case "forgot":
        return (
          <>
            <p className="text-lg font-semibold mb-2">
              Xác thực thành công
            </p>
            <p className="text-sm text-gray-600">
              Vui lòng đặt lại mật khẩu mới
            </p>
          </>
        );
      default:
        return (
          <>
            <p className="text-lg font-semibold mb-2">
              Xác thực thành công
            </p>
            <p className="text-sm text-gray-600">
              Vui lòng thiết lập mật khẩu cho tài khoản
            </p>
          </>
        );
    }
  };

  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center px-4">
      <form
        className="max-w-[500px] w-full bg-white rounded-xl shadow-md"
        onSubmit={(e) => {
          e.preventDefault();
          setSuccess(true);
        }}
      >
        <div className="p-8">
          <h1 className="text-xl font-bold mb-4">{getTitle()}</h1>

          {!success ? (
            <>
              <div className="flex flex-col mb-5">
                <label className="mb-1">Mã OTP</label>
                <input
                  className="h-11 px-4 border rounded-md"
                  placeholder="123456"
                />
              </div>

              <Button className="w-full h-12 bg-orange-500 hover:bg-orange-600 mb-4">
                Xác nhận OTP
              </Button>

              <button
                type="button"
                className="text-blue-500 w-full text-sm"
              >
                Gửi lại OTP
              </button>
            </>
          ) : (
            <div className="text-center py-6">
              <div className="text-4xl mb-3">✅</div>
              {getSuccessContent()}
            </div>
          )}
        </div>
      </form>
    </div>
  );
}
