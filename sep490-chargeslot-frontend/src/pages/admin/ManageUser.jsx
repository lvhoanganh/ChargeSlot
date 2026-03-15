import { useMemo, useState } from "react";

const ROLE_OPTIONS = [
  { label: "Tất cả", value: "ALL" },
  { label: "Tài xế", value: "Driver" },
  { label: "Chủ trạm", value: "Owner" },
  { label: "Quản trị viên", value: "Admin" },
];

const STATUS_OPTIONS = [
  { label: "Tất cả", value: "ALL" },
  { label: "Hoạt động", value: "ACTIVE" },
  { label: "Bị cấm", value: "BANNED" },
  { label: "Tạm khóa", value: "SUSPENDED" },
];

const MOCK_USERS = [
  {
    id: 101,
    fullName: "Nguyen Van Admin",
    phoneNumber: "0901234567",
    role: "Admin",
    status: "ACTIVE",
    createdAt: "2026-01-11T09:30:00Z",
  },
  {
    id: 102,
    fullName: "Tran Thi Linh",
    phoneNumber: "0912345678",
    role: "Driver",
    status: "ACTIVE",
    createdAt: "2026-01-12T08:10:00Z",
  },
  {
    id: 103,
    fullName: "Le Hoang Minh",
    phoneNumber: "0923456789",
    role: "Owner",
    status: "BANNED",
    createdAt: "2026-01-14T13:25:00Z",
  },
  {
    id: 104,
    fullName: "Pham Gia Bao",
    phoneNumber: "0934567890",
    role: "Driver",
    status: "BANNED",
    createdAt: "2026-01-15T11:05:00Z",
  },
  {
    id: 105,
    fullName: "Doan Thanh Tu",
    phoneNumber: "0945678901",
    role: "Owner",
    status: "ACTIVE",
    createdAt: "2026-01-16T07:42:00Z",
  },
  {
    id: 106,
    fullName: "Vo Minh Quan",
    phoneNumber: "0956789012",
    role: "Driver",
    status: "BANNED",
    createdAt: "2026-01-18T14:16:00Z",
  },
];

function formatDate(dateStr) {
  const d = new Date(dateStr);
  const dd = String(d.getDate()).padStart(2, "0");
  const mm = String(d.getMonth() + 1).padStart(2, "0");
  const yyyy = d.getFullYear();
  return `${dd}/${mm}/${yyyy}`;
}

function getRoleLabel(role) {
  switch (role) {
    case "Driver":
      return "Tài xế";
    case "Owner":
      return "Chủ trạm";
    case "Admin":
      return "Quản trị viên";
    default:
      return role;
  }
}

function getStatusLabel(status) {
  switch (status) {
    case "ACTIVE":
      return "Hoạt động";
    case "BANNED":
      return "Bị cấm";
    case "SUSPENDED":
      return "Tạm khóa";
    default:
      return status;
  }
}

function statusClass(status) {
  if (status === "ACTIVE") return "bg-green-100 text-green-700";
  if (status === "BANNED") return "bg-red-100 text-red-700";
  return "bg-yellow-100 text-yellow-700";
}

