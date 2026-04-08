import { useEffect, useState, useRef } from "react";

/**
 * TimePicker24h — Popup grid picker định dạng 24h (00:00 – 23:30)
 * ⚠️ BE RULE: startTime CHỈ ĐƯỢC là phút 00 hoặc 30 (30-Minute Block)
 * Props:
 *   value      string "HH:MM"
 *   onChange   (newValue: string) => void
 *   disabled   boolean
 *   className  string
 *   minAfter   string "HH:MM" — giá trị kết thúc phải > minAfter
 *   blockOnly  boolean (default: true) — chỉ cho phép phút 00/30
 */
export default function TimePicker24h({ value = "00:00", onChange, disabled = false, className = "", minAfter, blockOnly = true }) {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);

  const HOURS = Array.from({ length: 24 }, (_, i) => String(i).padStart(2, "0"));
  // Chỉ cho phép phút 00 hoặc 30 khi blockOnly=true (BE yêu cầu 30-minute block)
  const MINS = blockOnly ? ["00", "30"] : Array.from({ length: 60 }, (_, i) => String(i).padStart(2, "0"));

  const [hh, mm] = (value || "00:00").split(":").map(s => (s || "00").padStart(2, "0"));

  // Snap mm về 00 hoặc 30 nếu blockOnly và có giá trị lẻ
  useEffect(() => {
    if (!blockOnly || !onChange || !value) return;
    const [, m] = (value || "00:00").split(":");
    if (m !== "00" && m !== "30") {
      const snapped = Number(m) < 30 ? "00" : "30";
      onChange(`${hh}:${snapped}`);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [blockOnly]);

  // Tính giới hạn tối thiểu (theo block 30 phút)
  let minH = 0, minM = 0;
  if (minAfter) {
    const [mah, mam] = minAfter.split(":").map(Number);
    // Tăng lên block 30 phút tiếp theo
    const nextBlock = mam < 30 ? 30 : 0;
    const nextHourAdd = mam < 30 ? 0 : 1;
    minH = mah + nextHourAdd;
    minM = nextBlock;
    if (minH >= 24) { minH = 23; minM = 30; }
  }

  // Tự động đẩy giá trị lên block hợp lệ khi minAfter thay đổi
  useEffect(() => {
    if (!minAfter || !onChange) return;
    const [mah, mam] = minAfter.split(":").map(Number);
    const [vh, vm] = (value || "00:00").split(":").map(Number);
    const minTotal = mah * 60 + mam;
    const valTotal = vh * 60 + vm;
    if (valTotal <= minTotal) {
      // Tăng lên block 30 phút tiếp theo
      let next = minTotal + 30;
      if (next >= 24 * 60) next = 23 * 60 + 30;
      const nh = Math.floor(next / 60);
      const nm = next % 60 < 30 ? 0 : 30;
      onChange(`${String(nh).padStart(2, "0")}:${String(nm).padStart(2, "0")}`);
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
    if (blockOnly && (newMM !== "00" && newMM !== "30")) newMM = "00";
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
    padding: "6px 4px",
    borderRadius: 7,
    border: "none",
    background: selected ? "linear-gradient(135deg,#f97316,#ea580c)" : "transparent",
    color: selected ? "#fff" : isDisabled ? "#d1d5db" : "#374151",
    fontWeight: selected ? 700 : 400,
    fontSize: 13,
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
          width: blockOnly ? 220 : 270, animation: "fadeSlideDown .15s ease",
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

            {/* Minutes grid — chỉ 00 và 30 */}
            <div style={{ flex: 1 }}>
              <div style={{ fontSize: 10, fontWeight: 700, color: "#94a3b8", textAlign: "center", marginBottom: 5, letterSpacing: 1 }}>PHÚT</div>
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                {MINS.map(m => {
                  const isDis = minAfter && Number(hh) === minH && Number(m) < minM;
                  return (
                    <button key={m} type="button" disabled={isDis} onClick={() => pickMin(m)}
                      style={{ ...btnStyle(m === mm, isDis), fontSize: 15, padding: "10px 4px", fontWeight: m === mm ? 800 : 500 }}
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
