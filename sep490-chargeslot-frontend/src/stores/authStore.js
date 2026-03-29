import { instance } from "@/lib/httpRequest";
import { create } from "zustand";

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

  logout: () => {
    localStorage.removeItem("accessToken");
    localStorage.removeItem("refreshToken");
    localStorage.removeItem("userId");
    localStorage.removeItem("role");
    localStorage.removeItem("phoneNumber");
    set({ token: null, refreshToken: null, userId: null, role: null, phoneNumber: null });
  },
}));