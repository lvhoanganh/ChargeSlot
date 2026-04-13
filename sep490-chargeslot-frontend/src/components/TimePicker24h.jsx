import { useEffect, useState, useRef, useCallback } from "react";
import { createPortal } from "react-dom";

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
  const [popupStyle, setPopupStyle] = useState({});
  const btnRef = useRef(null);
  const popupRef = useRef(null);

  const HOURS = Array.from({ length: 24 }, (_, i) => String(i).padStart(2, "0"));
  const MINS = blockOnly 
    ? ["00", "30"] 
    : Array.from({ length: 12 }, (_, i) => String(i * 5).padStart(2, "0"));

  const [hh, mm] = (value || "00:00").split(":").map(s => (s || "00").padStart(2, "0"));

  // Snap mm về 00 hoặc 30 nếu blockOnly và có giá trị lẻ
  // Nếu !blockOnly, snap về bội của 5
  useEffect(() => {
    if (!onChange || !value) return;
    const [, m] = (value || "00:00").split(":");
    if (blockOnly) {
      if (m !== "00" && m !== "30") {
        const snapped = Number(m) < 30 ? "00" : "30";
        onChange(`${hh}:${snapped}`);
      }
    } else {
      const numM = Number(m);
      if (numM % 5 !== 0) {
        const snapped = Math.round(numM / 5) * 5;
        const validM = snapped === 60 ? "55" : String(snapped).padStart(2, "0");
        onChange(`${hh}:${validM}`);
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [blockOnly]);

  let minH = 0, minM = 0;
  if (minAfter) {
    const [mah, mam] = minAfter.split(":").map(Number);
    let nextBlock, nextHourAdd;
    if (blockOnly) {
      if (mam === 0) { nextBlock = 0; nextHourAdd = 0; }
      else if (mam <= 30) { nextBlock = 30; nextHourAdd = 0; }
      else { nextBlock = 0; nextHourAdd = 1; }
    } else {
      nextBlock = mam % 5 === 0 ? mam : mam + (5 - (mam % 5));
      nextHourAdd = nextBlock >= 60 ? 1 : 0;
    }
    minH = mah + nextHourAdd;
    minM = nextBlock >= 60 ? 0 : nextBlock;
    if (minH >= 24) { minH = 23; minM = blockOnly ? 30 : 55; }
  }

  // Tự động đẩy giá trị lên block hợp lệ khi minAfter thay đổi
  useEffect(() => {
    if (!minAfter || !onChange) return;
    const [vh, vm] = (value || "00:00").split(":").map(Number);
    const minLimitTotal = minH * 60 + minM;
    const valTotal = vh * 60 + vm;
    
    // Chỉ push nếu giờ hiện tại đang chọn NHỎ HƠN mức giới hạn đã được làm tròn block
    if (valTotal < minLimitTotal) {
      onChange(`${String(minH).padStart(2, "0")}:${String(minM).padStart(2, "0")}`);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [minAfter, minH, minM]);

  // Tính vị trí popup theo fixed coordinates
  const calcPopupStyle = useCallback(() => {
    if (!btnRef.current) return;
    const rect = btnRef.current.getBoundingClientRect();
    const popupWidth = blockOnly ? 230 : 280;
    const popupEstHeight = 320;

    // Hiện bên dưới hay trên button tùy không gian còn lại
    const spaceBelow = window.innerHeight - rect.bottom;
    const showAbove = spaceBelow < popupEstHeight && rect.top > popupEstHeight;

    let left = rect.left + rect.width / 2 - popupWidth / 2;
    // Clamp vào viewport
    if (left < 8) left = 8;
    if (left + popupWidth > window.innerWidth - 8) left = window.innerWidth - popupWidth - 8;

    setPopupStyle({
      position: "fixed",
      top: showAbove ? rect.top - popupEstHeight - 6 : rect.bottom + 6,
      left,
      width: popupWidth,
      zIndex: 99999,
    });
  }, [blockOnly]);

  function toggleOpen() {
    if (disabled) return;
    if (!open) {
      calcPopupStyle();
      setOpen(true);
    } else {
      setOpen(false);
    }
  }

  // Đóng khi click ngoài
  useEffect(() => {
    if (!open) return;
    function handle(e) {
      if (
        btnRef.current && !btnRef.current.contains(e.target) &&
        popupRef.current && !popupRef.current.contains(e.target)
      ) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", handle);
    return () => document.removeEventListener("mousedown", handle);
  }, [open]);

  // Cập nhật vị trí khi scroll/resize
  useEffect(() => {
    if (!open) return;
    const update = () => calcPopupStyle();
    window.addEventListener("scroll", update, true);
    window.addEventListener("resize", update);
    return () => {
      window.removeEventListener("scroll", update, true);
      window.removeEventListener("resize", update);
    };
  }, [open, calcPopupStyle]);

  function pickHour(h) {
    let newMM = mm;
    if (blockOnly && (newMM !== "00" && newMM !== "30")) newMM = "00";
    if (!blockOnly && Number(newMM) % 5 !== 0) newMM = "00";
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

  const popup = open && createPortal(
    <div
      ref={popupRef}
      style={{
        ...popupStyle,
        background: "#fff",
        borderRadius: 16,
        border: "1px solid #e2e8f0",
        boxShadow: "0 20px 60px rgba(0,0,0,0.18)",
        padding: "14px 12px 10px",
        animation: "tpFadeSlideDown .15s ease",
      }}
    >
      {/* Current time preview */}
      <div style={{ textAlign: "center", marginBottom: 10 }}>
        <span style={{
          fontSize: 32, fontWeight: 800,
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
          <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 3, maxHeight: 190, overflowY: "auto" }}>
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
          <div style={{ display: blockOnly ? "flex" : "grid", gridTemplateColumns: blockOnly ? "none" : "repeat(2, 1fr)", flexDirection: blockOnly ? "column" : "none", gap: blockOnly ? 6 : 3, maxHeight: 190, overflowY: "auto" }}>
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
          width: "100%", marginTop: 10, padding: "9px 0",
          background: "linear-gradient(135deg,#f97316,#ea580c)",
          color: "#fff", fontWeight: 700, fontSize: 13,
          border: "none", borderRadius: 10, cursor: "pointer",
          boxShadow: "0 4px 12px rgba(249,115,22,0.3)",
        }}
      >
        ✓ Xác nhận
      </button>

      <style>{`
        @keyframes tpFadeSlideDown {
          from { opacity: 0; transform: translateY(-6px); }
          to   { opacity: 1; transform: translateY(0); }
        }
      `}</style>
    </div>,
    document.body
  );

  return (
    <div style={{ position: "relative", display: "inline-block" }} className={className}>
      {/* Display chip */}
      <button
        ref={btnRef}
        type="button"
        disabled={disabled}
        onClick={toggleOpen}
        style={{
          display: "inline-flex", alignItems: "center", gap: 2,
          padding: "7px 16px", borderRadius: 10,
          border: `2px solid ${open ? "#f97316" : "#e2e8f0"}`,
          background: disabled ? "#f1f5f9" : open ? "#fff7ed" : "#fff",
          cursor: disabled ? "not-allowed" : "pointer",
          fontSize: 18, fontWeight: 700,
          color: disabled ? "#94a3b8" : "#1e293b",
          transition: "all 0.18s",
          boxShadow: open ? "0 0 0 3px rgba(249,115,22,0.15)" : "0 1px 3px rgba(0,0,0,0.06)",
          userSelect: "none",
          minWidth: 90,
          justifyContent: "center",
          whiteSpace: "nowrap",
        }}
      >
        <span style={{ color: "#f97316" }}>{hh}</span>
        <span style={{ color: "#cbd5e1", margin: "0 2px", fontSize: 16 }}>:</span>
        <span>{mm}</span>
        <span style={{ fontSize: 11, color: "#94a3b8", marginLeft: 6 }}>▾</span>
      </button>

      {popup}
    </div>
  );
}
