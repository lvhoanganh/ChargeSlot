import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { stationApi } from "@/services/api";

export default function ChargingStations() {
  const navigate = useNavigate();
  const [stations, setStations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    loadStations();
  }, []);

  const loadStations = async () => {
    try {
      setLoading(true);
      const data = await stationApi.getAll();
      setStations(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id, stationName) => {
    if (!confirm(`Bạn có chắc muốn xóa trạm "${stationName}"?`)) return;

    try {
      await stationApi.delete(id);
      alert("Xóa trạm sạc thành công!");
      loadStations();
    } catch (err) {
      alert(`Lỗi: ${err.message}`);
    }
  };

  const handleSubmitForApproval = async (id, stationName) => {
    if (!confirm(`Gửi trạm "${stationName}" đi phê duyệt?`)) return;

    try {
      await stationApi.submitForApproval(id);
      alert("Đã gửi trạm sạc đi phê duyệt!");
      loadStations();
    } catch (err) {
      alert(`Lỗi: ${err.message}`);
    }
  };

  const getStatusBadge = (status) => {
    const statusColors = {
      Draft: "bg-gray-200 text-gray-800",
      PendingApproval: "bg-yellow-200 text-yellow-800",
      Approved: "bg-green-200 text-green-800",
      Rejected: "bg-red-200 text-red-800",
    };

    const statusLabels = {
      Draft: "Bản nháp",
      PendingApproval: "Chờ duyệt",
      Approved: "Đã duyệt",
      Rejected: "Bị từ chối",
    };

    return (
      <span
        className={`px-3 py-1 rounded-full text-sm font-medium ${
          statusColors[status] || "bg-gray-200 text-gray-800"
        }`}
      >
        {statusLabels[status] || status}
      </span>
    );
  };

  const getOperationalStatusBadge = (status) => {
    const statusColors = {
      Open: "bg-green-100 text-green-800",
      Closed: "bg-red-100 text-red-800",
      Maintenance: "bg-orange-100 text-orange-800",
    };

    const statusLabels = {
      Open: "Đang mở",
      Closed: "Đã đóng",
      Maintenance: "Bảo trì",
    };

    return (
      <span
        className={`px-2 py-1 rounded text-xs font-medium ${
          statusColors[status] || "bg-gray-100 text-gray-800"
        }`}
      >
        {statusLabels[status] || status}
      </span>
    );
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-[#f3f4f5] p-8">
        <div className="max-w-7xl mx-auto">
          <p className="text-center text-gray-600">Đang tải...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-[#f3f4f5] p-8">
      <div className="max-w-7xl mx-auto">
        <div className="flex justify-between items-center mb-6">
          <div>
            <h1 className="text-3xl font-bold text-gray-800">
              Quản lý Trạm Sạc
            </h1>
            <p className="text-gray-600 mt-1">Danh sách các trạm sạc của bạn</p>
          </div>
          <Button
            onClick={() => navigate("/stations/create")}
            className="bg-orange-500 hover:bg-orange-600"
          >
            + Thêm Trạm Sạc Mới
          </Button>
        </div>

        {error && (
          <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded mb-4">
            Lỗi: {error}
          </div>
        )}

        {stations.length === 0 ? (
          <div className="bg-white rounded-lg shadow p-8 text-center">
            <p className="text-gray-600 mb-4">
              Chưa có trạm sạc nào. Hãy thêm trạm sạc đầu tiên của bạn!
            </p>
            <Button
              onClick={() => navigate("/stations/create")}
              className="bg-orange-500 hover:bg-orange-600"
            >
              + Thêm Trạm Sạc
            </Button>
          </div>
        ) : (
          <div className="bg-white rounded-lg shadow overflow-hidden">
            <table className="w-full">
              <thead className="bg-gray-50 border-b">
                <tr>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Tên trạm
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Địa chỉ
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Trạng thái duyệt
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Hoạt động
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Số slot
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Thao tác
                  </th>
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {stations.map((station) => (
                  <tr key={station.id} className="hover:bg-gray-50">
                    <td className="px-6 py-4">
                      <div className="text-sm font-medium text-gray-900">
                        {station.name}
                      </div>
                    </td>
                    <td className="px-6 py-4">
                      <div className="text-sm text-gray-600">
                        {station.address}
                      </div>
                    </td>
                    <td className="px-6 py-4">
                      {getStatusBadge(station.approvalStatus)}
                    </td>
                    <td className="px-6 py-4">
                      {getOperationalStatusBadge(station.operationalStatus)}
                    </td>
                    <td className="px-6 py-4">
                      <div className="text-sm text-gray-600">
                        {station.chargingSlots?.length || 0} slot
                      </div>
                    </td>
                    <td className="px-6 py-4 text-right text-sm font-medium">
                      <div className="flex gap-2 justify-end">
                        <Link
                          to={`/stations/${station.id}`}
                          className="text-blue-600 hover:text-blue-900"
                        >
                          Xem
                        </Link>

                        {(station.approvalStatus === "Draft" ||
                          station.approvalStatus === "Rejected") && (
                          <>
                            <Link
                              to={`/stations/${station.id}/edit`}
                              className="text-indigo-600 hover:text-indigo-900"
                            >
                              Sửa
                            </Link>
                            <button
                              onClick={() =>
                                handleDelete(station.id, station.name)
                              }
                              className="text-red-600 hover:text-red-900"
                            >
                              Xóa
                            </button>
                            <button
                              onClick={() =>
                                handleSubmitForApproval(
                                  station.id,
                                  station.name,
                                )
                              }
                              className="text-green-600 hover:text-green-900"
                            >
                              Gửi duyệt
                            </button>
                          </>
                        )}

                        {station.approvalStatus === "Approved" && (
                          <span className="text-gray-400 italic">Đã duyệt</span>
                        )}

                        {station.approvalStatus === "PendingApproval" && (
                          <span className="text-yellow-600 italic">
                            Đang chờ...
                          </span>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
