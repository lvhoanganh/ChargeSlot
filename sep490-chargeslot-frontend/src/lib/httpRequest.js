import axios from "axios";
import { showToast } from "@/components/Toast";
export const instance = axios.create({
  baseURL: import.meta.env.VITE_BASE_URL || "https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net/api",
});

instance.interceptors.request.use((config) => {
  const token = localStorage.getItem("accessToken");
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Biến cờ để tránh gọi logout nhiều lần cùng lúc (race condition)
let _isHandlingUnauthorized = false;

instance.interceptors.response.use(
  (response) => {
    return response;
  },
  async (error) => {
    if (error.response && (error.response.status === 401 || error.response.status === 403)) {
      const msg = String(error.response.data?.message || "");

      // Trường hợp bị ban/khoá
      if (msg.includes("Tài khoản bị khoá") || msg.includes("bị khoá") || msg.includes("bị cấm")) {
        if (!_isHandlingUnauthorized) {
          _isHandlingUnauthorized = true;
          localStorage.removeItem("accessToken");
          localStorage.removeItem("userId");
          localStorage.removeItem("role");
          localStorage.removeItem("auth-store");
          showToast.error("Tài khoản bị khoá do vi phạm tiêu chuẩn hệ thống!", 5000);
          // Emit event → SessionGuard sẽ navigate mượt mà thay vì hard redirect
          window.dispatchEvent(new CustomEvent("cs:logout", { detail: { reason: "banned" } }));
          setTimeout(() => { _isHandlingUnauthorized = false; }, 3000);
        }
        return Promise.reject(error);
      }

      // Trường hợp token hết hạn (401 thông thường) — thử refresh nếu có
      if (error.response.status === 401 && !_isHandlingUnauthorized && !error.config._retry) {
        error.config._retry = true; // Đánh dấu đã thử refresh 1 lần

        const refreshToken = localStorage.getItem("refreshToken");
        const accessToken = localStorage.getItem("accessToken");
        const API_BASE = import.meta.env.VITE_BASE_URL || "https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net/api";

        if (refreshToken && accessToken) {
          try {
            const refreshRes = await fetch(`${API_BASE}/auth/refresh-token`, {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify({ accessToken, refreshToken }),
            });
            if (refreshRes.ok) {
              const refreshData = await refreshRes.json();
              localStorage.setItem("accessToken", refreshData.accessToken);
              if (refreshData.refreshToken) localStorage.setItem("refreshToken", refreshData.refreshToken);
              if (refreshData.expiresAtUtc) localStorage.setItem("expiresAtUtc", refreshData.expiresAtUtc);
              // Retry request gốc với token mới
              error.config.headers["Authorization"] = `Bearer ${refreshData.accessToken}`;
              return instance(error.config);
            }
          } catch { /* refresh thất bại → logout bên dưới */ }
        }

        // Không refresh được → emit event logout (React Router navigate, không hard reload)
        _isHandlingUnauthorized = true;
        ["accessToken", "refreshToken", "userId", "role", "expiresAtUtc", "auth-store"].forEach(k =>
          localStorage.removeItem(k)
        );
        window.dispatchEvent(new CustomEvent("cs:logout", { detail: { reason: "expired" } }));
        setTimeout(() => { _isHandlingUnauthorized = false; }, 3000);
      }
    }
    return Promise.reject(error);
  }
);
