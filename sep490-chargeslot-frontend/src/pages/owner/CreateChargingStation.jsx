import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { createChargingStationSchema } from "@/schemas/createChargingStationSchema";
import { instance } from "@/lib/httpRequest";
// stationPricingApi not needed here — backend CreateFromFormAsync handles pricing from FormData
import { useFieldArray, useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router-dom";
import { useState, useRef, useEffect } from "react";
import { showToast } from "@/components/Toast";
import { MapContainer, TileLayer, Marker, useMapEvents } from "react-leaflet";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import TimePicker24h from "@/components/TimePicker24h";

/* Fix default marker icon */
delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png",
  iconUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png",
  shadowUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png",
});

/* ─── Map helpers ─── */
function MapFlyTo({ lat, lng }) {
  const map = useMapEvents({});
  useEffect(() => {
    if (lat && lng) map.flyTo([lat, lng], 17, { duration: 1.2 });
  }, [lat, lng, map]);
  return null;
}

function LocationMarker({ pos, onSelect }) {
  useMapEvents({
    click: async (e) => {
      const { lat, lng } = e.latlng;
      onSelect(lat, lng, null); // null = will reverse geocode below
      try {
        const res = await fetch(`https://nominatim.openstreetmap.org/reverse?lat=${lat}&lon=${lng}&format=json&accept-language=vi`);
        const data = await res.json();
        onSelect(lat, lng, data.display_name || "");
      } catch {
        onSelect(lat, lng, "");
      }
    },
  });
  return pos ? <Marker position={pos} /> : null;
}

