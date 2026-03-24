import { instance } from "@/lib/httpRequest";
import { create } from "zustand";

export const useAdminAccountStore = create((set, get) => ({
  users: [],
  totalItems: 0,
  summary: { total: 0, active: 0, banned: 0 },
  loading: false,
  error: "",

  fetchUsers: async (search, role, status, page, pageSize) => {
    set({ loading: true, error: "" });
    try {
      const params = {};
      if (search) params.search = search;
      if (role && role !== "ALL") params.role = role;
      if (status && status !== "ALL") params.status = status;
      params.page = page;
      params.pageSize = pageSize;

      const res = await instance.get("/AdminAccounts", { params });
      const data = res.data;

      const sorted = (data.items || []).sort((a, b) => a.id - b.id);
      set({ users: sorted, totalItems: data.totalItems || 0 });
    } catch (error) {
      const msg =
        error?.response?.data?.message ||
        error?.message ||
        "Không thể tải danh sách người dùng.";
      set({ error: msg });
    } finally {
      set({ loading: false });
    }
  },

  fetchStatistics: async () => {
    try {
      const res = await instance.get("/AdminAccounts/statistics");
      const data = res.data;
      set({
        summary: {
          total: data.totalAccounts,
          active: data.activeAccounts,
          banned: data.bannedAccounts,
        },
      });
    } catch {
      // silent
    }
  },

  toggleBan: async (id) => {
    const res = await instance.patch(`/AdminAccounts/${id}/toggle-ban`);
    return res.data;
  },
}));
