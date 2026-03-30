import { instance } from "@/lib/httpRequest";
import { create } from "zustand";

const API_BASE = import.meta.env.VITE_BASE_URL || "https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net/api";

export const useAuthStore = create((set) => ({
  token: localStorage.getItem("accessToken"),
  refreshToken: localStorage.getItem("refreshToken"),
  userId: localStorage.getItem("userId"),
  role: localStorage.getItem("role"),
  phoneNumber: localStorage.getItem("phoneNumber"),
  setPhoneNumber: (phone) => {
    localStorage.setItem("phoneNumber", phone || "");
    set({ phoneNumber: phone });
  },
  login: async (phoneNumber, password) => {
    try {
      const res = await instance.post("/auth/login", {
        phoneNumber,
        password,
      });
      if (res.status !== 200) {
        throw new Error("Login failed");
      }
      const data = res.data;

      localStorage.setItem("accessToken", data.accessToken);
      localStorage.setItem("refreshToken", data.refreshToken || "");
      localStorage.setItem("userId", data.userId);
      localStorage.setItem("role", data.role);
      localStorage.setItem("phoneNumber", data.phoneNumber || phoneNumber || "");
      set({
        token: data.accessToken,
        refreshToken: data.refreshToken || null,
        userId: data.userId,
        role: data.role,
        phoneNumber: data.phoneNumber || phoneNumber || null,
      });

      return data;
    } catch (error) {
      console.error("Login failed:", error);
      const rawMessage =
        error?.response?.data?.message ||
        error?.response?.data?.error ||
        "Đăng nhập thất bại. Vui lòng kiểm tra số điện thoại và mật khẩu.";

      if (rawMessage === "Invalid phone number or password.") {
        throw "Số điện thoại hoặc mật khẩu không đúng.";
      }

      throw rawMessage;
    }
  },

  logout: async () => {
    // Revoke token trên BE để vô hiệu hoá ngay lập tức (Immediate Invalidation)
    try {
      const token = localStorage.getItem("accessToken");
      const refreshToken = localStorage.getItem("refreshToken");
      if (token) {
        await fetch(`${API_BASE}/auth/revoke-token`, {
          method: "POST",
          headers: { "Content-Type": "application/json", "Authorization": `Bearer ${token}` },
          body: JSON.stringify({ refreshToken: refreshToken || "" }),
        }).catch(() => {}); // Bỏ qua lỗi mạng — vẫn logout local
      }
    } catch { /* silent */ }
    localStorage.removeItem("accessToken");
    localStorage.removeItem("refreshToken");
    localStorage.removeItem("userId");
    localStorage.removeItem("role");
    localStorage.removeItem("phoneNumber");
    localStorage.removeItem("expiresAtUtc");
    Object.keys(localStorage).forEach(k => {
      if (k.startsWith("activeChargingBooking_") || k === "activeChargingBookingId") {
        localStorage.removeItem(k);
      }
    });
    set({ token: null, refreshToken: null, userId: null, role: null, phoneNumber: null });
  },

  /** Refresh access token silently dùng refreshToken */
  refreshAccessToken: async () => {
    const refreshToken = localStorage.getItem("refreshToken");
    const accessToken = localStorage.getItem("accessToken");
    if (!refreshToken || !accessToken) throw new Error("No tokens");
    const res = await fetch(`${API_BASE}/auth/refresh-token`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ accessToken, refreshToken }),
    });
    if (!res.ok) throw new Error("Refresh failed");
    const data = await res.json();
    localStorage.setItem("accessToken", data.accessToken);
    if (data.refreshToken) localStorage.setItem("refreshToken", data.refreshToken);
    if (data.expiresAtUtc) localStorage.setItem("expiresAtUtc", data.expiresAtUtc);
    set({ token: data.accessToken, refreshToken: data.refreshToken || refreshToken });
    return data.accessToken;
  },
}));