import { useMemo, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { instance } from "@/lib/httpRequest";

/* ─── API helpers ─── */
const adminStationApi = {
  getPending: async () => {
    const { data } = await instance.get("/admin/stations/pending");
    return data;
  },
  review: async (stationId, body) => {
    const { data } = await instance.post(`/admin/stations/${stationId}/review`, body);
    return data;
  },
};

/* ─── Constants ─── */
const STATUS_MAP = {
  Draft: { label: "Bản nháp", cls: "bg-slate-100 text-slate-600" },
  PendingApproval: { label: "Đang chờ duyệt", cls: "bg-yellow-100 text-yellow-700" },
  Approved: { label: "Đã phê duyệt", cls: "bg-green-100 text-green-700" },
  Rejected: { label: "Đã từ chối", cls: "bg-red-100 text-red-700" },
};

function formatDate(dateStr) {
  if (!dateStr) return "—";
  const d = new Date(dateStr);
  return `${String(d.getDate()).padStart(2, "0")}/${String(d.getMonth() + 1).padStart(2, "0")}/${d.getFullYear()}`;
}

export default function ApproveStation() {
  const queryClient = useQueryClient();

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [confirmAction, setConfirmAction] = useState(null);
  const [adminNote, setAdminNote] = useState("");
  const [actionLoading, setActionLoading] = useState(false);

  /* ─── Fetch pending stations from BE ─── */
  const { data: stations = [], isLoading, error } = useQuery({
    queryKey: ["admin-stations-pending"],
    queryFn: adminStationApi.getPending,
  });

  /* ─── Approve / Reject mutation ─── */
  const reviewMutation = useMutation({
    mutationFn: ({ stationId, isApproved, adminNote }) =>
      adminStationApi.review(stationId, { isApproved, adminNote: adminNote || null }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-stations-pending"] });
      setConfirmAction(null);
      setAdminNote("");
    },
    onError: (err) => {
      const msg = err?.response?.data?.error || err?.message || "Lỗi không xác định";
      alert("Lỗi: " + msg);
    },
  });

  /* ─── Filter ─── */
  const filtered = useMemo(() => {
    const keyword = search.trim().toLowerCase();
    return stations.filter((s) => {
      const matchSearch =
        !keyword ||
        s.name?.toLowerCase().includes(keyword) ||
        s.address?.toLowerCase().includes(keyword);
      const matchStatus =
        statusFilter === "ALL" || s.approvalStatus === statusFilter;
      return matchSearch && matchStatus;
    });
  }, [stations, search, statusFilter]);

  /* ─── Summary ─── */
  const summary = useMemo(() => {
    return stations.reduce(
      (acc, s) => {
        acc.total += 1;
        if (s.approvalStatus === "PendingApproval") acc.pending += 1;
        else if (s.approvalStatus === "Approved") acc.approved += 1;
        else if (s.approvalStatus === "Rejected") acc.rejected += 1;
        return acc;
      },
      { total: 0, pending: 0, approved: 0, rejected: 0 }
    );
  }, [stations]);

  function askReview(station, isApproved) {
    setConfirmAction({ station, isApproved });
    setAdminNote("");
  }

  function confirmReview() {
    if (!confirmAction) return;
    reviewMutation.mutate({
      stationId: confirmAction.station.id,
      isApproved: confirmAction.isApproved,
      adminNote,
    });
  }

  if (isLoading) {
    return (
      <div className="max-w-[95%] mx-auto pt-28 pb-10 text-center">
        <div className="text-lg text-slate-500">⏳ Đang tải danh sách trạm...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="max-w-[95%] mx-auto pt-28 pb-10 text-center">
        <div className="text-lg text-red-500">❌ Lỗi tải dữ liệu: {error.message}</div>
        <button
          onClick={() => queryClient.invalidateQueries({ queryKey: ["admin-stations-pending"] })}
          className="mt-4 px-4 py-2 bg-blue-500 text-white rounded-lg"
        >
          Thử lại
        </button>
      </div>
    );
  }

  return (
    <div className="max-w-[95%] w-full mx-auto pt-28 pb-10">
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold">Duyệt trạm sạc</h1>
        <p className="text-sm text-gray-500 mt-1">
          Quản trị viên có thể phê duyệt hoặc từ chối các yêu cầu đăng ký trạm sạc
        </p>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <div className="bg-white border rounded-xl p-4">
          <p className="text-xs text-gray-500">Tổng yêu cầu</p>
          <p className="text-2xl font-bold">{summary.total}</p>
        </div>
        <div className="bg-white border rounded-xl p-4">
          <p className="text-xs text-gray-500">Đang chờ duyệt</p>
          <p className="text-2xl font-bold text-yellow-600">{summary.pending}</p>
        </div>
        <div className="bg-white border rounded-xl p-4">
          <p className="text-xs text-gray-500">Đã phê duyệt</p>
          <p className="text-2xl font-bold text-green-600">{summary.approved}</p>
        </div>
        <div className="bg-white border rounded-xl p-4">
          <p className="text-xs text-gray-500">Đã từ chối</p>
          <p className="text-2xl font-bold text-red-600">{summary.rejected}</p>
        </div>
      </div>

      {/* Filters */}
      <div className="bg-white border rounded-xl p-4 mb-4">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm theo tên trạm hoặc địa chỉ..."
            className="h-10 rounded-md border px-3 outline-none focus:ring-2 focus:ring-blue-200"
          />
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="h-10 rounded-md border px-3"
          >
            <option value="ALL">Tất cả</option>
            <option value="PendingApproval">Đang chờ duyệt</option>
            <option value="Approved">Đã phê duyệt</option>
            <option value="Rejected">Đã từ chối</option>
          </select>
          <button
            onClick={() => { setSearch(""); setStatusFilter("ALL"); }}
            className="h-10 rounded-md border bg-gray-50 hover:bg-gray-100"
          >
            Xóa bộ lọc
          </button>
        </div>
      </div>

      {/* Table */}
      <div className="bg-white border rounded-xl overflow-x-auto">
        <table className="w-full min-w-[900px]">
          <thead className="bg-gray-50 border-b">
            <tr className="text-left text-sm text-gray-600">
              <th className="px-4 py-3">ID</th>
              <th className="px-4 py-3">Tên trạm</th>
              <th className="px-4 py-3">Địa chỉ</th>
              <th className="px-4 py-3">Số ổ sạc</th>
              <th className="px-4 py-3">Ngày tạo</th>
              <th className="px-4 py-3">Trạng thái</th>
              <th className="px-4 py-3 text-right">Hành động</th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 ? (
              <tr>
                <td colSpan={7} className="text-center py-8 text-gray-500">
                  Không tìm thấy yêu cầu nào
                </td>
              </tr>
            ) : (
              filtered.map((s) => {
                const isPending = s.approvalStatus === "PendingApproval";
                const st = STATUS_MAP[s.approvalStatus] || STATUS_MAP.Draft;

                return (
                  <tr key={s.id} className="border-b text-sm">
                    <td className="px-4 py-3">{s.id}</td>
                    <td className="px-4 py-3 font-medium">{s.name}</td>
                    <td className="px-4 py-3">{s.address}</td>
                    <td className="px-4 py-3">{s.chargingSlots?.length || 0}</td>
                    <td className="px-4 py-3">{formatDate(s.createdAt)}</td>
                    <td className="px-4 py-3">
                      <span className={`px-2 py-1 rounded-full text-xs font-semibold ${st.cls}`}>
                        {st.label}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex justify-end gap-2">
                        <button
                          disabled={!isPending}
                          onClick={() => askReview(s, true)}
                          className={`h-8 px-3 rounded-md text-white text-xs font-semibold ${
                            isPending ? "bg-green-500 hover:bg-green-600 cursor-pointer" : "bg-gray-400 cursor-not-allowed"
                          }`}
                        >
                          Phê duyệt
                        </button>
                        <button
                          disabled={!isPending}
                          onClick={() => askReview(s, false)}
                          className={`h-8 px-3 rounded-md text-white text-xs font-semibold ${
                            isPending ? "bg-red-500 hover:bg-red-600 cursor-pointer" : "bg-gray-400 cursor-not-allowed"
                          }`}
                        >
                          Từ chối
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Confirm Modal */}
      {confirmAction && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
          <div className="bg-white rounded-xl p-6 w-[460px]">
            <h2 className="text-lg font-bold mb-3">Xác nhận thao tác</h2>
            <p className="text-sm text-gray-600 mb-4">
              Bạn có chắc chắn{" "}
              <strong className={confirmAction.isApproved ? "text-green-600" : "text-red-600"}>
                {confirmAction.isApproved ? "phê duyệt" : "từ chối"}
              </strong>{" "}
              trạm "<strong>{confirmAction.station.name}</strong>" không?
            </p>

            {/* Admin note */}
            <div className="mb-4">
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Ghi chú {!confirmAction.isApproved && <span className="text-red-500">*</span>}
              </label>
              <textarea
                value={adminNote}
                onChange={(e) => setAdminNote(e.target.value)}
                placeholder={confirmAction.isApproved ? "Ghi chú (tùy chọn)" : "Lý do từ chối..."}
                className="w-full h-20 rounded-lg border px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-200 resize-none"
              />
            </div>

            <div className="flex justify-end gap-2">
              <button
                onClick={() => { setConfirmAction(null); setAdminNote(""); }}
                className="px-4 py-2 border rounded-md"
                disabled={reviewMutation.isPending}
              >
                Hủy
              </button>
              <button
                onClick={confirmReview}
                disabled={reviewMutation.isPending || (!confirmAction.isApproved && !adminNote.trim())}
                className={`px-4 py-2 rounded-md text-white ${
                  confirmAction.isApproved
                    ? "bg-green-500 hover:bg-green-600"
                    : "bg-red-500 hover:bg-red-600"
                } disabled:opacity-50`}
              >
                {reviewMutation.isPending ? "Đang xử lý..." : "Xác nhận"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}