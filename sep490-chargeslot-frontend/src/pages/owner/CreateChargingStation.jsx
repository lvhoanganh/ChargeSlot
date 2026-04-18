import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { createChargingStationSchema } from "@/schemas/createChargingStationSchema";
import { instance } from "@/lib/httpRequest";
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
      onSelect(lat, lng, null);
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

function MapPicker({ lat, lng, onSelect }) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState([]);
  const [searching, setSearching] = useState(false);
  const [gettingLocation, setGettingLocation] = useState(false);
  const [markerPos, setMarkerPos] = useState(null);
  const debounceRef = useRef(null);

  function handleGetLocation() {
    if (!navigator.geolocation) { alert("Trình duyệt không hỗ trợ lấy vị trí"); return; }
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
        setQuery("Vị trí hiện tại");
        onSelect(cLat, cLng, "Vị trí hiện tại");
      }
      setGettingLocation(false);
    }, () => { alert("Không thể lấy vị trí hiện tại."); setGettingLocation(false); }, { timeout: 10000, enableHighAccuracy: true });
  }

  function handleQueryChange(value) {
    setQuery(value);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (value.trim().length < 3) { setResults([]); return; }
    debounceRef.current = setTimeout(async () => {
      setSearching(true);
      try {
        const res = await fetch(`https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(value)}&format=json&limit=5&accept-language=vi&countrycodes=vn`);
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
      <div style={{ display: "flex", gap: 8, marginBottom: 8, position: "relative" }}>
        <div style={{ position: "relative", flex: 1 }}>
          <input
            type="text"
            value={query}
            onChange={(e) => handleQueryChange(e.target.value)}
            placeholder=" Tìm kiếm địa chỉ"
            style={{ width: "100%", padding: "10px 14px", borderRadius: 12, border: "1.5px solid #e2e8f0", fontSize: 14, outline: "none", boxSizing: "border-box", background: "#fff" }}
            onFocus={(e) => (e.target.style.borderColor = "#f97316")}
            onBlur={(e) => (e.target.style.borderColor = "#e2e8f0")}
          />
          {searching && <span style={{ position: "absolute", right: 12, top: "50%", transform: "translateY(-50%)", fontSize: 12, color: "#94a3b8" }}>Đang tìm...</span>}
          {results.length > 0 && (
            <div style={{ position: "absolute", top: "100%", left: 0, right: 0, zIndex: 1000, background: "#fff", borderRadius: 12, border: "1px solid #e2e8f0", boxShadow: "0 8px 24px rgba(0,0,0,0.12)", marginTop: 4, maxHeight: 220, overflowY: "auto" }}>
              {results.map((item, idx) => (
                <button key={item.place_id || idx} type="button" onClick={() => handleSelectResult(item)}
                  style={{ width: "100%", textAlign: "left", padding: "10px 14px", border: "none", background: "transparent", cursor: "pointer", fontSize: 13, color: "#1e293b", borderBottom: idx < results.length - 1 ? "1px solid #f1f5f9" : "none", display: "flex", alignItems: "flex-start", gap: 8 }}
                  onMouseEnter={(e) => (e.currentTarget.style.background = "#fff7ed")}
                  onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                >
                  <span style={{ flexShrink: 0 }}></span>
                  <span style={{ lineHeight: 1.4 }}>{item.display_name}</span>
                </button>
              ))}
            </div>
          )}
        </div>
        <button type="button" onClick={handleGetLocation} disabled={gettingLocation}
          style={{ flexShrink: 0, padding: "0 16px", height: 42, borderRadius: 12, border: "none", background: gettingLocation ? "#e2e8f0" : "linear-gradient(135deg, #3b82f6, #2563eb)", color: gettingLocation ? "#94a3b8" : "#fff", fontWeight: 600, fontSize: 13, cursor: gettingLocation ? "not-allowed" : "pointer", display: "flex", alignItems: "center", gap: 6, boxShadow: gettingLocation ? "none" : "0 2px 8px rgba(59,130,246,0.25)" }}>
          {gettingLocation ? "Đang lấy..." : " Vị trí của tôi"}
        </button>
      </div>
      <div style={{ height: 340, borderRadius: 16, overflow: "hidden", border: "2px solid #e2e8f0" }}>
        <MapContainer center={[lat, lng]} zoom={14} style={{ height: "100%", width: "100%" }} scrollWheelZoom>
          <TileLayer attribution='&copy; <a href="https://www.google.com/maps">Google Maps</a>' url="https://mt1.google.com/vt/lyrs=m&x={x}&y={y}&z={z}" />
          <LocationMarker pos={markerPos} onSelect={handleMapClick} />
          {markerPos && <MapFlyTo lat={markerPos[0]} lng={markerPos[1]} />}
        </MapContainer>
      </div>
    </div>
  );
}

const dayOptions = [
  { value: 0, label: "Chủ nhật" }, { value: 1, label: "Thứ 2" }, { value: 2, label: "Thứ 3" },
  { value: 3, label: "Thứ 4" }, { value: 4, label: "Thứ 5" }, { value: 5, label: "Thứ 6" }, { value: 6, label: "Thứ 7" },
];

const defaultOperatingHours = dayOptions.map((day) => ({ dayOfWeek: day.value, isClosed: false, openTime: "06:00:00", closeTime: "23:00:00" }));

const createChargingStation = async (payload) => {
  const isFormData = payload instanceof FormData;
  const response = await instance.post("/stations", payload, isFormData ? { headers: { "Content-Type": "multipart/form-data" } } : undefined);
  return response.data;
};

function FieldError({ message }) {
  if (!message) return null;
  return <p className="mt-1 text-sm text-red-600">{message}</p>;
}

const SLOT_COLOR = "#f97316";
const SLOT_SELECTED = "#ea580c";

// ─────────────────────────────────────────
//  STEPS CONFIG
// ─────────────────────────────────────────
const StepIcons = {
  1: (<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><polyline points="10 9 9 9 8 9"/></svg>),
  2: (<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>),
  3: (<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z"/></svg>),
  4: (<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="1" y="4" width="22" height="16" rx="2" ry="2"/><line x1="1" y1="10" x2="23" y2="10"/></svg>),
};

const CheckIcon = (<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round"><polyline points="20 6 9 17 4 12"/></svg>);

const STEPS = [
  { id: 1, label: "Thông tin", desc: "Tên & mô tả trạm" },
  { id: 2, label: "Vị trí", desc: "Bản đồ & địa chỉ" },
  { id: 3, label: "Mặt bằng", desc: "Giờ hoạt động & trụ sạc" },
  { id: 4, label: "Giá & Hoàn tất", desc: "Giá theo khung giờ" },
];

// ─────────────────────────────────────────
//  STEP INDICATOR
// ─────────────────────────────────────────
function StepIndicator({ current }) {
  return (
    <div style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 0, marginBottom: 40 }}>
      {STEPS.map((step, idx) => {
        const done = current > step.id;
        const active = current === step.id;
        return (
          <div key={step.id} style={{ display: "flex", alignItems: "center" }}>
            <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 6 }}>
              <div style={{
                width: 44, height: 44, borderRadius: "50%",
                background: done ? "#22c55e" : active ? "linear-gradient(135deg, #f97316, #ea580c)" : "#e2e8f0",
                color: done || active ? "#fff" : "#94a3b8",
                display: "flex", alignItems: "center", justifyContent: "center",
                fontWeight: 700,
                boxShadow: active ? "0 4px 16px rgba(249,115,22,0.4)" : "none",
                transition: "all 0.3s",
                border: active ? "3px solid #fff" : "3px solid transparent",
                outline: active ? "2px solid #f97316" : "none",
              }}>
                {done ? CheckIcon : StepIcons[step.id]}
              </div>
              <div style={{ textAlign: "center" }}>
                <div style={{ fontSize: 12, fontWeight: 700, color: active ? "#f97316" : done ? "#22c55e" : "#94a3b8" }}>
                  {step.label}
                </div>
                <div style={{ fontSize: 10, color: "#cbd5e1", maxWidth: 72 }}>{step.desc}</div>
              </div>
            </div>
            {idx < STEPS.length - 1 && (
              <div style={{
                width: 60, height: 2, margin: "0 4px", marginBottom: 28,
                background: done ? "#22c55e" : "#e2e8f0",
                transition: "background 0.3s",
              }} />
            )}
          </div>
        );
      })}
    </div>
  );
}