export default function ManageUser() {
  const [users, setUsers] = useState(MOCK_USERS);
  const [search, setSearch] = useState("");
  const [role, setRole] = useState("ALL");
  const [status, setStatus] = useState("ALL");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(5);
  const [confirmTarget, setConfirmTarget] = useState(null);

  const filteredUsers = useMemo(() => {
    const keyword = search.trim().toLowerCase();

    return users.filter((u) => {
      const matchSearch =
        !keyword ||
        u.fullName.toLowerCase().includes(keyword) ||
        u.phoneNumber.includes(keyword);

      const matchRole = role === "ALL" || u.role === role;
      const matchStatus = status === "ALL" || u.status === status;

      return matchSearch && matchRole && matchStatus;
    });
  }, [users, search, role, status]);

  const totalPages = Math.max(1, Math.ceil(filteredUsers.length / pageSize));
  const currentPage = Math.min(page, totalPages);

  const pagedUsers = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredUsers.slice(start, start + pageSize);
  }, [filteredUsers, currentPage, pageSize]);

  const summary = useMemo(() => {
    return users.reduce(
      (acc, u) => {
        acc.total += 1;
        if (u.status === "ACTIVE") acc.active += 1;
        if (u.status === "BANNED") acc.banned += 1;
        if (u.status === "SUSPENDED") acc.suspended += 1;
        return acc;
      },
      { total: 0, active: 0, banned: 0, suspended: 0 }
    );
  }, [users]);

  function resetFilter() {
    setSearch("");
    setRole("ALL");
    setStatus("ALL");
    setPage(1);
  }

  function toggleBan(user) {
    if (user.role === "Admin" || user.status === "SUSPENDED") return;

    setUsers((prev) =>
      prev.map((item) => {
        if (item.id !== user.id) return item;

        return {
          ...item,
          status: item.status === "ACTIVE" ? "BANNED" : "ACTIVE",
        };
      })
    );
  }

  function askToggle(user) {
    if (user.role === "Admin" || user.status === "SUSPENDED") return;
    setConfirmTarget(user);
  }

  function confirmToggle() {
    if (!confirmTarget) return;
    toggleBan(confirmTarget);
    setConfirmTarget(null);
  }

  return (
    <div className="max-w-[95%] w-full mx-auto pt-28 pb-10">
      <div className="mb-6">
        <h1 className="text-2xl font-bold">Quản lý người dùng</h1>
        <p className="text-sm text-gray-500 mt-1">
          Quản trị viên có thể kích hoạt hoặc vô hiệu hóa tài khoản của người dùng
        </p>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 mb-6">
        <div className="bg-white border rounded-xl p-4 text-center">
          <p className="text-xs text-gray-500">Tổng số tài khoản</p>
          <p className="text-2xl font-bold">{summary.total}</p>
        </div>

        <div className="bg-white border rounded-xl p-4 text-center">
          <p className="text-xs text-gray-500">Hoạt động</p>
          <p className="text-2xl font-bold text-green-600">
            {summary.active}
          </p>
        </div>

        <div className="bg-white border rounded-xl p-4 text-center">
          <p className="text-xs text-gray-500">Vô hiệu hóa</p>
          <p className="text-2xl font-bold text-red-600">
            {summary.banned}
          </p>
        </div>
      </div>
      <div className="bg-white border rounded-xl p-4 mb-4">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-3">
          <input
            type="text"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            placeholder="Tìm theo tên hoặc số điện thoại..."
            className="h-10 rounded-md border px-3 outline-none focus:ring-2 focus:ring-blue-200"
          />

          <select
            value={role}
            onChange={(e) => {
              setRole(e.target.value);
              setPage(1);
            }}
            className="h-10 rounded-md border px-3"
          >
            {ROLE_OPTIONS.map((item) => (
              <option key={item.value} value={item.value}>
                {item.label}
              </option>
            ))}
          </select>

          <select
            value={status}
            onChange={(e) => {
              setStatus(e.target.value);
              setPage(1);
            }}
            className="h-10 rounded-md border px-3"
          >
            {STATUS_OPTIONS.map((item) => (
              <option key={item.value} value={item.value}>
                {item.label}
              </option>
            ))}
          </select>

          <button
            onClick={resetFilter}
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
              <th className="px-4 py-3">Họ tên</th>
              <th className="px-4 py-3">Số điện thoại</th>
              <th className="px-4 py-3">Vai trò</th>
              <th className="px-4 py-3">Trạng thái</th>
              <th className="px-4 py-3">Ngày tạo</th>
              <th className="px-4 py-3 text-right">Hành động</th>
            </tr>
          </thead>

          <tbody>
            {pagedUsers.length === 0 ? (
              <tr>
                <td colSpan={7} className="text-center py-8 text-gray-500">
                  Không tìm thấy người dùng
                </td>
              </tr>
            ) : (
              pagedUsers.map((u) => {
                const isAdmin = u.role === "Admin";
                const isSuspended = u.status === "SUSPENDED";
                const disabled = isAdmin || isSuspended;

                return (
                  <tr key={u.id} className="border-b text-sm">
                    <td className="px-4 py-3">{u.id}</td>
                    <td className="px-4 py-3 font-medium">{u.fullName}</td>
                    <td className="px-4 py-3">{u.phoneNumber}</td>
                    <td className="px-4 py-3">{getRoleLabel(u.role)}</td>

                    <td className="px-4 py-3">
                      <span
                        className={`px-2 py-1 rounded-full text-xs font-semibold ${statusClass(
                          u.status
                        )}`}
                      >
                        {getStatusLabel(u.status)}
                      </span>
                    </td>

                    <td className="px-4 py-3">{formatDate(u.createdAt)}</td>

                    <td className="px-4 py-3 text-right">
                      <button
                        disabled={disabled}
                        onClick={() => askToggle(u)}
                        className={`h-8 w-28 ml-auto rounded-md text-white text-xs font-semibold flex items-center justify-center 
                          ${disabled
                            ? "bg-gray-400 cursor-not-allowed"
                            : u.status === "ACTIVE"
                              ? "bg-red-500 hover:bg-red-600"
                              : "bg-green-500 hover:bg-green-600"
                          }`}
                      >
                        {disabled
                          ? "Không khả dụng"
                          : u.status === "ACTIVE"
                            ? "Vô hiệu hóa"
                            : "Kích hoạt"}
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
      <div className="mt-4 flex items-center justify-between flex-wrap gap-3">
        <p className="text-sm text-gray-600">
          Hiển thị {(currentPage - 1) * pageSize + (pagedUsers.length ? 1 : 0)}{" "}
          - {(currentPage - 1) * pageSize + pagedUsers.length} /{" "}
          {filteredUsers.length}
        </p>

        <div className="flex items-center gap-2">
          <button
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={currentPage === 1}
            className="h-9 px-3 border rounded-md text-sm disabled:opacity-40"
          >
            Trước
          </button>

          <span className="text-sm min-w-20 text-center">
            Trang {currentPage}/{totalPages}
          </span>

          <button
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={currentPage === totalPages}
            className="h-9 px-3 border rounded-md text-sm disabled:opacity-40"
          >
            Sau
          </button>
        </div>
      </div>

      {confirmTarget && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4">
          <div className="w-full max-w-md bg-white rounded-xl shadow-lg p-5">
            <h2 className="text-lg font-bold mb-2">Xác nhận thao tác</h2>
            <p className="text-sm text-gray-600 mb-5">
              Bạn có chắc chắn muốn{" "}
              {confirmTarget.status === "ACTIVE"
                ? "vô hiệu hóa"
                : "kích hoạt"}{" "}
              tài khoản này không?
            </p>
            <div className="flex justify-end gap-2">
              <button
                onClick={() => setConfirmTarget(null)}
                className="h-9 px-4 rounded-md border bg-white hover:bg-gray-50"
              >
                Hủy
              </button>
              <button
                onClick={confirmToggle}
                className={`h-9 px-4 rounded-md text-white ${confirmTarget.status === "ACTIVE"
                    ? "bg-red-500 hover:bg-red-600"
                    : "bg-green-500 hover:bg-green-600"
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