/* ─── Map Picker with Search ─── */
function MapPicker({ lat, lng, onSelect }) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState([]);
  const [searching, setSearching] = useState(false);
  const [gettingLocation, setGettingLocation] = useState(false);
  const [markerPos, setMarkerPos] = useState(null);
  const debounceRef = useRef(null);

  // Get current location
  function handleGetLocation() {
    if (!navigator.geolocation) {
      alert("Trình duyệt không hỗ trợ lấy vị trí");
      return;
    }
    setGettingLocation(true);
    navigator.geolocation.getCurrentPosition(async (position) => {
      const cLat = position.coords.latitude;
      const cLng = position.coords.longitude;
      setMarkerPos([cLat, cLng]);
      try {
        const res = await fetch(`https://nominatim.openstreetmap.org/reverse?lat=${cLat}&lon=${cLng}&format=json&accept-language=vi`);
        const data = await res.json();
        const addr = data.display_name || "Vị trí hiện tại";
        setQuery(addr);
        onSelect(cLat, cLng, addr);
      } catch {
        const addr = "Vị trí hiện tại";
        setQuery(addr);
        onSelect(cLat, cLng, addr);
      }
      setGettingLocation(false);
    }, () => {
      alert("Không thể lấy vị trí hiện tại. Vui lòng cấp quyền.");
      setGettingLocation(false);
    }, { timeout: 10000, enableHighAccuracy: true });
  }

  // Debounced search
  function handleQueryChange(value) {
    setQuery(value);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (value.trim().length < 3) { setResults([]); return; }
    debounceRef.current = setTimeout(async () => {
      setSearching(true);
      try {
        const res = await fetch(
          `https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(value)}&format=json&limit=5&accept-language=vi&countrycodes=vn`
        );
        const data = await res.json();
        setResults(data);
      } catch { setResults([]); }
      setSearching(false);
    }, 400);
  }

  function handleSelectResult(item) {
    const rLat = parseFloat(item.lat);
    const rLng = parseFloat(item.lon);
    setMarkerPos([rLat, rLng]);
    setQuery(item.display_name);
    setResults([]);
    onSelect(rLat, rLng, item.display_name);
  }

  function handleMapClick(cLat, cLng, addr) {
    setMarkerPos([cLat, cLng]);
    if (addr !== null) setQuery(addr);
    onSelect(cLat, cLng, addr);
  }

  return (
    <div style={{ position: "relative" }}>
      {/* Search box & Get Current Location */}
      <div style={{ display: "flex", gap: 8, marginBottom: 8, position: "relative" }}>
        <div style={{ position: "relative", flex: 1 }}>
          <input
            type="text"
            value={query}
            onChange={(e) => handleQueryChange(e.target.value)}
            placeholder="🔍 Tìm kiếm địa chỉ"
            style={{
              width: "100%", padding: "10px 14px 10px 14px", borderRadius: 12,
              border: "1.5px solid #e2e8f0", fontSize: 14, outline: "none",
              boxSizing: "border-box", background: "#fff",
              transition: "border-color 0.2s",
            }}
            onFocus={(e) => (e.target.style.borderColor = "#f97316")}
            onBlur={(e) => (e.target.style.borderColor = "#e2e8f0")}
          />
          {searching && (
            <span style={{ position: "absolute", right: 12, top: "50%", transform: "translateY(-50%)", fontSize: 12, color: "#94a3b8" }}>
              Đang tìm...
            </span>
          )}
          {/* Dropdown results */}
          {results.length > 0 && (
            <div style={{
              position: "absolute", top: "100%", left: 0, right: 0, zIndex: 1000,
              background: "#fff", borderRadius: 12, border: "1px solid #e2e8f0",
              boxShadow: "0 8px 24px rgba(0,0,0,0.12)", marginTop: 4,
              maxHeight: 220, overflowY: "auto",
            }}>
              {results.map((item, idx) => (
                <button
                  key={item.place_id || idx}
                  type="button"
                  onClick={() => handleSelectResult(item)}
                  style={{
                    width: "100%", textAlign: "left", padding: "10px 14px",
                    border: "none", background: "transparent", cursor: "pointer",
                    fontSize: 13, color: "#1e293b", borderBottom: idx < results.length - 1 ? "1px solid #f1f5f9" : "none",
                    display: "flex", alignItems: "flex-start", gap: 8,
                    transition: "background 0.15s",
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.background = "#fff7ed")}
                  onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                >
                  <span style={{ flexShrink: 0, fontSize: 16, marginTop: 1 }}>📍</span>
                  <span style={{ lineHeight: 1.4 }}>{item.display_name}</span>
                </button>
              ))}
            </div>
          )}
        </div>
        <button
          type="button"
          onClick={handleGetLocation}
          disabled={gettingLocation}
          style={{
            flexShrink: 0, padding: "0 16px", height: 42, borderRadius: 12, border: "none",
            background: gettingLocation ? "#e2e8f0" : "linear-gradient(135deg, #3b82f6, #2563eb)",
            color: gettingLocation ? "#94a3b8" : "#fff", fontWeight: 600, fontSize: 13,
            cursor: gettingLocation ? "not-allowed" : "pointer", display: "flex", alignItems: "center", gap: 6,
            boxShadow: gettingLocation ? "none" : "0 2px 8px rgba(59,130,246,0.25)", transition: "all 0.2s"
          }}
        >
          {gettingLocation ? (
            <>
              <div style={{ width: 14, height: 14, border: "2px solid #94a3b8", borderTopColor: "transparent", borderRadius: "50%", animation: "spin 1s linear infinite" }} />
              Đang lấy...
            </>
          ) : (
            <>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <path d="M12 2a10 10 0 1 0 10 10H12V2z" /><path d="M12 12L2.1 12" /><path d="M12 12L12 22" /><path d="M12 12L21.9 12" /><circle cx="12" cy="12" r="3" />
              </svg>
              Vị trí của tôi
            </>
          )}
        </button>
      </div>

      {/* Map */}
      <div style={{ height: 300, borderRadius: 16, overflow: "hidden", border: "2px solid #e2e8f0" }}>
        <MapContainer center={[lat, lng]} zoom={14} style={{ height: "100%", width: "100%" }} scrollWheelZoom>
          <TileLayer
            attribution='&copy; <a href="https://www.google.com/maps">Google Maps</a>'
            url="https://mt1.google.com/vt/lyrs=m&x={x}&y={y}&z={z}"
          />
          <LocationMarker pos={markerPos} onSelect={handleMapClick} />
          {markerPos && <MapFlyTo lat={markerPos[0]} lng={markerPos[1]} />}
        </MapContainer>
      </div>
    </div>
  );
}

const dayOptions = [
  { value: 0, label: "Chủ nhật" },
  { value: 1, label: "Thứ 2" },
  { value: 2, label: "Thứ 3" },
  { value: 3, label: "Thứ 4" },
  { value: 4, label: "Thứ 5" },
  { value: 5, label: "Thứ 6" },
  { value: 6, label: "Thứ 7" },
];

const defaultOperatingHours = dayOptions.map((day) => ({
  dayOfWeek: day.value,
  isClosed: false,
  openTime: "06:00:00",
  closeTime: "23:00:00",
}));

const createChargingStation = async (payload) => {
  const isFormData = payload instanceof FormData;
  const response = await instance.post(
    "/stations",
    payload,
    isFormData ? { headers: { "Content-Type": "multipart/form-data" } } : undefined,
  );
  return response.data;
};

