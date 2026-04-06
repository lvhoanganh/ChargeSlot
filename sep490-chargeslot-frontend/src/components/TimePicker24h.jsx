import { useEffect, useState, useRef } from "react";

/**
 * TimePicker24h — Popup grid picker định dạng 24h (00:00 – 23:59)
 * Props:
 *   value      string "HH:MM"
 *   onChange   (newValue: string) => void
 *   disabled   boolean
 *   className  string
 *   minAfter   string "HH:MM" — giá trị kết thúc phải > minAfter ít nhất 1 phút
 */
export default function TimePicker24h({ value = "00:00", onChange, disabled = false, className = "", minAfter }) {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);

  const HOURS = Array.from({ length: 24 }, (_, i) => String(i).padStart(2, "0"));
  const MINS  = Array.from({ length: 60 }, (_, i) => String(i).padStart(2, "0"));

  const [hh, mm] = (value || "00:00").split(":").map(s => (s || "00").padStart(2, "0"));

  // Tính giới hạn tối thiểu
  let minH = 0, minM = 0;
  if (minAfter) {
    const [mah, mam] = minAfter.split(":").map(Number);
    const total = mah * 60 + mam + 1;
    minH = Math.floor(total / 60);
    minM = total % 60;
    if (minH >= 24) { minH = 23; minM = 59; }
  }

  // Tự động đẩy giờ kết thúc lên minAfter+1 phút khi minAfter thay đổi
  useEffect(() => {
    if (!minAfter || !onChange) return;
    const [mah, mam] = minAfter.split(":").map(Number);
    const [vh, vm] = (value || "00:00").split(":").map(Number);
    const minTotal = mah * 60 + mam;
    const valTotal = vh * 60 + vm;
    if (valTotal <= minTotal) {
      let next = minTotal + 1;
      if (next >= 24 * 60) next = 23 * 60 + 59;
      onChange(`${String(Math.floor(next / 60)).padStart(2, "0")}:${String(next % 60).padStart(2, "0")}`);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [minAfter]);

  // Đóng khi click ngoài
  useEffect(() => {
    function handle(e) {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false);
    }
    document.addEventListener("mousedown", handle);
    return () => document.removeEventListener("mousedown", handle);
  }, []);

  function pickHour(h) {
    let newMM = mm;
    if (minAfter && Number(h) < minH) { h = String(minH).padStart(2, "0"); newMM = String(minM).padStart(2, "0"); }
    else if (minAfter && Number(h) === minH && Number(newMM) < minM) newMM = String(minM).padStart(2, "0");
    onChange?.(`${h}:${newMM}`);
  }

  function pickMin(m) {
    let newHH = hh;
    if (minAfter && Number(newHH) === minH && Number(m) < minM) m = String(minM).padStart(2, "0");
    onChange?.(`${newHH}:${m}`);
  }

  const btnStyle = (selected, isDisabled) => ({
    padding: "5px 2px",
    borderRadius: 7,
    border: "none",
    background: selected ? "linear-gradient(135deg,#f97316,#ea580c)" : "transparent",
    color: selected ? "#fff" : isDisabled ? "#d1d5db" : "#374151",
    fontWeight: selected ? 700 : 400,
    fontSize: 12,
    cursor: isDisabled ? "not-allowed" : "pointer",
    transition: "all 0.12s",
    boxShadow: selected ? "0 2px 8px rgba(249,115,22,0.35)" : "none",
  });

  return (
    <div ref={ref} style={{ position: "relative", display: "inline-block" }} className={className}>
      {/* Display chip */}
      <button
        type="button"
        disabled={disabled}
        onClick={() => !disabled && setOpen(o => !o)}
        style={{
          display: "inline-flex", alignItems: "center", gap: 2,
          padding: "7px 14px", borderRadius: 10,
          border: `2px solid ${open ? "#f97316" : "#e2e8f0"}`,
          background: disabled ? "#f1f5f9" : open ? "#fff7ed" : "#fff",
          cursor: disabled ? "not-allowed" : "pointer",
          fontSize: 17, fontWeight: 700,
          color: disabled ? "#94a3b8" : "#1e293b",
          transition: "all 0.18s",
          boxShadow: open ? "0 0 0 3px rgba(249,115,22,0.15)" : "0 1px 3px rgba(0,0,0,0.06)",
          userSelect: "none",
          minWidth: 80,
          justifyContent: "center",
        }}
      >
        <span style={{ color: "#f97316" }}>{hh}</span>
        <span style={{ color: "#cbd5e1", margin: "0 1px", fontSize: 15 }}>:</span>
        <span>{mm}</span>
        <span style={{ fontSize: 11, color: "#94a3b8", marginLeft: 5 }}>▾</span>
      </button>

      {/* Popup */}
      {open && (
        <div style={{
          position: "absolute", top: "calc(100% + 8px)", left: "50%",
          transform: "translateX(-50%)",
          background: "#fff", borderRadius: 16,
          border: "1px solid #e2e8f0",
          boxShadow: "0 20px 60px rgba(0,0,0,0.14)",
          zIndex: 9999, padding: "14px 12px 10px",
          width: 270, animation: "fadeSlideDown .15s ease",
        }}>
          {/* Current time preview */}
          <div style={{ textAlign: "center", marginBottom: 10 }}>
            <span style={{
              fontSize: 30, fontWeight: 800,
              background: "linear-gradient(135deg,#f97316,#ea580c)",
              WebkitBackgroundClip: "text", WebkitTextFillColor: "transparent",
              letterSpacing: 2,
            }}>
              {hh}:{mm}
            </span>
          </div>

          <div style={{ display: "flex", gap: 10 }}>
            {/* Hours grid */}
            <div style={{ flex: 1 }}>
              <div style={{ fontSize: 10, fontWeight: 700, color: "#94a3b8", textAlign: "center", marginBottom: 5, letterSpacing: 1 }}>GIỜ</div>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 3, maxHeight: 185, overflowY: "auto" }}>
                {HOURS.map(h => {
                  const isDis = minAfter && Number(h) < minH;
                  return (
                    <button key={h} type="button" disabled={isDis} onClick={() => pickHour(h)}
                      style={btnStyle(h === hh, isDis)}
                      onMouseEnter={e => { if (!isDis && h !== hh) e.currentTarget.style.background = "#fff7ed"; }}
                      onMouseLeave={e => { if (h !== hh) e.currentTarget.style.background = "transparent"; }}
                    >{h}</button>
                  );
                })}
              </div>
            </div>

            <div style={{ width: 1, background: "#f1f5f9", margin: "0 2px" }} />

            {/* Minutes grid */}
            <div style={{ flex: 1 }}>
              <div style={{ fontSize: 10, fontWeight: 700, color: "#94a3b8", textAlign: "center", marginBottom: 5, letterSpacing: 1 }}>PHÚT</div>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 3, maxHeight: 185, overflowY: "auto" }}>
                {MINS.map(m => {
                  const isDis = minAfter && Number(hh) === minH && Number(m) < minM;
                  return (
                    <button key={m} type="button" disabled={isDis} onClick={() => pickMin(m)}
                      style={btnStyle(m === mm, isDis)}
                      onMouseEnter={e => { if (!isDis && m !== mm) e.currentTarget.style.background = "#fff7ed"; }}
                      onMouseLeave={e => { if (m !== mm) e.currentTarget.style.background = "transparent"; }}
                    >{m}</button>
                  );
                })}
              </div>
            </div>
          </div>

          {/* Confirm */}
          <button
            type="button"
            onClick={() => setOpen(false)}
            style={{
              width: "100%", marginTop: 10, padding: "8px 0",
              background: "linear-gradient(135deg,#f97316,#ea580c)",
              color: "#fff", fontWeight: 700, fontSize: 13,
              border: "none", borderRadius: 10, cursor: "pointer",
              boxShadow: "0 4px 12px rgba(249,115,22,0.3)",
            }}
          >
            ✓ Xác nhận
          </button>
        </div>
      )}

      <style>{`
        @keyframes fadeSlideDown {
          from { opacity: 0; transform: translateX(-50%) translateY(-6px); }
          to   { opacity: 1; transform: translateX(-50%) translateY(0); }
        }
      `}</style>
    </div>
  );
}
