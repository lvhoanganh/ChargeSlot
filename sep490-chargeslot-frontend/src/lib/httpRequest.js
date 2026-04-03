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

instance.interceptors.response.use(
  (response) => {
    return response;
  },
  async (error) => {
    if (error.response && (error.response.status === 401 || error.response.status === 403)) {
      const msg = String(error.response.data?.message || "");
      if (msg.includes("Tài khoản bị khoá") || msg.includes("bị khoá") || msg.includes("bị cấm")) {
        localStorage.removeItem("accessToken");
        localStorage.removeItem("userId");
        localStorage.removeItem("role");
        localStorage.removeItem("auth-store"); // clear Zustand persist
        showToast.error("Tài khoản bị khoá do vi phạm tiêu chuẩn hệ thống!", 5000);
        window.dispatchEvent(new Event("cs:logout"));
        setTimeout(() => {
          window.location.href = '/login?banned=true';
        }, 1500);
        return Promise.reject(error);
      }
    }
    return Promise.reject(error);
  }
);
