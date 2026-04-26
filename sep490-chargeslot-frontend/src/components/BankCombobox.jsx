import React, { useState, useEffect, useRef } from "react";

export default function BankCombobox({ value, onChange, placeholder = "Chọn hoặc nhập tên ngân hàng..." }) {
  const [banks, setBanks] = useState([]);
  const [isOpen, setIsOpen] = useState(false);
  const [search, setSearch] = useState(value || "");
  const dropdownRef = useRef(null);

  useEffect(() => {
    fetch("https://api.vietqr.io/v2/banks")
      .then(res => res.json())
      .then(data => {
        if (data.code === "00") setBanks(data.data);
      })
      .catch(() => {});
  }, []);

  // Sync internal search state if external value changes
  useEffect(() => {
    setSearch(value || "");
  }, [value]);

  // Click outside to close dropdown
  useEffect(() => {
    function handleClickOutside(event) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target)) {
        setIsOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  // Filter logic
  const filteredBanks = banks.filter(b => {
    const term = search.toLowerCase();
    return b.shortName?.toLowerCase().includes(term) || b.name?.toLowerCase().includes(term);
  });

  return (
    <div ref={dropdownRef} style={{ position: "relative" }}>
      <label style={{ fontSize: 13, fontWeight: 600, color: "#374151", display: "block", marginBottom: 4 }}>
        Tên ngân hàng
      </label>
      <input
        type="text"
        value={search}
        onChange={e => {
          setSearch(e.target.value);
          onChange(e.target.value);
          setIsOpen(true);
        }}
        onFocus={() => setIsOpen(true)}
        placeholder={placeholder}
        style={{
          width: "100%", padding: "10px 14px", borderRadius: 10,
          border: "1.5px solid #e5e7eb", fontSize: 14, outline: "none", boxSizing: "border-box",
          transition: "border-color 0.2s",
        }}
        onFocusCapture={e => { e.target.style.borderColor = "#3b82f6"; }}
        onBlurCapture={e => { e.target.style.borderColor = "#e5e7eb"; }}
      />
      
      {isOpen && (
        <div style={{
          position: "absolute", top: "100%", left: 0, right: 0, marginTop: 6,
          background: "#fff", border: "1px solid #e5e7eb", borderRadius: 12,
          boxShadow: "0 10px 25px rgba(0,0,0,0.1)", zIndex: 50,
          maxHeight: 280, overflowY: "auto"
        }}>
          {filteredBanks.length === 0 ? (
            <div style={{ padding: "16px", color: "#64748b", fontSize: 13, textAlign: "center" }}>
              Không tìm thấy ngân hàng phù hợp
            </div>
          ) : (
            filteredBanks.map(b => (
              <div 
                key={b.bin}
                onClick={() => {
                  const val = b.shortName; // Sử dụng tên ngắn gọn khi chọn chuẩn
                  setSearch(val);
                  onChange(val);
                  setIsOpen(false);
                }}
                style={{
                  display: "flex", alignItems: "center", gap: 12, padding: "10px 16px",
                  cursor: "pointer", borderBottom: "1px solid #f8fafc", transition: "background 0.2s"
                }}
                onMouseEnter={e => e.currentTarget.style.background = "#f1f5f9"}
                onMouseLeave={e => e.currentTarget.style.background = "transparent"}
              >
                <div style={{
                  width: 50, height: 32, flexShrink: 0, background: "#fff", 
                  borderRadius: 6, border: "1px solid #e2e8f0", padding: 2,
                  display: "flex", alignItems: "center", justifyContent: "center"
                }}>
                  <img src={b.logo} alt={b.shortName} style={{ maxWidth: "100%", maxHeight: "100%", objectFit: "contain" }} />
                </div>
                <div style={{ minWidth: 0 }}>
                  <div style={{ fontWeight: 700, fontSize: 14, color: "#1e293b", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                    {b.shortName}
                  </div>
                  <div style={{ fontSize: 11, color: "#64748b", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis", marginTop: 2 }}>
                    {b.name}
                  </div>
                </div>
              </div>
            ))
          )}
        </div>
      )}
    </div>
  );
}
