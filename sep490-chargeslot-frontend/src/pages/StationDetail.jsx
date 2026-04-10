import { useEffect, useState } from "react";
import { useNavigate, useParams, Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { stationApi, chargingApi } from "@/services/api";
import { formatVN } from "@/utils/dateVN";
import { showToast } from "@/components/Toast";
import { showConfirm } from "@/components/ConfirmDialog";

export default function StationDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [station, setStation] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeSlotIds, setActiveSlotIds] = useState(new Set()); // Track slots with active sessions

  const dayNames = [
    "Chủ nhật",
    "Thứ hai",
    "Thứ ba",
    "Thứ tư",
    "Thứ năm",
    "Thứ sáu",
    "Thứ bảy",
  ];

  useEffect(() => {
    loadStation();
  }, [id]);

  // Fetch active charging sessions for this station
  useEffect(() => {
    if (!id || !station?.chargingSlots) return;
    const fetchActiveSessions = async () => {
      try {
        const response = await chargingApi.getByStationId(Number(id));
        const activeSessions = Array.isArray(response)
          ? response.filter(s => !s.actualEndTime && s.bookingStatus !== "Completed")
          : (response?.items || []).filter(s => !s.actualEndTime && s.bookingStatus !== "Completed");
        
        const slotIds = new Set(activeSessions.map(s => s.slotId));
        setActiveSlotIds(slotIds);
      } catch { 
        // Reset on error
        setActiveSlotIds(new Set());
      }
    };
    
    fetchActiveSessions();
    // Poll every 5 seconds to update active slots
    const interval = setInterval(fetchActiveSessions, 5000);
    return () => clearInterval(interval);
  }, [id, station?.chargingSlots]);

  const loadStation = async () => {
    try {
      setLoading(true);
      const data = await stationApi.getById(id);
      setStation(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async () => {
    if (!(await showConfirm(`Bạn có chắc muốn xóa trạm "${station.name}"?`, "Xóa trạm sạc"))) return;

    try {
      await stationApi.delete(id);
      showToast.success("Xóa trạm sạc thành công!");
      navigate("/stations");
    } catch (err) {
      showToast.error(`Lỗi: ${err.message}`);
    }
  };

  const handleSubmitForApproval = async () => {
    if (!(await showConfirm(`Gửi trạm "${station.name}" đi phê duyệt?`, "Gửi phê duyệt"))) return;

    try {
      await stationApi.submitForApproval(id);
      showToast.success("Đã gửi trạm sạc đi phê duyệt!");
      loadStation(); // Reload to see updated status
    } catch (err) {
      showToast.error(`Lỗi: ${err.message}`);
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
        className={`px-4 py-2 rounded-full text-sm font-semibold ${
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
        className={`px-3 py-1 rounded text-sm font-medium ${
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
        <div className="max-w-6xl mx-auto">
          <p className="text-center text-gray-600">Đang tải...</p>
        </div>
      </div>
    );
  }

  if (error || !station) {
    return (
      <div className="min-h-screen bg-[#f3f4f5] p-8">
        <div className="max-w-6xl mx-auto">
          <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded">
            Lỗi: {error || "Không tìm thấy trạm sạc"}
          </div>
          <Button
            onClick={() => navigate("/stations")}
            className="mt-4 bg-gray-500 hover:bg-gray-600"
          >
            ← Quay lại danh sách
          </Button>
        </div>
      </div>
    );
  }

  const canEdit =
    station.approvalStatus === "Draft" || station.approvalStatus === "Rejected";

  return (
    <div className="min-h-screen bg-[#f3f4f5] p-8">
      <div className="max-w-6xl mx-auto">
        {/* Header */}
        <div className="mb-6">
          <Button
            onClick={() => navigate("/stations")}
            className="mb-4 bg-gray-500 hover:bg-gray-600"
          >
            ← Quay lại danh sách
          </Button>

          <div className="flex justify-between items-start">
            <div>
              <h1 className="text-3xl font-bold text-gray-800">
                {station.name}
              </h1>
              <p className="text-gray-600 mt-2 flex items-center gap-3">
                <span>📍 {station.address}</span>
              </p>
            </div>

            <div className="flex gap-3">
              {getStatusBadge(station.approvalStatus)}
              {getOperationalStatusBadge(station.operationalStatus)}
            </div>
          </div>
        </div>

        {/* Actions */}
        {canEdit && (
          <div className="mb-6 flex gap-3">
            <Link to={`/stations/${id}/edit`}>
              <Button className="bg-indigo-500 hover:bg-indigo-600">
                ✏️ Chỉnh sửa
              </Button>
            </Link>
            <Button
              onClick={handleSubmitForApproval}
              className="bg-green-500 hover:bg-green-600"
            >
              ✓ Gửi phê duyệt
            </Button>
            <Button
              onClick={handleDelete}
              className="bg-red-500 hover:bg-red-600"
            >
              🗑️ Xóa
            </Button>
          </div>
        )}

        {/* Admin Note (if rejected) */}
        {station.approvalStatus === "Rejected" && station.adminNote && (
          <div className="mb-6 bg-red-50 border border-red-200 rounded-lg p-4">
            <h3 className="font-semibold text-red-800 mb-2">
              Ghi chú từ Admin:
            </h3>
            <p className="text-red-700">{station.adminNote}</p>
          </div>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Main Info */}
          <div className="lg:col-span-2 space-y-6">
            {/* Basic Information */}
            <div className="bg-white rounded-lg shadow p-6">
              <h2 className="text-xl font-semibold mb-4 text-gray-800">
                Thông tin cơ bản
              </h2>

              <div className="space-y-3">
                <div className="grid grid-cols-3 gap-4">
                  <span className="text-gray-600 font-medium">Tên trạm:</span>
                  <span className="col-span-2 text-gray-900">
                    {station.name}
                  </span>
                </div>

                <div className="grid grid-cols-3 gap-4">
                  <span className="text-gray-600 font-medium">Địa chỉ:</span>
                  <span className="col-span-2 text-gray-900">
                    {station.address}
                  </span>
                </div>

                {station.description && (
                  <div className="grid grid-cols-3 gap-4">
                    <span className="text-gray-600 font-medium">Mô tả:</span>
                    <span className="col-span-2 text-gray-900">
                      {station.description}
                    </span>
                  </div>
                )}

                {(station.latitude || station.longitude) && (
                  <div className="grid grid-cols-3 gap-4">
                    <span className="text-gray-600 font-medium">
                      Tọa độ GPS:
                    </span>
                    <span className="col-span-2 text-gray-900">
                      Lat: {station.latitude}, Long: {station.longitude}
                    </span>
                  </div>
                )}

                <div className="grid grid-cols-3 gap-4">
                  <span className="text-gray-600 font-medium">Tạo lúc:</span>
                  <span className="col-span-2 text-gray-900">
                    {formatVN(station.createdAt)}
                  </span>
                </div>

                {station.updatedAt && (
                  <div className="grid grid-cols-3 gap-4">
                    <span className="text-gray-600 font-medium">Cập nhật:</span>
                    <span className="col-span-2 text-gray-900">
                      {formatVN(station.updatedAt)}
                    </span>
                  </div>
                )}
              </div>
            </div>

            {/* Layout Information */}
            {(station.layoutImageUrl ||
              station.layoutWidth ||
              station.layoutHeight) && (
              <div className="bg-white rounded-lg shadow p-6">
                <h2 className="text-xl font-semibold mb-4 text-gray-800">
                  Thông tin sơ đồ
                </h2>

                <div className="space-y-3">
                  {station.layoutImageUrl && (
                    <div>
                      <span className="text-gray-600 font-medium block mb-2">
                        Hình sơ đồ:
                      </span>
                      <img
                        src={station.layoutImageUrl}
                        alt="Layout"
                        className="max-w-full rounded border"
                      />
                    </div>
                  )}

                  {(station.layoutWidth || station.layoutHeight) && (
                    <div className="grid grid-cols-3 gap-4">
                      <span className="text-gray-600 font-medium">
                        Kích thước:
                      </span>
                      <span className="col-span-2 text-gray-900">
                        {station.layoutWidth} × {station.layoutHeight} mét
                      </span>
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* Images */}
            {station.images && station.images.length > 0 && (
              <div className="bg-white rounded-lg shadow p-6">
                <h2 className="text-xl font-semibold mb-4 text-gray-800">
                  Hình ảnh trạm ({station.images.length})
                </h2>

                <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
                  {station.images.map((img) => (
                    <div key={img.id} className="aspect-square">
                      <img
                        src={img.imageUrl}
                        alt={`Station ${img.id}`}
                        className="w-full h-full object-cover rounded border"
                      />
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Charging Slots */}
            {station.chargingSlots && station.chargingSlots.length > 0 && (
              <div className="bg-white rounded-lg shadow p-6">
                <h2 className="text-xl font-semibold mb-4 text-gray-800">
                  Các slot sạc ({station.chargingSlots.length})
                </h2>

                <div className="space-y-3">
                  {station.chargingSlots.map((slot) => {
                    // Check if this slot has an active charging session
                    const hasActiveSession = activeSlotIds.has(slot.id);
                    const displayStatus = hasActiveSession ? "Occupied" : slot.status;
                    
                    return (
                      <div
                        key={slot.id}
                        className="flex justify-between items-center p-3 bg-gray-50 rounded border"
                      >
                        <div>
                          <div className="font-medium text-gray-900">
                            {slot.slotName}
                          </div>
                          <div className="text-sm text-gray-600">
                            {slot.connectorType} • {slot.powerOutput}kW
                          </div>
                        </div>
                        <div className="text-right">
                          <div className="font-semibold text-orange-600">
                            {slot.pricePerHour?.toLocaleString("vi-VN")}đ/giờ
                          </div>
                          <div className={`text-xs ${hasActiveSession ? "text-red-600 font-medium" : "text-gray-500"}`}>
                            {displayStatus === "Occupied" ? "Đang dùng" : displayStatus}
                          </div>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}
          </div>

          {/* Side Info */}
          <div className="space-y-6">
            {/* Operating Hours */}
            <div className="bg-white rounded-lg shadow p-6">
              <h2 className="text-xl font-semibold mb-4 text-gray-800">
                Giờ hoạt động
              </h2>

              {station.operatingHours && station.operatingHours.length > 0 ? (
                <div className="space-y-2">
                  {[0, 1, 2, 3, 4, 5, 6].map((dayOfWeek) => {
                    const hours = station.operatingHours.find(
                      (h) => h.dayOfWeek === dayOfWeek,
                    );

                    return (
                      <div
                        key={dayOfWeek}
                        className="flex justify-between text-sm"
                      >
                        <span className="font-medium text-gray-700">
                          {dayNames[dayOfWeek]}:
                        </span>
                        <span className="text-gray-900">
                          {hours && !hours.isClosed
                            ? `${hours.openTime} - ${hours.closeTime}`
                            : "Đóng cửa"}
                        </span>
                      </div>
                    );
                  })}
                </div>
              ) : (
                <p className="text-gray-500 text-sm">
                  Chưa cập nhật giờ hoạt động
                </p>
              )}
            </div>

            {/* Statistics */}
            <div className="bg-white rounded-lg shadow p-6">
              <h2 className="text-xl font-semibold mb-4 text-gray-800">
                Thống kê
              </h2>

              <div className="space-y-3">
                <div className="flex justify-between">
                  <span className="text-gray-600">Tổng số slot:</span>
                  <span className="font-semibold text-gray-900">
                    {station.chargingSlots?.length || 0}
                  </span>
                </div>

                <div className="flex justify-between">
                  <span className="text-gray-600">Số hình ảnh:</span>
                  <span className="font-semibold text-gray-900">
                    {station.images?.length || 0}
                  </span>
                </div>

                <div className="flex justify-between">
                  <span className="text-gray-600">Owner ID:</span>
                  <span className="font-semibold text-gray-900">
                    {station.ownerUserId}
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