// ─────────────────────────────────────────
//  MAIN COMPONENT
// ─────────────────────────────────────────
export default function CreateChargingStation() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [step, setStep] = useState(1);
  const [selectedSlotIdx, setSelectedSlotIdx] = useState(null);
  const pricingRulesRef = useRef([]);
  const [stationImages, setStationImages] = useState([]);

  const { control, handleSubmit, register, reset, setError, watch, setValue, trigger, formState: { errors } } = useForm({
    resolver: zodResolver(createChargingStationSchema),
    defaultValues: {
      name: "", address: "", description: "",
      latitude: 10.7295, longitude: 106.7218,
      layoutImageUrl: "", layoutWidth: 6, layoutHeight: 4,
      operatingHours: defaultOperatingHours,
      slots: [], stationPricing: [],
    },
  });

  const { fields, append, remove } = useFieldArray({ control, name: "slots" });
  const operatingHours = watch("operatingHours");
  const layoutWidth = watch("layoutWidth") || 6;
  const layoutHeight = watch("layoutHeight") || 4;
  const slots = watch("slots") || [];

  const latestCloseTime = (() => {
    if (!operatingHours || operatingHours.length === 0) return null;
    const closeTimes = operatingHours.filter((h) => !h.isClosed && h.closeTime).map((h) => String(h.closeTime).substring(0, 5));
    return closeTimes.length > 0 ? closeTimes.reduce((min, t) => t < min ? t : min) : null;
  })();

  const earliestOpenTime = (() => {
    if (!operatingHours || operatingHours.length === 0) return "00:00";
    const openTimes = operatingHours.filter((h) => !h.isClosed && h.openTime).map((h) => String(h.openTime).substring(0, 5));
    return openTimes.length > 0 ? openTimes.reduce((min, t) => t < min ? t : min) : "00:00";
  })();

  const createStationMutation = useMutation({
    mutationFn: createChargingStation,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["owner-stations"] });
      showToast.success("Tạo trạm sạc thành công!");
      reset();
      navigate("/stations");
    },
    onError: (error) => {
      const response = error?.response;
      const data = response?.data;
      let msg = "Tạo trạm sạc thất bại.";
      if (data) {
        if (typeof data === "string") msg = data;
        else if (data.errors) msg = `Lỗi validate: ${Object.entries(data.errors).map(([f, m]) => `${f}: ${Array.isArray(m) ? m.join(", ") : m}`).join(" | ")}`;
        else if (data.title) msg = data.title + (data.detail ? ` — ${data.detail}` : "");
        else if (data.message) msg = data.message;
      } else if (error?.message) msg = error.message;
      setError("root.serverError", { type: "server", message: msg });
    },
  });

  const onSubmit = (data) => {
    if (!data.slots || data.slots.length === 0) {
      setError("root.serverError", { type: "manual", message: "Vui lòng thêm ít nhất 1 trụ sạc." });
      setStep(3);
      return;
    }
    const timeRegex = /^([01]\d|2[0-3]):([0-5]\d)$/;
    const pricing = data.stationPricing || [];
    for (let i = 0; i < pricing.length; i++) {
      const rule = pricing[i];
      if (!rule.endTime || !timeRegex.test(rule.endTime)) {
        setError("root.serverError", { type: "manual", message: `Khung giờ ${i + 1}: Thời gian không hợp lệ!` });
        return;
      }
      const autoStart = i === 0 ? "00:00" : pricing[i - 1].endTime;
      if (rule.endTime <= autoStart) {
        setError("root.serverError", { type: "manual", message: `Khung giờ ${i + 1}: Giờ kết thúc phải sau ${autoStart}.` });
        return;
      }
    }

    const stationPricing = data.stationPricing || [];
    pricingRulesRef.current = stationPricing;

    const fd = new FormData();
    fd.append("name", data.name);
    fd.append("address", data.address || "Chưa chọn địa chỉ");
    if (data.description?.trim()) fd.append("description", data.description.trim());
    if (data.latitude) fd.append("latitude", Number(data.latitude));
    if (data.longitude) fd.append("longitude", Number(data.longitude));
    fd.append("layoutWidth", Number(data.layoutWidth));
    fd.append("layoutHeight", Number(data.layoutHeight));
    stationImages.forEach((file) => fd.append("images", file));
    data.operatingHours.forEach((item, i) => {
      fd.append(`operatingHours[${i}].dayOfWeek`, Number(item.dayOfWeek));
      fd.append(`operatingHours[${i}].isClosed`, item.isClosed);
      if (!item.isClosed && item.openTime) fd.append(`operatingHours[${i}].openTime`, item.openTime);
      if (!item.isClosed && item.closeTime) fd.append(`operatingHours[${i}].closeTime`, item.closeTime);
    });
    data.slots.forEach((slot, i) => {
      fd.append(`slots[${i}].slotName`, slot.slotName);
      fd.append(`slots[${i}].positionX`, Number(slot.positionX));
      fd.append(`slots[${i}].positionY`, Number(slot.positionY));
    });
    stationPricing.forEach((rule, i) => {
      fd.append(`stationPricing[${i}].startTime`, rule.startTime);
      fd.append(`stationPricing[${i}].endTime`, rule.endTime);
      fd.append(`stationPricing[${i}].pricePerHour`, Number(rule.pricePerHour));
    });
    createStationMutation.mutate(fd);
  };

  async function handleNext() {
    let fieldsToValidate = [];
    if (step === 1) fieldsToValidate = ["name", "description"];
    if (step === 2) fieldsToValidate = ["address", "latitude", "longitude"];
    const valid = fieldsToValidate.length > 0 ? await trigger(fieldsToValidate) : true;
    if (valid) setStep((s) => Math.min(s + 1, 4));
  }

  function handleCellClick(x, y) {
    const existingIdx = slots.findIndex((s) => Number(s.positionX) === x && Number(s.positionY) === y);
    if (existingIdx >= 0) { setSelectedSlotIdx(existingIdx); return; }
    append({ slotName: `${String.fromCharCode(64 + y)}${x}`, connectorType: "CCS2", powerKw: 50, positionX: x, positionY: y });
    setSelectedSlotIdx(fields.length);
  }

  function handleRemoveSlot(idx) { remove(idx); setSelectedSlotIdx(null); }

  // ── Shared card style
  const card = { background: "#fff", borderRadius: 20, padding: "28px 32px", boxShadow: "0 2px 12px rgba(0,0,0,0.06)", border: "1px solid #f1f5f9" };
  const label = { fontSize: 13, fontWeight: 600, color: "#374151", display: "block", marginBottom: 6 };
  const input = { height: 44, width: "100%", borderRadius: 12, border: "1.5px solid #e2e8f0", paddingLeft: 14, paddingRight: 14, fontSize: 14, outline: "none", boxSizing: "border-box", background: "#f8fafc", transition: "border-color 0.2s" };

  return (
    <div style={{ minHeight: "100vh", background: "linear-gradient(135deg, #fff7ed 0%, #f8fafc 60%)", paddingTop: 88, paddingBottom: 60, fontFamily: "'Inter', -apple-system, sans-serif" }}>
      <div style={{ maxWidth: 860, margin: "0 auto", padding: "0 20px" }}>

        {/* Header */}
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 32 }}>
          <div>
            <p style={{ fontSize: 12, fontWeight: 700, color: "#f97316", textTransform: "uppercase", letterSpacing: 2, margin: 0 }}>Owner Dashboard</p>
            <h1 style={{ fontSize: 28, fontWeight: 800, color: "#0f172a", margin: "6px 0 4px", letterSpacing: -0.5 }}>Tạo trạm sạc mới</h1>
            <p style={{ fontSize: 14, color: "#64748b", margin: 0 }}>Hoàn thành 4 bước để đăng ký trạm sạc của bạn</p>
          </div>
          <Link to="/stations" style={{ padding: "10px 20px", borderRadius: 12, border: "1.5px solid #e2e8f0", background: "#fff", color: "#64748b", fontSize: 13, fontWeight: 600, textDecoration: "none", display: "flex", alignItems: "center", gap: 6 }}>
            ← Quay lại
          </Link>
        </div>

        {/* Step Indicator */}
        <StepIndicator current={step} />

        <form onSubmit={handleSubmit(onSubmit)}>

          {/* ───────── STEP 1: Thông tin cơ bản ───────── */}
          {step === 1 && (
            <div style={{ ...card, animation: "fadeIn 0.3s ease" }}>
              <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 28 }}>
                <div style={{ width: 44, height: 44, borderRadius: 12, background: "linear-gradient(135deg, #f97316, #ea580c)", display: "flex", alignItems: "center", justifyContent: "center", color: "#fff" }}>
                  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><polyline points="10 9 9 9 8 9"/></svg>
                </div>
                <div>
                  <h2 style={{ margin: 0, fontSize: 20, fontWeight: 800, color: "#0f172a" }}>Thông tin trạm sạc</h2>
                  <p style={{ margin: 0, fontSize: 13, color: "#94a3b8" }}>Đặt tên và mô tả cho trạm sạc của bạn</p>
                </div>
              </div>

              <div style={{ display: "grid", gap: 20 }}>
                <div>
                  <label style={label}>Tên trạm sạc <span style={{ color: "#ef4444" }}>*</span></label>
                  <input {...register("name")} style={input} placeholder="Ví dụ: Trạm Sạc Xe Máy Quận 7" onFocus={(e) => (e.target.style.borderColor = "#f97316")} onBlur={(e) => (e.target.style.borderColor = "#e2e8f0")} />
                  <FieldError message={errors.name?.message} />
                </div>

                <div>
                  <label style={label}>Mô tả trạm</label>
                  <textarea {...register("description")} rows={3} style={{ ...input, height: "auto", paddingTop: 12, paddingBottom: 12, resize: "vertical" }} placeholder="Mô tả vị trí, tiện ích, ghi chú thêm..." onFocus={(e) => (e.target.style.borderColor = "#f97316")} onBlur={(e) => (e.target.style.borderColor = "#e2e8f0")} />
                </div>

                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16 }}>
                  <div>
                    <label style={label}>Số cột (chiều rộng)</label>
                    <input type="number" min="1" max="20" {...register("layoutWidth", { valueAsNumber: true })} style={input} onFocus={(e) => (e.target.style.borderColor = "#f97316")} onBlur={(e) => (e.target.style.borderColor = "#e2e8f0")} />
                  </div>
                  <div>
                    <label style={label}>Số hàng (chiều cao)</label>
                    <input type="number" min="1" max="20" {...register("layoutHeight", { valueAsNumber: true })} style={input} onFocus={(e) => (e.target.style.borderColor = "#f97316")} onBlur={(e) => (e.target.style.borderColor = "#e2e8f0")} />
                  </div>
                </div>

                <div style={{ background: "#fff7ed", borderRadius: 12, padding: "12px 16px", border: "1px solid #fed7aa", fontSize: 13, color: "#92400e", display: "flex", alignItems: "center", gap: 8 }}>
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z"/></svg>
                  Mặt bằng: <strong>{layoutWidth} cột × {layoutHeight} hàng</strong> = {layoutWidth * layoutHeight} ô
                </div>

                <div>
                  <label style={label}>Ảnh trạm sạc</label>
                  <div style={{ border: "2px dashed #e2e8f0", borderRadius: 12, padding: 16, background: "#f8fafc", textAlign: "center" }}>
                    <input type="file" accept="image/*" multiple id="station-images-input"
                      onChange={(e) => { const files = Array.from(e.target.files || []); setStationImages((prev) => [...prev, ...files]); e.target.value = ""; }}
                      style={{ display: "none" }}
                    />
                    <label htmlFor="station-images-input" style={{ cursor: "pointer", display: "block" }}>
                      <div style={{ display: "flex", justifyContent: "center", marginBottom: 8 }}>
                        <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="#94a3b8" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"><rect x="3" y="3" width="18" height="18" rx="2" ry="2"/><circle cx="8.5" cy="8.5" r="1.5"/><polyline points="21 15 16 10 5 21"/></svg>
                      </div>
                      <div style={{ fontSize: 13, color: "#64748b", fontWeight: 600 }}>Nhấn để chọn ảnh</div>
                      <div style={{ fontSize: 11, color: "#94a3b8", marginTop: 2 }}>PNG, JPG, WEBP (tối đa 5 ảnh)</div>
                    </label>
                    {stationImages.length > 0 && (
                      <div style={{ display: "flex", flexWrap: "wrap", gap: 10, marginTop: 14, justifyContent: "center" }}>
                        {stationImages.map((file, i) => (
                          <div key={i} style={{ position: "relative" }}>
                            <img src={URL.createObjectURL(file)} alt={`Preview ${i + 1}`} style={{ width: 72, height: 72, borderRadius: 10, objectFit: "cover", border: "2px solid #e2e8f0" }} />
                            <button type="button" onClick={() => setStationImages((prev) => prev.filter((_, idx) => idx !== i))}
                              style={{ position: "absolute", top: -6, right: -6, width: 20, height: 20, background: "#ef4444", color: "#fff", border: "none", borderRadius: "50%", fontSize: 11, cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center" }}>×</button>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* ───────── STEP 2: Vị trí bản đồ ───────── */}
          {step === 2 && (
            <div style={{ ...card, animation: "fadeIn 0.3s ease" }}>
              <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 28 }}>
                <div style={{ width: 44, height: 44, borderRadius: 12, background: "linear-gradient(135deg, #3b82f6, #2563eb)", display: "flex", alignItems: "center", justifyContent: "center", color: "#fff" }}>
                  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>
                </div>
                <div>
                  <h2 style={{ margin: 0, fontSize: 20, fontWeight: 800, color: "#0f172a" }}>Vị trí trạm sạc</h2>
                  <p style={{ margin: 0, fontSize: 13, color: "#94a3b8" }}>Chọn vị trí trên bản đồ hoặc nhập địa chỉ</p>
                </div>
              </div>

              <MapPicker
                lat={watch("latitude") || 10.7295}
                lng={watch("longitude") || 106.7218}
                onSelect={(lat, lng, addr) => {
                  setValue("latitude", lat);
                  setValue("longitude", lng);
                  if (addr) setValue("address", addr);
                }}
              />

              <div style={{ marginTop: 20 }}>
                <label style={label}>Địa chỉ cụ thể <span style={{ color: "#ef4444" }}>*</span></label>
                <input {...register("address")} style={input} placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/thành phố..." onFocus={(e) => (e.target.style.borderColor = "#3b82f6")} onBlur={(e) => (e.target.style.borderColor = "#e2e8f0")} />
                <FieldError message={errors.address?.message} />
              </div>
              <input type="hidden" {...register("latitude")} />
              <input type="hidden" {...register("longitude")} />

              {watch("latitude") && watch("longitude") && (
                <div style={{ marginTop: 12, background: "#eff6ff", borderRadius: 10, padding: "10px 14px", fontSize: 12, color: "#1d4ed8", border: "1px solid #bfdbfe", display: "flex", alignItems: "center", gap: 6 }}>
                  <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><circle cx="12" cy="12" r="3"/><path d="M12 2v4M12 18v4M2 12h4M18 12h4"/></svg>
                  Đã chọn tọa độ: {Number(watch("latitude")).toFixed(5)}, {Number(watch("longitude")).toFixed(5)}
                </div>
              )}
            </div>
          )}

          {/* ───────── STEP 3: Giờ hoạt động + Mặt bằng ───────── */}
          {step === 3 && (
            <div style={{ display: "flex", flexDirection: "column", gap: 24, animation: "fadeIn 0.3s ease" }}>
              {/* Giờ hoạt động */}
              <div style={card}>
                <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 24 }}>
                  <div style={{ width: 44, height: 44, borderRadius: 12, background: "linear-gradient(135deg, #10b981, #059669)", display: "flex", alignItems: "center", justifyContent: "center", color: "#fff" }}>
                    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
                  </div>
                  <div>
                    <h2 style={{ margin: 0, fontSize: 20, fontWeight: 800, color: "#0f172a" }}>Giờ hoạt động</h2>
                    <p style={{ margin: 0, fontSize: 13, color: "#94a3b8" }}>Cấu hình cho từng ngày trong tuần</p>
                  </div>
                </div>
                <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                  {dayOptions.map((day, index) => {
                    const isClosed = operatingHours?.[index]?.isClosed;
                    return (
                      <div key={day.value} style={{ display: "grid", gridTemplateColumns: "100px 1fr 1fr auto", gap: 12, alignItems: "center", background: isClosed ? "#f8fafc" : "#fff", borderRadius: 12, padding: "12px 16px", border: "1.5px solid #e2e8f0" }}>
                        <div>
                          <div style={{ fontSize: 13, fontWeight: 700, color: isClosed ? "#94a3b8" : "#1e293b" }}>{day.label}</div>
                          <label style={{ display: "flex", alignItems: "center", gap: 5, fontSize: 11, color: "#94a3b8", marginTop: 4, cursor: "pointer" }}>
                            <input type="checkbox" {...register(`operatingHours.${index}.isClosed`)} />
                            Đóng cửa
                          </label>
                        </div>
                        <TimePicker24h value={(operatingHours?.[index]?.openTime || "06:00").substring(0, 5)} disabled={isClosed} onChange={(v) => setValue(`operatingHours.${index}.openTime`, v)} />
                        <TimePicker24h value={(operatingHours?.[index]?.closeTime || "23:00").substring(0, 5)} disabled={isClosed} minAfter={isClosed ? undefined : (operatingHours?.[index]?.openTime || "06:00").substring(0, 5)} onChange={(v) => setValue(`operatingHours.${index}.closeTime`, v)} />
                        <input type="hidden" {...register(`operatingHours.${index}.dayOfWeek`)} />
                      </div>
                    );
                  })}
                </div>
              </div>

              {/* Mặt bằng trụ sạc */}
              <div style={card}>
                <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 24 }}>
                  <div style={{ width: 44, height: 44, borderRadius: 12, background: "linear-gradient(135deg, #f97316, #ea580c)", display: "flex", alignItems: "center", justifyContent: "center", color: "#fff" }}>
                    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z"/></svg>
                  </div>
                  <div>
                    <h2 style={{ margin: 0, fontSize: 20, fontWeight: 800, color: "#0f172a" }}>Mặt bằng trụ sạc</h2>
                    <p style={{ margin: 0, fontSize: 13, color: "#94a3b8" }}>Nhấn vào ô trống để đặt trụ sạc • {slots.length} trụ đã đặt</p>
                  </div>
                </div>

                <div style={{ display: "flex", gap: 24, flexWrap: "wrap" }}>
                  {/* Grid */}
                  <div style={{ flex: 1, minWidth: 300 }}>
                    <div style={{ display: "grid", gridTemplateColumns: `36px repeat(${layoutWidth}, 1fr)`, gap: 4, background: "#f1f5f9", borderRadius: 16, padding: 12, border: "2px solid #e2e8f0", maxWidth: 640 }}>
                      <div />
                      {Array.from({ length: layoutWidth }).map((_, col) => (
                        <div key={`h-${col}`} style={{ textAlign: "center", fontSize: 11, fontWeight: 700, color: "#94a3b8", padding: "4px 0" }}>{col + 1}</div>
                      ))}
                      {Array.from({ length: layoutHeight }).map((_, row) => (
                        <>
                          <div key={`r-${row}`} style={{ display: "flex", alignItems: "center", justifyContent: "center", fontSize: 11, fontWeight: 700, color: "#94a3b8" }}>
                            {String.fromCharCode(65 + row)}
                          </div>
                          {Array.from({ length: layoutWidth }).map((_, col) => {
                            const x = col + 1; const y = row + 1;
                            const slotIdx = slots.findIndex((s) => Number(s.positionX) === x && Number(s.positionY) === y);
                            const slot = slotIdx >= 0 ? slots[slotIdx] : null;
                            const isSelected = selectedSlotIdx === slotIdx && slot;
                            if (slot) return (
                              <button type="button" key={`${x}-${y}`} onClick={() => setSelectedSlotIdx(slotIdx)}
                                style={{ background: isSelected ? SLOT_SELECTED : SLOT_COLOR, color: "#fff", borderRadius: 10, border: isSelected ? "3px solid #1e293b" : "2px solid #c2410c", display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", cursor: "pointer", minHeight: 52, padding: "4px 2px", transition: "all .15s", transform: isSelected ? "scale(1.08)" : "scale(1)", boxShadow: isSelected ? "0 4px 12px rgba(0,0,0,0.25)" : "0 1px 4px rgba(0,0,0,0.1)" }} title={slot.slotName}>
                                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" /></svg>
                                <span style={{ fontSize: 9, fontWeight: 700, marginTop: 2, lineHeight: 1 }}>{slot.slotName}</span>
                              </button>
                            );
                            return (
                              <button type="button" key={`${x}-${y}`} onClick={() => handleCellClick(x, y)}
                                style={{ background: "#e8ecf1", borderRadius: 10, border: "2px dashed #cbd5e1", display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer", minHeight: 52, transition: "all .15s" }}
                                onMouseEnter={(e) => { e.currentTarget.style.background = "#fed7aa"; e.currentTarget.style.borderColor = "#f97316"; }}
                                onMouseLeave={(e) => { e.currentTarget.style.background = "#e8ecf1"; e.currentTarget.style.borderColor = "#cbd5e1"; }}
                                title={`Thêm trụ tại ${String.fromCharCode(65 + row)}${col + 1}`}>
                                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#94a3b8" strokeWidth="2"><line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" /></svg>
                              </button>
                            );
                          })}
                        </>
                      ))}
                    </div>
                    <div style={{ display: "flex", gap: 14, marginTop: 10, fontSize: 11, color: "#94a3b8" }}>
                      <div style={{ display: "flex", alignItems: "center", gap: 5 }}><div style={{ width: 12, height: 12, borderRadius: 3, background: SLOT_COLOR }} />Trụ đã đặt</div>
                      <div style={{ display: "flex", alignItems: "center", gap: 5 }}><div style={{ width: 12, height: 12, borderRadius: 3, background: "#e8ecf1", border: "1.5px dashed #cbd5e1" }} />Ô trống</div>
                    </div>
                  </div>

                  {/* Slot detail */}
                  <div style={{ width: 220, flexShrink: 0 }}>
                    <h4 style={{ fontSize: 14, fontWeight: 700, color: "#1e293b", marginBottom: 12 }}>Ổ sạc đã chọn</h4>
                    {selectedSlotIdx !== null && slots[selectedSlotIdx] ? (
                      <div style={{ background: "#f8fafc", borderRadius: 14, padding: 16, border: "1.5px solid #e2e8f0" }}>
                        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 10 }}>
                          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                            <div style={{ width: 32, height: 32, borderRadius: 8, background: SLOT_COLOR, display: "flex", alignItems: "center", justifyContent: "center" }}>
                              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth="2.5"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" /></svg>
                            </div>
                            <span style={{ fontWeight: 700, fontSize: 16, color: "#0f172a" }}>
                              {String.fromCharCode(64 + Number(slots[selectedSlotIdx].positionY))}{slots[selectedSlotIdx].positionX}
                            </span>
                          </div>
                          <button type="button" onClick={() => handleRemoveSlot(selectedSlotIdx)} style={{ fontSize: 11, fontWeight: 600, color: "#ef4444", border: "none", background: "none", cursor: "pointer", display: "flex", alignItems: "center", gap: 4 }}>
                            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/></svg>
                            Xóa
                          </button>
                        </div>
                        <div style={{ fontSize: 12, color: "#94a3b8" }}>Hàng {String.fromCharCode(64 + Number(slots[selectedSlotIdx].positionY))}, Cột {slots[selectedSlotIdx].positionX}</div>
                        <input type="hidden" {...register(`slots.${selectedSlotIdx}.positionX`)} />
                        <input type="hidden" {...register(`slots.${selectedSlotIdx}.positionY`)} />
                      </div>
                    ) : (
                      <div style={{ background: "#f8fafc", borderRadius: 14, padding: 24, border: "2px dashed #e2e8f0", textAlign: "center" }}>
                        <div style={{ display: "flex", justifyContent: "center", marginBottom: 8, opacity: 0.3 }}>
                          <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="#94a3b8" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z"/></svg>
                        </div>
                        <p style={{ fontSize: 12, color: "#94a3b8", margin: 0 }}>{slots.length === 0 ? "Nhấn vào ô trống để đặt ổ sạc" : "Chọn ổ sạc để xem"}</p>
                      </div>
                    )}
                    {slots.length > 0 && (
                      <div style={{ marginTop: 12, background: "#f0fdf4", borderRadius: 10, padding: "10px 14px", fontSize: 12, color: "#15803d", border: "1px solid #bbf7d0", display: "flex", alignItems: "center", gap: 6 }}>
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round"><polyline points="20 6 9 17 4 12"/></svg>
                        Đã đặt <strong>{slots.length}</strong> trụ sạc
                      </div>
                    )}
                  </div>
                </div>
                <FieldError message={errors.slots?.message} />
              </div>
            </div>
          )}

          {/* ───────── STEP 4: Giá + Xác nhận ───────── */}
          {step === 4 && (
            <div style={{ display: "flex", flexDirection: "column", gap: 24, animation: "fadeIn 0.3s ease" }}>
              {/* Giá theo khung giờ */}
              <div style={card}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 24 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
                    <div style={{ width: 44, height: 44, borderRadius: 12, background: "linear-gradient(135deg, #8b5cf6, #7c3aed)", display: "flex", alignItems: "center", justifyContent: "center", color: "#fff" }}>
                      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="1" y="4" width="22" height="16" rx="2" ry="2"/><line x1="1" y1="10" x2="23" y2="10"/></svg>
                    </div>
                    <div>
                      <h2 style={{ margin: 0, fontSize: 20, fontWeight: 800, color: "#0f172a" }}>Giá theo khung giờ</h2>
                      <p style={{ margin: 0, fontSize: 13, color: "#94a3b8" }}>Áp dụng chung cho tất cả ổ sạc</p>
                    </div>
                  </div>
                  <button type="button" onClick={() => {
                    const current = watch("stationPricing") || [];
                    const lastEnd = current.length > 0 ? current[current.length - 1].endTime : "00:00";
                    setValue("stationPricing", [...current, { startTime: lastEnd, endTime: "", pricePerHour: "" }]);
                  }} style={{ padding: "8px 16px", borderRadius: 10, border: "1.5px solid #f97316", background: "#fff7ed", color: "#ea580c", fontWeight: 700, fontSize: 13, cursor: "pointer" }}>
                    + Thêm khung giờ
                  </button>
                </div>

                {(() => {
                  const pricing = watch("stationPricing") || [];
                  if (pricing.length === 0) return (
                    <div style={{ textAlign: "center", padding: "32px 0", color: "#94a3b8", fontSize: 14 }}>
                      <div style={{ display: "flex", justifyContent: "center", marginBottom: 10 }}>
                        <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="#cbd5e1" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
                      </div>
                      <p>Chưa có khung giờ. Nhấn "+ Thêm khung giờ" để bắt đầu.</p>
                    </div>
                  );
                  return (
                    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                      {pricing.map((rule, rIdx) => {
                        const autoStart = rIdx === 0 ? "00:00" : (pricing[rIdx - 1]?.endTime || "");
                        const timeRegex = /^([01]\d|2[0-3]):([0-5]\d)$/;
                        const isValidFormat = !rule.endTime || timeRegex.test(rule.endTime);
                        const isAfterStart = !rule.endTime || !autoStart || rule.endTime > autoStart;
                        const hasError = rule.endTime && (!isValidFormat || !isAfterStart);
                        return (
                          <div key={rIdx} style={{ background: "#f8fafc", borderRadius: 14, padding: "14px 16px", border: hasError ? "1.5px solid #fca5a5" : "1.5px solid #e2e8f0" }}>
                            <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
                              <div style={{ textAlign: "center" }}>
                                <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 4 }}>Từ</div>
                                <div style={{ height: 38, width: 68, background: "#e2e8f0", borderRadius: 10, display: "flex", alignItems: "center", justifyContent: "center", fontSize: 14, fontWeight: 700, color: "#475569" }}>{autoStart || "--:--"}</div>
                              </div>
                              <span style={{ color: "#cbd5e1", fontSize: 18, marginTop: 14 }}>→</span>
                              <div style={{ textAlign: "center" }}>
                                <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 4 }}>Đến</div>
                                <input type="text" value={rule.endTime || ""} placeholder="HH:mm" maxLength={5}
                                  onChange={(e) => {
                                    let val = e.target.value.replace(/[^0-9:]/g, "");
                                    if (val.length === 2 && !val.includes(":")) val += ":";
                                    if (val.length > 5) val = val.slice(0, 5);
                                    const updated = [...pricing];
                                    updated[rIdx] = { ...updated[rIdx], endTime: val, startTime: autoStart };
                                    if (val.length === 5 && rIdx + 1 < updated.length) updated[rIdx + 1] = { ...updated[rIdx + 1], startTime: val };
                                    setValue("stationPricing", updated);
                                  }}
                                  style={{ height: 38, width: 68, borderRadius: 10, border: hasError ? "1.5px solid #ef4444" : "1.5px solid #e2e8f0", textAlign: "center", fontWeight: 700, fontSize: 14, outline: "none", background: hasError ? "#fef2f2" : "#fff", color: hasError ? "#ef4444" : "#1e293b" }}
                                />
                              </div>
                              <div style={{ flex: 1 }}>
                                <div style={{ fontSize: 10, color: "#94a3b8", marginBottom: 4 }}>Giá / giờ (VND)</div>
                                <div style={{ position: "relative" }}>
                                  <input type="number" value={rule.pricePerHour} placeholder="10000"
                                    onChange={(e) => {
                                      const updated = [...pricing];
                                      updated[rIdx] = { ...updated[rIdx], pricePerHour: Number(e.target.value), startTime: autoStart };
                                      setValue("stationPricing", updated);
                                    }}
                                    style={{ height: 38, width: "100%", borderRadius: 10, border: "1.5px solid #e2e8f0", paddingLeft: 12, paddingRight: 28, fontSize: 14, outline: "none", boxSizing: "border-box" }}
                                  />
                                  <span style={{ position: "absolute", right: 10, top: "50%", transform: "translateY(-50%)", fontSize: 11, color: "#94a3b8" }}>đ</span>
                                </div>
                              </div>
                              <button type="button" onClick={() => { const updated = [...pricing]; updated.splice(rIdx, 1); setValue("stationPricing", updated); }}
                                style={{ marginTop: 16, color: "#ef4444", border: "none", background: "none", cursor: "pointer", fontSize: 18, lineHeight: 1 }} title="Xóa">×</button>
                            </div>
                            {hasError && (
                              <p style={{ margin: "6px 0 0", fontSize: 11, color: "#ef4444", fontWeight: 600, display: "flex", alignItems: "center", gap: 4 }}>
                                <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
                                {!isValidFormat ? "Thời gian không hợp lệ!" : `Phải sau ${autoStart}`}
                              </p>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  );
                })()}
              </div>

              {/* Summary */}
              <div style={{ ...card, background: "linear-gradient(135deg, #fff7ed, #fffbeb)", border: "1.5px solid #fed7aa" }}>
                <h3 style={{ margin: "0 0 16px", fontSize: 16, fontWeight: 800, color: "#92400e", display: "flex", alignItems: "center", gap: 8 }}>
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>
                  Tóm tắt trạm sạc
                </h3>
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, fontSize: 13 }}>
                  <div style={{ background: "rgba(255,255,255,0.6)", borderRadius: 10, padding: "10px 14px" }}>
                    <div style={{ color: "#92400e", fontWeight: 600, marginBottom: 2 }}>Tên trạm</div>
                    <div style={{ color: "#1e293b", fontWeight: 700 }}>{watch("name") || "—"}</div>
                  </div>
                  <div style={{ background: "rgba(255,255,255,0.6)", borderRadius: 10, padding: "10px 14px" }}>
                    <div style={{ color: "#92400e", fontWeight: 600, marginBottom: 2 }}>Mặt bằng</div>
                    <div style={{ color: "#1e293b", fontWeight: 700 }}>{layoutWidth} × {layoutHeight} ({slots.length} trụ)</div>
                  </div>
                  <div style={{ background: "rgba(255,255,255,0.6)", borderRadius: 10, padding: "10px 14px", gridColumn: "1 / -1" }}>
                    <div style={{ color: "#92400e", fontWeight: 600, marginBottom: 2 }}>Địa chỉ</div>
                    <div style={{ color: "#1e293b", fontWeight: 700, fontSize: 12 }}>{watch("address") || "Chưa nhập"}</div>
                  </div>
                  <div style={{ background: "rgba(255,255,255,0.6)", borderRadius: 10, padding: "10px 14px" }}>
                    <div style={{ color: "#92400e", fontWeight: 600, marginBottom: 2 }}>Ảnh</div>
                    <div style={{ color: "#1e293b", fontWeight: 700 }}>{stationImages.length} ảnh</div>
                  </div>
                  <div style={{ background: "rgba(255,255,255,0.6)", borderRadius: 10, padding: "10px 14px" }}>
                    <div style={{ color: "#92400e", fontWeight: 600, marginBottom: 2 }}>Khung giờ giá</div>
                    <div style={{ color: "#1e293b", fontWeight: 700 }}>{(watch("stationPricing") || []).length} khung</div>
                  </div>
                </div>

                {errors.root?.serverError?.message && (
                  <div style={{ marginTop: 16, background: "#fef2f2", border: "1px solid #fca5a5", borderRadius: 12, padding: "12px 16px", fontSize: 13, color: "#dc2626", fontWeight: 600, display: "flex", alignItems: "center", gap: 8 }}>
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
                    {errors.root.serverError.message}
                  </div>
                )}
              </div>
            </div>
          )}

          {/* ───────── Navigation Buttons ───────── */}
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: 28 }}>
            <button type="button" onClick={() => setStep((s) => Math.max(s - 1, 1))} disabled={step === 1}
              style={{ padding: "12px 24px", borderRadius: 12, border: "1.5px solid #e2e8f0", background: step === 1 ? "#f8fafc" : "#fff", color: step === 1 ? "#cbd5e1" : "#374151", fontWeight: 600, fontSize: 14, cursor: step === 1 ? "not-allowed" : "pointer", display: "flex", alignItems: "center", gap: 8, transition: "all 0.2s" }}>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><polyline points="15 18 9 12 15 6"/></svg>
              Bước trước
            </button>

            <div style={{ fontSize: 13, color: "#94a3b8" }}>Bước {step} / {STEPS.length}</div>

            {step < 4 ? (
              <button type="button" onClick={handleNext}
                style={{ padding: "12px 28px", borderRadius: 12, border: "none", background: "linear-gradient(135deg, #f97316, #ea580c)", color: "#fff", fontWeight: 700, fontSize: 14, cursor: "pointer", display: "flex", alignItems: "center", gap: 8, boxShadow: "0 4px 14px rgba(249,115,22,0.35)", transition: "all 0.2s" }}>
                Bước tiếp theo
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><polyline points="9 18 15 12 9 6"/></svg>
              </button>
            ) : (
              <Button type="submit" disabled={createStationMutation.isPending}
                style={{ padding: "12px 28px", borderRadius: 12, border: "none", background: createStationMutation.isPending ? "#d1d5db" : "linear-gradient(135deg, #22c55e, #16a34a)", color: "#fff", fontWeight: 700, fontSize: 14, cursor: createStationMutation.isPending ? "not-allowed" : "pointer", display: "flex", alignItems: "center", gap: 8, boxShadow: createStationMutation.isPending ? "none" : "0 4px 14px rgba(34,197,94,0.35)" }}>
                {createStationMutation.isPending ? (
                  <><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ animation: "spin 1s linear infinite" }}><path d="M21 12a9 9 0 1 1-6.219-8.56"/></svg> Đang tạo trạm...</>
                ) : (
                  <><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round"><polyline points="20 6 9 17 4 12"/></svg> Hoàn tất tạo trạm ({slots.length} trụ)</>
                )}
              </Button>
            )}
          </div>
        </form>
      </div>

      <style>{`
        @keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
        @keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
      `}</style>
    </div>
  );
}
