import { instance } from "@/lib/httpRequest";
import { create } from "zustand";
import { persist, createJSONStorage } from "zustand/middleware";

const API_BASE = import.meta.env.VITE_BASE_URL || "https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net/api";

export const useAuthStore = create(
  persist(
    (set) => ({
      token: null,
      refreshToken: null,
      userId: null,
      role: null,
      phoneNumber: null,

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

          // Cập nhật state qua Zustand persist (sẽ tự ghi localStorage qua "auth-store")
          set({
            token: data.accessToken,
            refreshToken: data.refreshToken || null,
            userId: data.userId,
            role: data.role,
            phoneNumber: data.phoneNumber || phoneNumber || null,
          });

          // Giữ lại các key riêng cho tương thích ngược với các phần khác của app
          localStorage.setItem("accessToken", data.accessToken);
          localStorage.setItem("refreshToken", data.refreshToken || "");
          localStorage.setItem("userId", data.userId);
          localStorage.setItem("role", data.role);
          localStorage.setItem("phoneNumber", data.phoneNumber || phoneNumber || "");

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

        // Clear tất cả auth keys
        ["accessToken", "refreshToken", "userId", "role", "phoneNumber", "expiresAtUtc", "userInfoByPhone", "auth-store"].forEach(k =>
          localStorage.removeItem(k)
        );

        // Clear charging session keys và các key user-specific khác
        Object.keys(localStorage).forEach(k => {
          if (
            k.startsWith("activeChargingBooking_") ||
            k === "activeChargingBookingId" ||
            k.startsWith("cs_") ||
            k.startsWith("wallet_") ||
            k.startsWith("booking_") ||
            k.startsWith("noti_")
          ) {
            localStorage.removeItem(k);
          }
        });

        set({ token: null, refreshToken: null, userId: null, role: null, phoneNumber: null });
        // Broadcast logout event → các store khác (admin, owner, ...) listen và reset
        window.dispatchEvent(new Event("cs:logout"));
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
    }),
    {
      name: "auth-store", // key trong localStorage — persist tự động đọc/ghi
      storage: createJSONStorage(() => localStorage),
      // Chỉ persist những field cần thiết để auth hoạt động
      partialize: (state) => ({
        token: state.token,
        refreshToken: state.refreshToken,
        userId: state.userId,
        role: state.role,
        phoneNumber: state.phoneNumber,
      }),
    }
  )
);