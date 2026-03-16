import { instance } from "@/lib/httpRequest";
import { create } from "zustand";

export const useAuthStore = create((set) => ({
  token: localStorage.getItem("accessToken"),
  refreshToken: localStorage.getItem("refreshToken"),
  userId: (() => {
    const v = Number(localStorage.getItem("userId"));
    return Number.isFinite(v) && v > 0 ? v : null;
  })(),
  role: localStorage.getItem("role"),
  phoneNumber: localStorage.getItem("phoneNumber"),
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
      set({
        token: data.accessToken,
        refreshToken: data.refreshToken,
        userId: data.userId,
        role: data.role,
        phoneNumber: data.phoneNumber || phoneNumber,
      });

      localStorage.setItem("accessToken", data.accessToken);
      localStorage.setItem("userId", data.userId);
      localStorage.setItem("role", data.role);
      localStorage.setItem("phoneNumber", data.phoneNumber || phoneNumber || "");
      if (data.refreshToken) {
        localStorage.setItem("refreshToken", data.refreshToken);
      } else {
        localStorage.removeItem("refreshToken");
      }

      // Restore fullName for this phone if we have it from registration.
      try {
        const key = "userInfoByPhone";
        const phone = data.phoneNumber || phoneNumber || "";
        const map = JSON.parse(localStorage.getItem(key) || "{}");
        const known = map?.[phone];
        if (known?.fullName) {
          localStorage.setItem("fullName", known.fullName);
        }
      } catch {
        // ignore localStorage/JSON errors
      }
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
    localStorage.removeItem("phoneNumber");
    localStorage.removeItem("fullName");
    set({
      token: null,
      refreshToken: null,
      userId: null,
      role: null,
      phoneNumber: null,
    });
  },
}));
