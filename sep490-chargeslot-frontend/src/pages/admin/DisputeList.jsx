import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { instance } from "@/lib/httpRequest";

/* ─── API helper ─── */
const disputeApiAdmin = {
  getPending: async () => {
    const { data } = await instance.get("/dispute/pending");
    return data;
  },
};

/* ─── Constants ─── */
const STATUS_MAP = {
  Open: { label: "Mở", cls: "bg-yellow-100 text-yellow-700" },
  WaitingOwnerEvidence: { label: "Chờ Owner phản hồi", cls: "bg-orange-100 text-orange-700" },
  PendingReview: { label: "Chờ xem xét", cls: "bg-blue-100 text-blue-700" },
  ResolvedRefund: { label: "Hoàn tiền Driver", cls: "bg-green-100 text-green-700" },
  ResolvedPayout: { label: "Thanh toán Owner", cls: "bg-purple-100 text-purple-700" },
};

function formatDate(dateStr) {
  if (!dateStr) return "—";
  const s = String(dateStr);
  const d = new Date(s.endsWith("Z") ? s : s + "Z");
  return `${String(d.getDate()).padStart(2, "0")}/${String(d.getMonth() + 1).padStart(2, "0")}/${d.getFullYear()} ${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
}

export default function DisputeList() {
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");

  const { data: disputes = [], isLoading, error } = useQuery({
    queryKey: ["admin-disputes-pending"],
    queryFn: disputeApiAdmin.getPending,
  });

  const filtered = useMemo(() => {
    const keyword = search.trim().toLowerCase();
    return disputes.filter((d) => {
      const matchSearch =
        !keyword ||
        d.createdByName?.toLowerCase().includes(keyword) ||
        d.reason?.toLowerCase().includes(keyword) ||
        String(d.id).includes(keyword) ||
        String(d.bookingId).includes(keyword);
      const matchStatus = statusFilter === "ALL" || d.status === statusFilter;
      return matchSearch && matchStatus;
    });
  }, [disputes, search, statusFilter]);

  const summary = useMemo(() => {
    return disputes.reduce(
      (acc, d) => {
        acc.total += 1;
        if (d.status === "WaitingOwnerEvidence") acc.waiting += 1;
        if (d.status === "PendingReview") acc.pending += 1;
        return acc;
      },
      { total: 0, waiting: 0, pending: 0 }
    );
  }, [disputes]);

  if (isLoading) {
    return (
      <div className="max-w-[95%] mx-auto pt-28 pb-10 text-center">
        <div className="text-lg text-slate-500">⏳ Đang tải danh sách khiếu nại...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="max-w-[95%] mx-auto pt-28 pb-10 text-center">
        <div className="text-lg text-red-500">❌ Lỗi tải dữ liệu: {error.message}</div>
      </div>
    );
  }

  return (
    <div className="max-w-[95%] w-full mx-auto pt-28 pb-10">
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold">Quản lý khiếu nại</h1>
        <p className="text-sm text-gray-500 mt-1">Xem xét và xử lý các khiếu nại từ Driver</p>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
        <div className="bg-white border rounded-xl p-4">
          <p className="text-xs text-gray-500">Tổng khiếu nại</p>
          <p className="text-2xl font-bold">{summary.total}</p>
        </div>
        <div className="bg-white border rounded-xl p-4">
          <p className="text-xs text-gray-500">Chờ Owner phản hồi</p>
          <p className="text-2xl font-bold text-orange-600">{summary.waiting}</p>
        </div>
        <div className="bg-white border rounded-xl p-4">
          <p className="text-xs text-gray-500">Sẵn sàng xem xét</p>
          <p className="text-2xl font-bold text-blue-600">{summary.pending}</p>
        </div>
      </div>

      {/* Filters */}
      <div className="bg-white border rounded-xl p-4 mb-4">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm theo ID, booking, driver, lý do..."
            className="h-10 rounded-md border px-3 outline-none focus:ring-2 focus:ring-blue-200"
          />
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="h-10 rounded-md border px-3"
          >
            <option value="ALL">Tất cả trạng thái</option>
            <option value="WaitingOwnerEvidence">Chờ Owner phản hồi</option>
            <option value="PendingReview">Chờ xem xét</option>
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
        <table className="w-full min-w-[800px]">
          <thead className="bg-gray-50 border-b">
            <tr className="text-left text-sm text-gray-600">
              <th className="px-4 py-3">ID</th>
              <th className="px-4 py-3">Booking</th>
              <th className="px-4 py-3">Driver</th>
              <th className="px-4 py-3">Lý do</th>
              <th className="px-4 py-3">Trạng thái</th>
              <th className="px-4 py-3">Ngày tạo</th>
              <th className="px-4 py-3 text-right">Hành động</th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 ? (
              <tr>
                <td colSpan={7} className="text-center py-8 text-gray-500">
                  Không tìm thấy khiếu nại nào
                </td>
              </tr>
            ) : (
              filtered.map((d) => {
                const st = STATUS_MAP[d.status] || STATUS_MAP.Open;
                return (
                  <tr key={d.id} className="border-b text-sm hover:bg-gray-50 transition-colors">
                    <td className="px-4 py-3 font-medium">#{d.id}</td>
                    <td className="px-4 py-3">#{d.bookingId}</td>
                    <td className="px-4 py-3">{d.createdByName}</td>
                    <td className="px-4 py-3 max-w-[200px] truncate">{d.reason}</td>
                    <td className="px-4 py-3">
                      <span className={`px-2 py-1 rounded-full text-xs font-semibold ${st.cls}`}>
                        {st.label}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-gray-500">{formatDate(d.createdAt)}</td>
                    <td className="px-4 py-3">
                      <div className="flex justify-end">
                        <button
                          onClick={() => navigate(`/admin/disputes/${d.id}`)}
                          className="h-8 px-4 rounded-md bg-blue-500 hover:bg-blue-600 text-white text-xs font-semibold cursor-pointer transition-colors"
                        >
                          Xem xét
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
    </div>
  );
}
