import { useMemo, useState, useEffect } from "react";

const STATUS = {
  PENDING: "PENDING",
  APPROVED: "APPROVED",
  REJECTED: "REJECTED",
};

const MOCK_STATIONS = [
  {
    id: 1,
    stationName: "Trạm sạc Vinhomes Grand Park",
    ownerName: "Nguyễn Văn A",
    address: "Thủ Đức, TP.HCM",
    submittedAt: "2026-03-01T09:00:00Z",
    status: STATUS.PENDING,
  },
  {
    id: 2,
    stationName: "Trạm sạc Riverside",
    ownerName: "Trần Thị B",
    address: "Ninh Kiều, Cần Thơ",
    submittedAt: "2026-03-02T10:30:00Z",
    status: STATUS.PENDING,
  },
  {
    id: 3,
    stationName: "Trạm sạc Đà Nẵng Center",
    ownerName: "Lê Văn C",
    address: "Hải Châu, Đà Nẵng",
    submittedAt: "2026-03-03T14:20:00Z",
    status: STATUS.PENDING,
  },
  {
    id: 4,
    stationName: "Trạm sạc Biên Hòa Hub",
    ownerName: "Phạm Thị D",
    address: "Biên Hòa, Đồng Nai",
    submittedAt: "2026-03-04T08:15:00Z",
    status: STATUS.PENDING,
  },
  {
    id: 5,
    stationName: "Trạm sạc Hà Nội West",
    ownerName: "Hoàng Văn E",
    address: "Nam Từ Liêm, Hà Nội",
    submittedAt: "2026-03-05T13:00:00Z",
    status: STATUS.PENDING,
  },
];

function formatDate(dateStr) {
  const d = new Date(dateStr);
  const dd = String(d.getDate()).padStart(2, "0");
  const mm = String(d.getMonth() + 1).padStart(2, "0");
  const yyyy = d.getFullYear();
  return `${dd}/${mm}/${yyyy}`;
}

function statusLabel(status) {
  if (status === STATUS.PENDING) return "Đang chờ duyệt";
  if (status === STATUS.APPROVED) return "Đã phê duyệt";
  return "Đã từ chối";
}

function statusClass(status) {
  if (status === STATUS.PENDING) return "bg-yellow-100 text-yellow-700";
  if (status === STATUS.APPROVED) return "bg-green-100 text-green-700";
  return "bg-red-100 text-red-700";
}

