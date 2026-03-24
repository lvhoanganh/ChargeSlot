import { useEffect, useRef, useState } from "react";
import { useAdminAccountStore } from "@/stores/adminAccountStore";
import { showToast } from "@/components/Toast";

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
];

function formatDate(dateStr) {
  const d = new Date(dateStr);
  const dd = String(d.getDate()).padStart(2, "0");
  const mm = String(d.getMonth() + 1).padStart(2, "0");
  const yyyy = d.getFullYear();
  return `${dd}/${mm}/${yyyy}`;
}

function maskPhone(phone) {
  if (!phone || phone.length < 6) return phone || "";
  return phone.slice(0, 3) + "***" + phone.slice(-3);
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
  const [search, setSearch] = useState("");
  const [role, setRole] = useState("ALL");
  const [status, setStatus] = useState("ALL");
  const [page, setPage] = useState(1);
  const pageSize = 10;
  const [confirmTarget, setConfirmTarget] = useState(null);
  const [toggling, setToggling] = useState(false);
  const debounceRef = useRef(null);

  const {
    users,
    totalItems,
    summary,
    loading,
    error,
    fetchUsers,
    fetchStatistics,
    toggleBan,
  } = useAdminAccountStore();

  useEffect(() => {
    fetchUsers(search, role, status, page, pageSize);
  }, [role, status, page, pageSize]);

  useEffect(() => {
    clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setPage(1);
      fetchUsers(search, role, status, 1, pageSize);
    }, 400);
    return () => clearTimeout(debounceRef.current);
  }, [search]);

  useEffect(() => {
    fetchStatistics();
  }, []);

  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));
  const currentPage = Math.min(page, totalPages);

  function resetFilter() {
    setSearch("");
    setRole("ALL");
    setStatus("ALL");
    setPage(1);
  }

  function askToggle(user) {
    if (user.role === "Admin") return;
    setConfirmTarget(user);
  }

  async function confirmToggleBan() {
    if (!confirmTarget) return;
    setToggling(true);
    try {
      await toggleBan(confirmTarget.id);
      await Promise.all([
        fetchUsers(search, role, status, page, pageSize),
        fetchStatistics(),
      ]);
    } catch (err) {
      showToast.error(
        err?.response?.data?.message || err?.message || "Thao tác thất bại."
      );
    } finally {
      setToggling(false);
      setConfirmTarget(null);
    }
  }

  return (
    <div className="max-w-[95%] w-full mx-auto pt-28 pb-10">
      <div className="mb-6">
        <h1 className="text-2xl font-bold">Quản lý người dùng</h1>
        <p className="text-sm text-gray-500 mt-1">
          Quản trị viên có thể kích hoạt hoặc vô hiệu hóa tài khoản của người
          dùng
        </p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 mb-6">
        <div className="bg-white border rounded-xl p-4 text-center">
          <p className="text-xs text-gray-500">Tổng số tài khoản</p>
          <p className="text-2xl font-bold">{summary.total}</p>
        </div>
        <div className="bg-white border rounded-xl p-4 text-center">
          <p className="text-xs text-gray-500">Hoạt động</p>
          <p className="text-2xl font-bold text-green-600">{summary.active}</p>
        </div>
        <div className="bg-white border rounded-xl p-4 text-center">
          <p className="text-xs text-gray-500">Vô hiệu hóa</p>
          <p className="text-2xl font-bold text-red-600">{summary.banned}</p>
        </div>
      </div>

      <div className="bg-white border rounded-xl p-4 mb-4">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-3">
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
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

      {error && (
        <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-lg text-red-700 text-sm">
          {error}
        </div>
      )}

      <div className="bg-white border rounded-xl overflow-x-auto relative">
        {loading && (
          <div className="absolute inset-0 bg-white/70 flex items-center justify-center z-10">
            <span className="text-gray-500 text-sm">Đang tải...</span>
          </div>
        )}

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
            {users.length === 0 && !loading ? (
              <tr>
                <td colSpan={7} className="text-center py-8 text-gray-500">
                  Không tìm thấy người dùng
                </td>
              </tr>
            ) : (
              users.map((u) => {
                const isAdmin = u.role === "Admin";
                return (
                  <tr key={u.id} className="border-b text-sm">
                    <td className="px-4 py-3">{u.id}</td>
                    <td className="px-4 py-3 font-medium">{u.fullName}</td>
                    <td className="px-4 py-3">{maskPhone(u.phoneNumber)}</td>
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
                        disabled={isAdmin}
                        onClick={() => askToggle(u)}
                        className={`h-8 w-28 ml-auto rounded-md text-white text-xs font-semibold flex items-center justify-center 
                          ${isAdmin
                            ? "bg-gray-400 cursor-not-allowed"
                            : u.status === "ACTIVE"
                              ? "bg-red-500 hover:bg-red-600"
                              : "bg-green-500 hover:bg-green-600"
                          }`}
                      >
                        {isAdmin
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
          Hiển thị {(currentPage - 1) * pageSize + (users.length ? 1 : 0)} -{" "}
          {(currentPage - 1) * pageSize + users.length} / {totalItems}
        </p>
        <div className="flex items-center gap-2">
          <button
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={currentPage <= 1}
            className="h-9 px-3 border rounded-md text-sm disabled:opacity-40"
          >
            Trước
          </button>
          {Array.from({ length: totalPages }, (_, i) => i + 1).map((p) => (
            <button
              key={p}
              onClick={() => setPage(p)}
              className={`h-9 w-9 rounded-md text-sm border ${p === currentPage
                ? "bg-blue-500 text-white border-blue-500"
                : "hover:bg-gray-100"
                }`}
            >
              {p}
            </button>
          ))}
          <button
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={currentPage >= totalPages}
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
                disabled={toggling}
                className="h-9 px-4 rounded-md border bg-white hover:bg-gray-50"
              >
                Hủy
              </button>
              <button
                onClick={confirmToggleBan}
                disabled={toggling}
                className={`h-9 px-4 rounded-md text-white ${confirmTarget.status === "ACTIVE"
                  ? "bg-red-500 hover:bg-red-600"
                  : "bg-green-500 hover:bg-green-600"
                  }`}
              >
                {toggling ? "Đang xử lý..." : "Xác nhận"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