const getErrorMessage = (error) => {
  const data = error?.response?.data;
  if (typeof data === "string") return data;
  if (data?.error) return data.error;
  if (data?.title) return data.title;
  if (data?.errors) {
    const firstEntry = Object.values(data.errors)[0];
    if (Array.isArray(firstEntry) && firstEntry.length > 0) return firstEntry[0];
  }
  return "Tạo trạm sạc thất bại. Vui lòng kiểm tra lại dữ liệu.";
};

function FieldError({ message }) {
  if (!message) return null;
  return <p className="mt-1 text-sm text-red-600">{message}</p>;
}

/* ─── Slot color for the visual grid ─── */
const SLOT_COLOR = "#f97316";
const SLOT_SELECTED = "#ea580c";

export default function CreateChargingStation() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [selectedSlotIdx, setSelectedSlotIdx] = useState(null);
  const pricingRulesRef = useRef([]);

  const {
    control,
    handleSubmit,
    register,
    reset,
    setError,
    watch,
    setValue,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(createChargingStationSchema),
    defaultValues: {
      name: "",
      address: "",
      description: "",
      latitude: 10.7295,
      longitude: 106.7218,
      layoutImageUrl: "",
      layoutWidth: 6,
      layoutHeight: 4,
      operatingHours: defaultOperatingHours,
      slots: [],
      stationPricing: [],
    },
  });

  const { fields, append, remove } = useFieldArray({ control, name: "slots" });
  const operatingHours = watch("operatingHours");
  const layoutWidth = watch("layoutWidth") || 6;
  const layoutHeight = watch("layoutHeight") || 4;
  const slots = watch("slots") || [];

  // Tính giờ đóng cửa sớm nhất (strictest) trong các ngày không đóng cửa
  // closeTime format: "HH:MM:SS" hoặc "HH:MM" → lấy 5 ký tự đầu
  const latestCloseTime = (() => {
    if (!operatingHours || operatingHours.length === 0) return null;
    const closeTimes = operatingHours
      .filter((h) => !h.isClosed && h.closeTime)
      .map((h) => String(h.closeTime).substring(0, 5));
    return closeTimes.length > 0 ? closeTimes.reduce((min, t) => t < min ? t : min) : null;
  })();

  const [stationImages, setStationImages] = useState([]);

  const createStationMutation = useMutation({
    mutationFn: createChargingStation,
    onSuccess: () => {
      // Backend CreateFromFormAsync already saves station-level pricing from FormData
      queryClient.invalidateQueries({ queryKey: ["owner-stations"] });
      showToast.success("Tạo trạm sạc thành công!");
      reset();
      navigate("/stations");
    },
    onError: (error) => {
      const response = error?.response;
      const data = response?.data;
      console.error("❌ Tạo trạm sạc lỗi:", { status: response?.status, data });
      let msg = "Tạo trạm sạc thất bại.";
      if (data) {
        if (typeof data === "string") {
          msg = data;
        } else if (data.errors) {
          // Validation errors from ASP.NET
          const details = Object.entries(data.errors)
            .map(([field, msgs]) => `${field}: ${Array.isArray(msgs) ? msgs.join(", ") : msgs}`)
            .join(" | ");
          msg = `Lỗi validate: ${details}`;
        } else if (data.title) {
          msg = data.title + (data.detail ? ` — ${data.detail}` : "");
        } else if (data.message) {
          msg = data.message;
        }
      } else if (error?.message) {
        msg = error.message;
      }
      setError("root.serverError", { type: "server", message: msg });
    },
  });

  const onSubmit = (data) => {
    if (!data.slots || data.slots.length === 0) {
      setError("root.serverError", { type: "manual", message: "Vui lòng thêm ít nhất 1 trụ sạc bằng cách nhấn vào ô trống trên mặt bằng." });
      return;
    }

    // Validate khung giờ pricing
    const timeRegex = /^([01]\d|2[0-3]):([0-5]\d)$/;
    const pricing = data.stationPricing || [];
    for (let i = 0; i < pricing.length; i++) {
      const rule = pricing[i];
      if (!rule.endTime) {
        setError("root.serverError", { type: "manual", message: `Khung giờ ${i + 1}: Vui lòng nhập giờ kết thúc.` });
        return;
      }
      if (!timeRegex.test(rule.endTime)) {
        setError("root.serverError", { type: "manual", message: `Khung giờ ${i + 1}: Thời gian "${rule.endTime}" không hợp lệ! Giờ từ 00–23, phút từ 00–59.` });
        return;
      }
      const autoStart = i === 0 ? "00:00" : pricing[i - 1].endTime;
      if (rule.endTime <= autoStart) {
        setError("root.serverError", { type: "manual", message: `Khung giờ ${i + 1}: Giờ kết thúc phải sau ${autoStart}.` });
        return;
      }
      if (latestCloseTime && latestCloseTime !== "00:00" && rule.endTime > latestCloseTime) {
        setError("root.serverError", { type: "manual", message: `Khung giờ ${i + 1}: Giờ kết thúc (${rule.endTime}) vượt giờ đóng cửa (${latestCloseTime})!` });
        return;
      }
    }

    // Save station-level pricing
    const stationPricing = data.stationPricing || [];
    pricingRulesRef.current = stationPricing.length > 0 ? stationPricing : [];

    // Build FormData for multipart upload
    const fd = new FormData();
    fd.append("name", data.name);
    fd.append("address", data.address || "Chưa chọn địa chỉ");
    if (data.description?.trim()) fd.append("description", data.description.trim());
    if (data.latitude) fd.append("latitude", Number(data.latitude));
    if (data.longitude) fd.append("longitude", Number(data.longitude));
    fd.append("layoutWidth", Number(data.layoutWidth));
    fd.append("layoutHeight", Number(data.layoutHeight));

    // Images
    stationImages.forEach((file) => fd.append("images", file));

    // Operating hours
    data.operatingHours.forEach((item, i) => {
      fd.append(`operatingHours[${i}].dayOfWeek`, Number(item.dayOfWeek));
      fd.append(`operatingHours[${i}].isClosed`, item.isClosed);
      if (!item.isClosed && item.openTime) fd.append(`operatingHours[${i}].openTime`, item.openTime);
      if (!item.isClosed && item.closeTime) fd.append(`operatingHours[${i}].closeTime`, item.closeTime);
    });

    // Slots
    data.slots.forEach((slot, i) => {
      fd.append(`slots[${i}].slotName`, slot.slotName);
      fd.append(`slots[${i}].positionX`, Number(slot.positionX));
      fd.append(`slots[${i}].positionY`, Number(slot.positionY));
    });

    // Station-level pricing — backend CreateFromFormAsync reads these fields
    stationPricing.forEach((rule, i) => {
      fd.append(`stationPricing[${i}].startTime`, rule.startTime);
      fd.append(`stationPricing[${i}].endTime`, rule.endTime);
      fd.append(`stationPricing[${i}].pricePerHour`, Number(rule.pricePerHour));
    });

    console.log("📦 FormData entries:");
    for (const [k, v] of fd.entries()) console.log(`  ${k}:`, v);
    createStationMutation.mutate(fd);
  };

  /* ═══ Click empty cell to add a slot ═══ */
  function handleCellClick(x, y) {
    const existingIdx = slots.findIndex((s) => Number(s.positionX) === x && Number(s.positionY) === y);
    if (existingIdx >= 0) {
      setSelectedSlotIdx(existingIdx);
      return;
    }
    // Add new slot at this position
    const slotNum = fields.length + 1;
    append({
      slotName: `${String.fromCharCode(64 + y)}${x}`,
      connectorType: "CCS2",
      powerKw: 50,
      positionX: x,
      positionY: y,
    });
    setSelectedSlotIdx(fields.length); // select the newly added one
  }

  function handleRemoveSlot(idx) {
    remove(idx);
    setSelectedSlotIdx(null);
  }

  return (
    <div className="min-h-screen bg-slate-100 px-6 pt-20 pb-8 text-slate-900">
      <div className="mx-auto max-w-6xl">
        {/* Header */}
        <div className="mb-6 flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-white px-6 py-5 shadow-sm ring-1 ring-slate-200">
          <div>
            <p className="text-sm font-medium uppercase tracking-[0.18em] text-orange-500">Owner Dashboard</p>
            <h1 className="mt-2 text-3xl font-bold">Tạo trạm sạc mới</h1>
            <p className="mt-2 text-sm text-slate-600">Khai báo thông tin, mặt bằng và nhấn vào ô trống để đặt trụ sạc.</p>
          </div>
          <Link to="/stations" className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:border-slate-400 hover:bg-slate-50">
            Quay lại danh sách
          </Link>
        </div>

        <form className="space-y-6" onSubmit={handleSubmit(onSubmit)}>
          {/* ═══ ROW 1: Basic info + Layout dimensions ═══ */}
          <div className="grid gap-6 lg:grid-cols-[1.2fr_0.8fr]">
            <section className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
              <h2 className="text-xl font-semibold">Thông tin cơ bản</h2>
              <div className="mt-5 grid gap-5">
                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-700">Tên trạm</label>
                  <input {...register("name")} className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400" placeholder="Ví dụ: Trạm Sạc Xe Máy Quận 7" />
                  <FieldError message={errors.name?.message} />
                </div>
                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-700">Mô tả</label>
                  <textarea {...register("description")} rows={2} className="w-full rounded-xl border border-slate-300 px-4 py-3 outline-none transition focus:border-orange-400" placeholder="Mô tả vị trí, tiện ích..." />
                </div>

                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-700">📍 Chọn vị trí trên bản đồ</label>
                  <MapPicker
                    lat={watch("latitude") || 10.7295}
                    lng={watch("longitude") || 106.7218}
                    onSelect={(lat, lng, addr) => {
                      setValue("latitude", lat);
                      setValue("longitude", lng);
                      if (addr) setValue("address", addr);
                    }}
                  />
                  <div className="mt-4">
                    <label className="mb-2 block text-sm font-medium text-slate-700">Địa chỉ cụ thể (có thể chỉnh sửa)</label>
                    <input {...register("address")} className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400" placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/thành phố..." />
                  </div>
                  <input type="hidden" {...register("latitude")} />
                  <input type="hidden" {...register("longitude")} />
                  <FieldError message={errors.address?.message} />
                </div>
              </div>
            </section>

            <section className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
              <h2 className="text-xl font-semibold">Kích thước mặt bằng</h2>
              <p className="mt-1 text-sm text-slate-500">Chọn số cột × số hàng cho bản đồ trụ sạc</p>
              <div className="mt-5 grid gap-5 grid-cols-2">
                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-700">Số cột (chiều rộng)</label>
                  <input type="number" min="1" max="20" {...register("layoutWidth", { valueAsNumber: true })} className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400" />
                </div>
                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-700">Số hàng (chiều cao)</label>
                  <input type="number" min="1" max="20" {...register("layoutHeight", { valueAsNumber: true })} className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400" />
                </div>
              </div>
              <div className="mt-4 rounded-xl border border-dashed border-slate-300 bg-slate-50 p-3 text-sm text-slate-600">
                📐 Mặt bằng hiện tại: <strong>{layoutWidth} cột × {layoutHeight} hàng</strong> = {layoutWidth * layoutHeight} ô
                <br />Đã đặt: <strong className="text-orange-600">{slots.length}</strong> trụ sạc
              </div>

              <div className="mt-5">
                <label className="mb-2 block text-sm font-medium text-slate-700">📷 Ảnh trạm sạc</label>
                <input
                  type="file"
                  accept="image/*"
                  multiple
                  onChange={(e) => {
                    const files = Array.from(e.target.files || []);
                    setStationImages((prev) => [...prev, ...files]);
                    e.target.value = "";
                  }}
                  className="h-11 w-full rounded-xl border border-slate-300 px-4 py-2 text-sm outline-none transition focus:border-orange-400 file:mr-3 file:rounded-lg file:border-0 file:bg-orange-50 file:px-3 file:py-1 file:text-sm file:font-semibold file:text-orange-600"
                />
                {stationImages.length > 0 && (
                  <div className="mt-3 flex flex-wrap gap-2">
                    {stationImages.map((file, i) => (
                      <div key={i} className="relative group">
                        <img
                          src={URL.createObjectURL(file)}
                          alt={`Preview ${i + 1}`}
                          className="h-16 w-16 rounded-lg object-cover border border-slate-200"
                        />
                        <button
                          type="button"
                          onClick={() => setStationImages((prev) => prev.filter((_, idx) => idx !== i))}
                          className="absolute -top-1 -right-1 w-5 h-5 bg-red-500 text-white rounded-full text-xs flex items-center justify-center opacity-0 group-hover:opacity-100 transition cursor-pointer"
                        >
                          ✕
                        </button>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </section>
          </div>

          {/* ═══ ROW 2: Operating Hours ═══ */}
          <section className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <h2 className="text-xl font-semibold">Giờ hoạt động</h2>
            <p className="mt-1 text-sm text-slate-600">Cấu hình cho từng ngày trong tuần.</p>
            <div className="mt-5 space-y-3">
              {dayOptions.map((day, index) => {
                const isClosed = operatingHours?.[index]?.isClosed;
                return (
                  <div key={day.value} className="grid gap-4 rounded-xl border border-slate-200 p-3 md:grid-cols-[1.2fr_1fr_1fr_auto] md:items-center">
                    <div>
                      <p className="font-medium text-slate-800 text-sm">{day.label}</p>
                      <label className="mt-1 inline-flex items-center gap-2 text-xs text-slate-600">
                        <input type="checkbox" {...register(`operatingHours.${index}.isClosed`)} />
                        Đóng cửa
                      </label>
                    </div>
                    <div>
                      <TimePicker24h
                        value={(operatingHours?.[index]?.openTime || "06:00").substring(0,5)}
                        disabled={isClosed}
                        onChange={(v) => setValue(`operatingHours.${index}.openTime`, v)}
                      />
                    </div>
                    <div>
                      <TimePicker24h
                        value={(operatingHours?.[index]?.closeTime || "23:00").substring(0,5)}
                        disabled={isClosed}
                        minAfter={isClosed ? undefined : (operatingHours?.[index]?.openTime || "06:00").substring(0,5)}
                        onChange={(v) => setValue(`operatingHours.${index}.closeTime`, v)}
                      />
                    </div>
                    <input type="hidden" {...register(`operatingHours.${index}.dayOfWeek`)} />
                  </div>
                );
              })}
            </div>
          </section>

          {/* ═══ ROW 3: Visual Slot Grid (Cinema-style) ═══ */}
          <section className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <div className="flex flex-wrap items-center justify-between gap-3 mb-4">
              <div>
                <h2 className="text-xl font-semibold">Mặt bằng trụ sạc</h2>
                <p className="mt-1 text-sm text-slate-600">
                  Nhấn vào ô trống để đặt trụ sạc • Nhấn vào trụ để chỉnh sửa • {slots.length} trụ đã đặt
                </p>
              </div>
            </div>

            <div className="flex gap-6 flex-col lg:flex-row">
              {/* Grid */}
              <div className="flex-1">
                <div
                  style={{
                    display: "grid",
                    gridTemplateColumns: `40px repeat(${layoutWidth}, 1fr)`,
                    gap: 4,
                    background: "#f1f5f9",
                    borderRadius: 16,
                    padding: 12,
                    border: "2px solid #e2e8f0",
                    maxWidth: 700,
                  }}
                >
                  {/* Column headers */}
                  <div />
                  {Array.from({ length: layoutWidth }).map((_, col) => (
                    <div key={`h-${col}`} style={{ textAlign: "center", fontSize: 11, fontWeight: 700, color: "#94a3b8", padding: "4px 0" }}>
                      {col + 1}
                    </div>
                  ))}

                  {/* Grid rows */}
                  {Array.from({ length: layoutHeight }).map((_, row) => (
                    <>
                      {/* Row label */}
                      <div key={`r-${row}`} style={{ display: "flex", alignItems: "center", justifyContent: "center", fontSize: 11, fontWeight: 700, color: "#94a3b8" }}>
                        {String.fromCharCode(65 + row)}
                      </div>

                      {/* Cells */}
                      {Array.from({ length: layoutWidth }).map((_, col) => {
                        const x = col + 1;
                        const y = row + 1;
                        const slotIdx = slots.findIndex((s) => Number(s.positionX) === x && Number(s.positionY) === y);
                        const slot = slotIdx >= 0 ? slots[slotIdx] : null;
                        const isSelected = selectedSlotIdx === slotIdx && slot;

                        if (slot) {
                          return (
                            <button
                              type="button"
                              key={`${x}-${y}`}
                              onClick={() => setSelectedSlotIdx(slotIdx)}
                              style={{
                                background: isSelected ? SLOT_SELECTED : SLOT_COLOR,
                                color: "#fff",
                                borderRadius: 10,
                                border: isSelected ? "3px solid #1e293b" : "2px solid #c2410c",
                                display: "flex",
                                flexDirection: "column",
                                alignItems: "center",
                                justifyContent: "center",
                                cursor: "pointer",
                                transition: "all .15s",
                                transform: isSelected ? "scale(1.08)" : "scale(1)",
                                boxShadow: isSelected ? "0 4px 12px rgba(0,0,0,0.25)" : "0 1px 4px rgba(0,0,0,0.1)",
                                minHeight: 56,
                                padding: "4px 2px",
                              }}
                              title={slot.slotName}
                            >
                              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                                <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
                              </svg>
                              <span style={{ fontSize: 10, fontWeight: 700, marginTop: 2, lineHeight: 1 }}>
                                {slot.slotName}
                              </span>
                            </button>
                          );
                        }

                        // Empty cell — clickable to add
                        return (
                          <button
                            type="button"
                            key={`${x}-${y}`}
                            onClick={() => handleCellClick(x, y)}
                            style={{
                              background: "#e8ecf1",
                              borderRadius: 10,
                              border: "2px dashed #cbd5e1",
                              display: "flex",
                              alignItems: "center",
                              justifyContent: "center",
                              cursor: "pointer",
                              minHeight: 56,
                              transition: "all .15s",
                            }}
                            onMouseEnter={(e) => { e.currentTarget.style.background = "#fed7aa"; e.currentTarget.style.borderColor = "#f97316"; }}
                            onMouseLeave={(e) => { e.currentTarget.style.background = "#e8ecf1"; e.currentTarget.style.borderColor = "#cbd5e1"; }}
                            title={`Nhấn để thêm trụ sạc tại ${String.fromCharCode(65 + row)}${col + 1}`}
                          >
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#94a3b8" strokeWidth="2">
                              <line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />
                            </svg>
                          </button>
                        );
                      })}
                    </>
                  ))}
                </div>

                {/* Legend */}
                <div className="flex gap-4 mt-3 flex-wrap text-xs text-slate-500">
                  <div className="flex items-center gap-1.5">
                    <div style={{ width: 12, height: 12, borderRadius: 4, background: SLOT_COLOR }} />
                    Trụ sạc đã đặt
                  </div>
                  <div className="flex items-center gap-1.5">
                    <div style={{ width: 12, height: 12, borderRadius: 4, background: "#e8ecf1", border: "1.5px dashed #cbd5e1" }} />
                    Ô trống (nhấn để thêm)
                  </div>
                </div>
              </div>

              {/* Slot detail — just show position + delete */}
              <div className="lg:w-[260px] flex-shrink-0">
                <h4 className="text-base font-bold text-slate-800 mb-3">Ổ sạc đã chọn</h4>
                {selectedSlotIdx !== null && slots[selectedSlotIdx] ? (
                  <div className="bg-slate-50 rounded-xl p-4 border border-slate-200 space-y-3">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <div style={{ width: 32, height: 32, borderRadius: 8, background: SLOT_COLOR, display: "flex", alignItems: "center", justifyContent: "center" }}>
                          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth="2.5"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" /></svg>
                        </div>
                        <span className="font-bold text-slate-900">
                          {String.fromCharCode(64 + Number(slots[selectedSlotIdx].positionY))}{slots[selectedSlotIdx].positionX}
                        </span>
                      </div>
                      <button
                        type="button"
                        onClick={() => handleRemoveSlot(selectedSlotIdx)}
                        className="text-xs font-semibold text-red-500 hover:text-red-700 cursor-pointer"
                      >
                        🗑️ Xóa
                      </button>
                    </div>
                    <p className="text-xs text-slate-400">
                      Tọa độ: Hàng {String.fromCharCode(64 + Number(slots[selectedSlotIdx].positionY))}, Cột {slots[selectedSlotIdx].positionX}
                    </p>

                    <input type="hidden" {...register(`slots.${selectedSlotIdx}.positionX`)} />
                    <input type="hidden" {...register(`slots.${selectedSlotIdx}.positionY`)} />
                  </div>
                ) : (
                  <div className="bg-slate-50 rounded-xl p-6 border border-dashed border-slate-300 text-center">
                    <div className="text-3xl mb-2 opacity-40">👆</div>
                    <p className="text-sm text-slate-400">
                      {slots.length === 0
                        ? "Nhấn vào ô trống để đặt ổ sạc"
                        : "Chọn ổ sạc để xem vị trí"}
                    </p>
                  </div>
                )}
              </div>
            </div>

            <FieldError message={errors.slots?.message} />
          </section>

          {/* ═══ STATION-LEVEL PRICING ═══ */}
          <section className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <div className="flex items-center justify-between mb-4">
              <div>
                <h2 className="text-xl font-semibold">⏰ Giá theo khung giờ</h2>
                <p className="text-sm text-slate-500 mt-1">Áp dụng chung cho tất cả ổ sạc của trạm</p>
              </div>
              <button
                type="button"
                onClick={() => {
                  const current = watch("stationPricing") || [];
                  const lastEnd = current.length > 0 ? current[current.length - 1].endTime : "00:00";
                  setValue("stationPricing", [
                    ...current,
                    { startTime: lastEnd, endTime: "", pricePerHour: "" },
                  ]);
                }}
                className="text-sm font-semibold text-orange-600 hover:text-orange-700 cursor-pointer"
              >
                + Thêm khung giờ
              </button>
            </div>

            {(() => {
              const pricing = watch("stationPricing") || [];
              if (pricing.length === 0) return (
                <p className="text-sm text-slate-400 italic py-4 text-center">
                  Chưa có khung giờ. Nhấn "+ Thêm khung giờ" để bắt đầu.
                </p>
              );
              return (
                <div className="space-y-2">
                  {pricing.map((rule, rIdx) => {
                    const autoStart = rIdx === 0 ? "00:00" : (pricing[rIdx - 1]?.endTime || "");
                    const timeRegex = /^([01]\d|2[0-3]):([0-5]\d)$/;
                    const isValidFormat = !rule.endTime || timeRegex.test(rule.endTime);
                    const isAfterStart = !rule.endTime || !autoStart || rule.endTime > autoStart;
                    const exceedsClose = isValidFormat && rule.endTime && latestCloseTime && latestCloseTime !== "00:00" && rule.endTime > latestCloseTime;
                    const hasError = rule.endTime && (!isValidFormat || !isAfterStart || exceedsClose);
                    return (
                      <div key={rIdx} className="bg-slate-50 rounded-xl p-3 border border-slate-200">
                        <div className="flex items-center gap-2">
                          <div className="text-center">
                            <div className="text-[10px] text-slate-400 mb-1">Từ</div>
                            <div className="h-9 w-[64px] rounded-lg bg-white border border-slate-200 text-sm flex items-center justify-center font-bold text-slate-700">
                              {autoStart || "--:--"}
                            </div>
                          </div>
                          <span className="text-slate-300 mt-4 text-lg">→</span>
                          <div className="text-center">
                            <div className="text-[10px] text-slate-400 mb-1">Đến</div>
                            <input
                              type="text"
                              value={rule.endTime || ""}
                              placeholder="HH:mm"
                              maxLength={5}
                              onChange={(e) => {
                                let val = e.target.value.replace(/[^0-9:]/g, "");
                                if (val.length === 2 && !val.includes(":")) val += ":";
                                if (val.length > 5) val = val.slice(0, 5);
                                const updated = [...pricing];
                                updated[rIdx] = { ...updated[rIdx], endTime: val, startTime: autoStart };
                                if (val.length === 5 && rIdx + 1 < updated.length) {
                                  updated[rIdx + 1] = { ...updated[rIdx + 1], startTime: val };
                                }
                                setValue("stationPricing", updated);
                              }}
                              className={`h-9 w-[64px] rounded-lg border px-2 text-sm font-semibold text-center outline-none ${hasError ? "border-red-400 text-red-600 bg-red-50" : "border-slate-200 focus:border-orange-400"}`}
                            />
                          </div>
                          <div className="flex-1 text-center">
                            <div className="text-[10px] text-slate-400 mb-1">Giá / giờ (VND)</div>
                            <div className="relative">
                              <input
                                type="number"
                                value={rule.pricePerHour}
                                placeholder="10000"
                                onChange={(e) => {
                                  const updated = [...pricing];
                                  updated[rIdx] = { ...updated[rIdx], pricePerHour: Number(e.target.value), startTime: autoStart };
                                  setValue("stationPricing", updated);
                                }}
                                className="h-9 w-full rounded-lg border border-slate-200 px-3 pr-6 text-sm outline-none focus:border-orange-400"
                              />
                              <span className="absolute right-2 top-1/2 -translate-y-1/2 text-xs text-slate-400">đ</span>
                            </div>
                          </div>
                          <button
                            type="button"
                            onClick={() => {
                              const updated = [...pricing];
                              updated.splice(rIdx, 1);
                              setValue("stationPricing", updated);
                            }}
                            className="text-red-400 hover:text-red-600 cursor-pointer mt-4"
                            title="Xóa"
                          >
                            ✕
                          </button>
                        </div>
                        {hasError && (
                          <p className="text-xs text-red-500 mt-1 font-medium">
                            {!isValidFormat
                              ? "⚠ Thời gian không hợp lệ! Giờ từ 00–23, phút từ 00–59."
                              : exceedsClose
                              ? `⚠ Giờ kết thúc vượt giờ đóng cửa (${latestCloseTime})!`
                              : `⚠ Giờ kết thúc phải sau ${autoStart}`
                            }
                          </p>
                        )}
                      </div>
                    );
                  })}
                </div>
              );
            })()}
          </section>

          {/* ═══ Submit ═══ */}
          <div className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            {errors.root?.serverError?.message && (
              <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                {errors.root.serverError.message}
              </div>
            )}
            <div className="flex flex-wrap items-center justify-end gap-3">
              <Link to="/stations" className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:border-slate-400 hover:bg-slate-50">
                Hủy
              </Link>
              <Button type="submit" className="h-11 bg-orange-500 px-6 text-white hover:bg-orange-600" disabled={createStationMutation.isPending}>
                {createStationMutation.isPending ? "Đang tạo trạm..." : `Tạo trạm sạc (${slots.length} trụ)`}
              </Button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
