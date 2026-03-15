import { useState, useEffect } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { stationApi } from "@/services/api";

export default function EditStation() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [formData, setFormData] = useState({
    name: "",
    address: "",
    description: "",
    latitude: "",
    longitude: "",
    layoutImageUrl: "",
    layoutWidth: "",
    layoutHeight: "",
    imageUrls: "",
    operatingHours: [
      { dayOfWeek: 1, isClosed: false, openTime: "08:00", closeTime: "20:00" },
      { dayOfWeek: 2, isClosed: false, openTime: "08:00", closeTime: "20:00" },
      { dayOfWeek: 3, isClosed: false, openTime: "08:00", closeTime: "20:00" },
      { dayOfWeek: 4, isClosed: false, openTime: "08:00", closeTime: "20:00" },
      { dayOfWeek: 5, isClosed: false, openTime: "08:00", closeTime: "20:00" },
      { dayOfWeek: 6, isClosed: false, openTime: "08:00", closeTime: "20:00" },
      { dayOfWeek: 0, isClosed: false, openTime: "08:00", closeTime: "20:00" },
    ],
  });

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

  const loadStation = async () => {
    try {
      setLoading(true);
      const data = await stationApi.getById(id);

      // Prepare operating hours - merge with existing days
      const allDays = [1, 2, 3, 4, 5, 6, 0]; // Mon-Sun
      const operatingHours = allDays.map((dayOfWeek) => {
        const existing = data.operatingHours?.find(
          (h) => h.dayOfWeek === dayOfWeek,
        );
        if (existing) {
          return {
            dayOfWeek,
            isClosed: existing.isClosed,
            openTime: existing.openTime || "08:00",
            closeTime: existing.closeTime || "20:00",
          };
        }
        return {
          dayOfWeek,
          isClosed: true,
          openTime: "08:00",
          closeTime: "20:00",
        };
      });

      setFormData({
        name: data.name || "",
        address: data.address || "",
        description: data.description || "",
        latitude: data.latitude || "",
        longitude: data.longitude || "",
        layoutImageUrl: data.layoutImageUrl || "",
        layoutWidth: data.layoutWidth || "",
        layoutHeight: data.layoutHeight || "",
        imageUrls: data.images?.map((img) => img.imageUrl).join("\n") || "",
        operatingHours,
      });
    } catch (err) {
      alert(`Lỗi: ${err.message}`);
      navigate("/stations");
    } finally {
      setLoading(false);
    }
  };

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }));
  };

  const handleOperatingHourChange = (index, field, value) => {
    const newHours = [...formData.operatingHours];
    newHours[index] = {
      ...newHours[index],
      [field]: field === "isClosed" ? value === "true" : value,
    };
    setFormData((prev) => ({
      ...prev,
      operatingHours: newHours,
    }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitting(true);

    try {
      // Prepare data for API
      const payload = {
        name: formData.name,
        address: formData.address,
        description: formData.description || null,
        latitude: formData.latitude ? parseFloat(formData.latitude) : null,
        longitude: formData.longitude ? parseFloat(formData.longitude) : null,
        layoutImageUrl: formData.layoutImageUrl || null,
        layoutWidth: formData.layoutWidth
          ? parseFloat(formData.layoutWidth)
          : null,
        layoutHeight: formData.layoutHeight
          ? parseFloat(formData.layoutHeight)
          : null,
        operatingHours: formData.operatingHours
          .filter((h) => !h.isClosed)
          .map((h) => ({
            dayOfWeek: parseInt(h.dayOfWeek),
            isClosed: false,
            openTime: h.openTime,
            closeTime: h.closeTime,
          })),
        imageUrls: formData.imageUrls
          ? formData.imageUrls.split("\n").filter((url) => url.trim())
          : null,
      };

      await stationApi.update(id, payload);
      alert("Cập nhật trạm sạc thành công!");
      navigate("/stations");
    } catch (err) {
      alert(`Lỗi: ${err.message}`);
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-[#f3f4f5] p-8">
        <div className="max-w-4xl mx-auto">
          <p className="text-center text-gray-600">Đang tải...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-[#f3f4f5] p-8">
      <div className="max-w-4xl mx-auto">
        <div className="mb-6">
          <h1 className="text-3xl font-bold text-gray-800">
            Chỉnh Sửa Trạm Sạc
          </h1>
          <p className="text-gray-600 mt-1">
            Cập nhật thông tin trạm sạc của bạn
          </p>
        </div>

        <form
          onSubmit={handleSubmit}
          className="bg-white rounded-lg shadow p-6"
        >
          {/* Basic Information */}
          <div className="mb-6">
            <h2 className="text-xl font-semibold mb-4 text-gray-800">
              Thông tin cơ bản
            </h2>

            <div className="grid grid-cols-1 gap-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Tên trạm sạc <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  name="name"
                  value={formData.name}
                  onChange={handleChange}
                  className="w-full px-4 py-2 border border-gray-300 rounded-md focus:ring-2 focus:ring-orange-500 focus:border-transparent"
                  placeholder="VD: Trạm sạc EV City Center"
                  required
                  maxLength={255}
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Địa chỉ <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  name="address"
                  value={formData.address}
                  onChange={handleChange}
                  className="w-full px-4 py-2 border border-gray-300 rounded-md focus:ring-2 focus:ring-orange-500 focus:border-transparent"
                  placeholder="VD: 123 Nguyễn Huệ, Quận 1, TP.HCM"
                  required
                  maxLength={300}
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Mô tả
                </label>
                <textarea
                  name="description"
                  value={formData.description}
                  onChange={handleChange}
                  className="w-full px-4 py-2 border border-gray-300 rounded-md focus:ring-2 focus:ring-orange-500 focus:border-transparent"
                  placeholder="Mô tả về trạm sạc..."
                  rows={4}
                  maxLength={2000}
                />
              </div>
            </div>
          </div>

          {/* Location */}
          <div className="mb-6">
            <h2 className="text-xl font-semibold mb-4 text-gray-800">
              Vị trí địa lý
            </h2>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Vĩ độ (Latitude)
                </label>
                <input
                  type="number"
                  name="latitude"
                  value={formData.latitude}
                  onChange={handleChange}
                  step="any"
                  className="w-full px-4 py-2 border border-gray-300 rounded-md focus:ring-2 focus:ring-orange-500 focus:border-transparent"
                  placeholder="VD: 10.762622"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Kinh độ (Longitude)
                </label>
                <input
                  type="number"
                  name="longitude"
                  value={formData.longitude}
                  onChange={handleChange}
                  step="any"
                  className="w-full px-4 py-2 border border-gray-300 rounded-md focus:ring-2 focus:ring-orange-500 focus:border-transparent"
                  placeholder="VD: 106.660172"
                />
              </div>
            </div>
          </div>

          {/* Layout Information */}
          <div className="mb-6">
            <h2 className="text-xl font-semibold mb-4 text-gray-800">
              Thông tin sơ đồ
            </h2>

            <div className="grid grid-cols-1 gap-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  URL hình sơ đồ
                </label>
                <input
                  type="text"
                  name="layoutImageUrl"
                  value={formData.layoutImageUrl}
                  onChange={handleChange}
                  className="w-full px-4 py-2 border border-gray-300 rounded-md focus:ring-2 focus:ring-orange-500 focus:border-transparent"
                  placeholder="https://example.com/layout.png"
                  maxLength={500}
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    Chiều rộng sơ đồ (m)
                  </label>
                  <input
                    type="number"
                    name="layoutWidth"
                    value={formData.layoutWidth}
                    onChange={handleChange}
                    step="any"
                    className="w-full px-4 py-2 border border-gray-300 rounded-md focus:ring-2 focus:ring-orange-500 focus:border-transparent"
                    placeholder="VD: 50"
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    Chiều cao sơ đồ (m)
                  </label>
                  <input
                    type="number"
                    name="layoutHeight"
                    value={formData.layoutHeight}
                    onChange={handleChange}
                    step="any"
                    className="w-full px-4 py-2 border border-gray-300 rounded-md focus:ring-2 focus:ring-orange-500 focus:border-transparent"
                    placeholder="VD: 30"
                  />
                </div>
              </div>
            </div>
          </div>

          {/* Images */}
          <div className="mb-6">
            <h2 className="text-xl font-semibold mb-4 text-gray-800">
              Hình ảnh trạm
            </h2>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                URL hình ảnh (mỗi dòng một URL)
              </label>
              <textarea
                name="imageUrls"
                value={formData.imageUrls}
                onChange={handleChange}
                className="w-full px-4 py-2 border border-gray-300 rounded-md focus:ring-2 focus:ring-orange-500 focus:border-transparent font-mono text-sm"
                placeholder="https://example.com/image1.jpg&#10;https://example.com/image2.jpg&#10;https://example.com/image3.jpg"
                rows={5}
              />
              <p className="text-xs text-gray-500 mt-1">
                Nhập mỗi URL trên một dòng riêng biệt
              </p>
            </div>
          </div>

          {/* Operating Hours */}
          <div className="mb-6">
            <h2 className="text-xl font-semibold mb-4 text-gray-800">
              Giờ hoạt động
            </h2>

            <div className="space-y-3">
              {formData.operatingHours.map((hour, index) => (
                <div
                  key={hour.dayOfWeek}
                  className="grid grid-cols-12 gap-4 items-center"
                >
                  <div className="col-span-2">
                    <span className="text-sm font-medium text-gray-700">
                      {dayNames[hour.dayOfWeek]}
                    </span>
                  </div>

                  <div className="col-span-2">
                    <select
                      value={hour.isClosed.toString()}
                      onChange={(e) =>
                        handleOperatingHourChange(
                          index,
                          "isClosed",
                          e.target.value,
                        )
                      }
                      className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:ring-2 focus:ring-orange-500 focus:border-transparent"
                    >
                      <option value="false">Mở cửa</option>
                      <option value="true">Đóng cửa</option>
                    </select>
                  </div>

                  {!hour.isClosed && (
                    <>
                      <div className="col-span-3">
                        <input
                          type="time"
                          value={hour.openTime}
                          onChange={(e) =>
                            handleOperatingHourChange(
                              index,
                              "openTime",
                              e.target.value,
                            )
                          }
                          className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:ring-2 focus:ring-orange-500 focus:border-transparent"
                        />
                      </div>

                      <div className="col-span-1 text-center text-gray-500">
                        đến
                      </div>

                      <div className="col-span-3">
                        <input
                          type="time"
                          value={hour.closeTime}
                          onChange={(e) =>
                            handleOperatingHourChange(
                              index,
                              "closeTime",
                              e.target.value,
                            )
                          }
                          className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:ring-2 focus:ring-orange-500 focus:border-transparent"
                        />
                      </div>
                    </>
                  )}
                </div>
              ))}
            </div>
          </div>

          {/* Actions */}
          <div className="flex gap-4 justify-end pt-6 border-t">
            <Button
              type="button"
              onClick={() => navigate("/stations")}
              className="bg-gray-500 hover:bg-gray-600"
              disabled={submitting}
            >
              Hủy
            </Button>
            <Button
              type="submit"
              className="bg-orange-500 hover:bg-orange-600"
              disabled={submitting}
            >
              {submitting ? "Đang lưu..." : "Cập nhật trạm sạc"}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
