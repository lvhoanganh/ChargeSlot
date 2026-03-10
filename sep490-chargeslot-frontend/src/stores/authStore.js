import { instance } from "@/lib/httpRequest";
import { create } from "zustand";

export const useAuthStore = create((set) => ({
  token: localStorage.getItem("accessToken"),
  refreshToken: localStorage.getItem("refreshToken"),
  userId: null,
  role: null,
  phoneNumber: null,
  setPhoneNumber: (phone) => set({ phoneNumber: phone }),
  login: async (phoneNumber, password) => {
    try {
      const res = await instance.post(
        `${import.meta.env.VITE_BASE_URL}/Auth/login`,
        {
          phoneNumber: phoneNumber,
          password: password,
        },
      );
      if (res.status !== 200) {
        throw new Error("Login failed");
      }
      const data = res.data;
      localStorage.setItem("accessToken", data.accessToken);
      localStorage.setItem("refreshToken", data.refreshToken);
      set({
        token: data.accessToken,
        refreshToken: data.refreshToken,
        userId: data.userId,
        role: data.role,
      });
      localStorage.setItem("accessToken", data.accessToken);
      localStorage.setItem("refreshToken", data.refreshToken);
      localStorage.setItem("userId", data.userId);
      localStorage.setItem("role", data.role);
    } catch (error) {
      console.error("Login failed:", error);
      throw error;
    }
  },
  logout: () => {
    localStorage.removeItem("accessToken");
    localStorage.removeItem("refreshToken");
    localStorage.removeItem("userId");
    localStorage.removeItem("role");
    set({ token: null, refreshToken: null, userId: null, role: null });
  },
}));
