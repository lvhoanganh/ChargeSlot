import { useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { showToast } from "@/components/Toast";

/**
 * SessionGuard — lắng nghe event cs:logout được emit từ api.js / httpRequest.js.
 *
 * Thay thế hard redirect (window.location.href = "/login") bằng React Router navigate,
 * giúp tránh reload toàn bộ trang gây ra hiện tượng nháy/flash màn hình khi token hết hạn.
 *
 * Mount component này 1 lần ở cấp App (bên trong BrowserRouter).
 */
export default function SessionGuard() {
  const navigate = useNavigate();
  // Dùng ref để tránh lắng nghe nhiều lần khi StrictMode double-mount
  const handledRef = useRef(false);

  useEffect(() => {
    function handleLogout(e) {
      // Tránh xử lý nhiều lần trong 1 chu kỳ
      if (handledRef.current) return;
      handledRef.current = true;

      const reason = e?.detail?.reason;

      // Nếu hiện tại đang ở /login rồi thì không navigate nữa
      if (window.location.pathname === "/login") {
        handledRef.current = false;
        return;
      }

      if (reason === "banned") {
        showToast.error("Tài khoản bị khoá do vi phạm tiêu chuẩn hệ thống!", 5000);
        navigate("/login?banned=true", { replace: true });
      } else {
        // expired hoặc mặc định
        showToast.warning("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.", 4000);
        navigate("/login", { replace: true });
      }

      // Reset sau 3s để cho phép emit tiếp nếu cần
      setTimeout(() => {
        handledRef.current = false;
      }, 3000);
    }

    window.addEventListener("cs:logout", handleLogout);
    return () => window.removeEventListener("cs:logout", handleLogout);
  }, [navigate]);

  // Không render gì cả — chỉ là invisible event listener
  return null;
}
