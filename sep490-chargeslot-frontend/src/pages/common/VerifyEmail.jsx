import { useEffect, useState } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { authApi } from "@/services/api";

export default function VerifyEmail() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  const userId = searchParams.get("userId");
  const token = searchParams.get("token");

  const [status, setStatus] = useState("loading"); // loading, success, error
  const [message, setMessage] = useState("");

  useEffect(() => {
    let cancelled = false;

    if (!userId || !token) {
      setStatus("error");
      setMessage("Link xác thực không hợp lệ hoặc thiếu tham số.");
      return;
    }

    // decode token just in case (though fetch will serialize as JSON properly usually)
    authApi
      .verifyEmail(parseInt(userId, 10), token)
      .then(() => {
        if (!cancelled) {
          setStatus("success");
          setMessage("Email của bạn đã được xác thực thành công.");
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setStatus("error");
          setMessage(err.message || "Xác thực thất bại. Vui lòng thử lại.");
        }
      });

    return () => {
      cancelled = true;
    };
  }, [userId, token]);

  return (
    <div className="flex items-center justify-center min-h-[70vh] bg-gray-50 px-4">
      <div className="max-w-md w-full bg-white p-8 rounded-2xl shadow-xl border border-gray-100 flex flex-col items-center text-center">
        {status === "loading" && (
          <>
            <div className="w-16 h-16 border-4 border-orange-200 border-t-orange-500 rounded-full animate-spin mb-6" />
            <h2 className="text-2xl font-bold text-gray-800 mb-2">Đang xác thực...</h2>
            <p className="text-gray-500">Vui lòng chờ trong giây lát.</p>
          </>
        )}

        {status === "success" && (
          <>
            <div className="w-20 h-20 bg-green-100 text-green-500 rounded-full flex items-center justify-center mb-6">
              <svg className="w-10 h-10" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
              </svg>
            </div>
            <h2 className="text-2xl font-bold text-gray-800 mb-2">Xác thực thành công!</h2>
            <p className="text-gray-600 mb-8">{message}</p>
            <button
              onClick={() => navigate("/")}
              className="w-full bg-orange-500 hover:bg-orange-600 text-white font-semibold py-3 px-6 rounded-xl transition-colors shadow-md shadow-orange-500/30"
            >
              Về trang chủ
            </button>
          </>
        )}

        {status === "error" && (
          <>
            <div className="w-20 h-20 bg-red-100 text-red-500 rounded-full flex items-center justify-center mb-6">
              <svg className="w-10 h-10" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </div>
            <h2 className="text-2xl font-bold text-gray-800 mb-2">Xác thực thất bại</h2>
            <p className="text-red-600 font-medium mb-8 bg-red-50 py-3 px-4 rounded-xl w-full">{message}</p>
            <button
              onClick={() => navigate("/")}
              className="w-full bg-gray-100 hover:bg-gray-200 text-gray-800 font-semibold py-3 px-6 rounded-xl transition-colors"
            >
              Về trang chủ
            </button>
          </>
        )}
      </div>
    </div>
  );
}