export default function ApproveStation() {

  const [stations, setStations] = useState(() => {
    const saved = localStorage.getItem("stations");
    return saved ? JSON.parse(saved) : MOCK_STATIONS;
  });

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [confirmAction, setConfirmAction] = useState(null);

  useEffect(() => {
    localStorage.setItem("stations", JSON.stringify(stations));
  }, [stations]);

  const filtered = useMemo(() => {
    const keyword = search.trim().toLowerCase();

    return stations.filter((s) => {
      const matchSearch =
        !keyword ||
        s.stationName.toLowerCase().includes(keyword) ||
        s.ownerName.toLowerCase().includes(keyword) ||
        s.address.toLowerCase().includes(keyword);

      const matchStatus =
        statusFilter === "ALL" || s.status === statusFilter;

      return matchSearch && matchStatus;
    });
  }, [stations, search, statusFilter]);

  const summary = useMemo(() => {
    return stations.reduce(
      (acc, item) => {
        acc.total += 1;
        if (item.status === STATUS.PENDING) acc.pending += 1;
        if (item.status === STATUS.APPROVED) acc.approved += 1;
        if (item.status === STATUS.REJECTED) acc.rejected += 1;
        return acc;
      },
      { total: 0, pending: 0, approved: 0, rejected: 0 }
    );
  }, [stations]);

  function updateStatus(id, newStatus) {
    setStations((prev) =>
      prev.map((item) =>
        item.id === id ? { ...item, status: newStatus } : item
      )
    );
  }

  function askUpdateStatus(station, newStatus) {
    setConfirmAction({ station, newStatus });
  }

  function confirmUpdateStatus() {
    if (!confirmAction) return;

    const { station, newStatus } = confirmAction;
    updateStatus(station.id, newStatus);
    setConfirmAction(null);
  }

  return (
    <div className="max-w-[95%] w-full mx-auto pt-28 pb-10">

      <div className="mb-6">
        <h1 className="text-2xl font-bold">Duyệt trạm sạc</h1>
        <p className="text-sm text-gray-500 mt-1">
          Quản trị viên có thể phê duyệt hoặc từ chối các yêu cầu đăng ký trạm sạc
        </p>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">

        <div className="bg-white border rounded-xl p-4">
          <p className="text-xs text-gray-500">Tổng yêu cầu</p>
          <p className="text-2xl font-bold">{summary.total}</p>
        </div>

        <div className="bg-white border rounded-xl p-4">
          <p className="text-xs text-gray-500">Đang chờ duyệt</p>
          <p className="text-2xl font-bold text-yellow-600">
            {summary.pending}
          </p>
        </div>

        <div className="bg-white border rounded-xl p-4">
          <p className="text-xs text-gray-500">Đã phê duyệt</p>
          <p className="text-2xl font-bold text-green-600">
            {summary.approved}
          </p>
        </div>

        <div className="bg-white border rounded-xl p-4">
          <p className="text-xs text-gray-500">Đã từ chối</p>
          <p className="text-2xl font-bold text-red-600">
            {summary.rejected}
          </p>
        </div>

      </div>
      <div className="bg-white border rounded-xl p-4 mb-4">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">

          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm theo tên trạm, chủ trạm hoặc địa chỉ..."
            className="h-10 rounded-md border px-3 outline-none focus:ring-2 focus:ring-blue-200"
          />

          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="h-10 rounded-md border px-3"
          >
            <option value="ALL">Tất cả</option>
            <option value={STATUS.PENDING}>Đang chờ duyệt</option>
            <option value={STATUS.APPROVED}>Đã phê duyệt</option>
            <option value={STATUS.REJECTED}>Đã từ chối</option>
          </select>

          <button
            onClick={() => {
              setSearch("");
              setStatusFilter("ALL");
            }}
            className="h-10 rounded-md border bg-gray-50 hover:bg-gray-100"
          >
            Xóa bộ lọc
          </button>

        </div>
      </div>

      {/* Table */}
      <div className="bg-white border rounded-xl overflow-x-auto">
        <table className="w-full min-w-[980px]">

          <thead className="bg-gray-50 border-b">
            <tr className="text-left text-sm text-gray-600">
              <th className="px-4 py-3">ID</th>
              <th className="px-4 py-3">Tên trạm</th>
              <th className="px-4 py-3">Chủ trạm</th>
              <th className="px-4 py-3">Địa chỉ</th>
              <th className="px-4 py-3">Ngày gửi</th>
              <th className="px-4 py-3">Trạng thái</th>
              <th className="px-4 py-3 text-right">Hành động</th>
            </tr>
          </thead>

          <tbody>
            {filtered.length === 0 ? (
              <tr>
                <td colSpan={8} className="text-center py-8 text-gray-500">
                  Không tìm thấy yêu cầu nào
                </td>
              </tr>
            ) : (
              filtered.map((s) => {
                const pending = s.status === STATUS.PENDING;

                return (
                  <tr key={s.id} className="border-b text-sm">

                    <td className="px-4 py-3">{s.id}</td>
                    <td className="px-4 py-3 font-medium">{s.stationName}</td>
                    <td className="px-4 py-3">{s.ownerName}</td>
                    <td className="px-4 py-3">{s.address}</td>
                    <td className="px-4 py-3">{formatDate(s.submittedAt)}</td>

                    <td className="px-4 py-3">
                      <span className={`px-2 py-1 rounded-full text-xs font-semibold ${statusClass(s.status)}`}>
                        {statusLabel(s.status)}
                      </span>
                    </td>

                    <td className="px-4 py-3">
                      <div className="flex justify-end gap-2">

                        <button
                          disabled={!pending}
                          onClick={() => askUpdateStatus(s, STATUS.APPROVED)}
                          className={`h-8 px-3 rounded-md text-white text-xs font-semibold ${pending
                              ? "bg-green-500 hover:bg-green-600"
                              : "bg-gray-400 cursor-not-allowed"
                            }`}
                        >
                          Phê duyệt
                        </button>

                        <button
                          disabled={!pending}
                          onClick={() => askUpdateStatus(s, STATUS.REJECTED)}
                          className={`h-8 px-3 rounded-md text-white text-xs font-semibold ${pending
                              ? "bg-red-500 hover:bg-red-600"
                              : "bg-gray-400 cursor-not-allowed"
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
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center">
          <div className="bg-white rounded-xl p-6 w-[420px]">

            <h2 className="text-lg font-bold mb-3">Xác nhận thao tác</h2>

            <p className="text-sm text-gray-600 mb-6">
              Bạn có chắc chắn{" "}
              {confirmAction.newStatus === STATUS.APPROVED
                ? "phê duyệt"
                : "từ chối"}{" "}
              trạm "{confirmAction.station.stationName}" không?
            </p>

            <div className="flex justify-end gap-2">

              <button
                onClick={() => setConfirmAction(null)}
                className="px-4 py-2 border rounded-md"
              >
                Hủy
              </button>

              <button
                onClick={confirmUpdateStatus}
                className={`px-4 py-2 rounded-md text-white ${confirmAction.newStatus === STATUS.APPROVED
                    ? "bg-green-500 hover:bg-green-600"
                    : "bg-red-500 hover:bg-red-600"
                  }`}
              >
                Xác nhận
              </button>

            </div>
          </div>
        </div>
      )}
    </div>
  );
}