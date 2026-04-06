import { useState, useRef, useEffect, useCallback } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { useAuthStore } from "@/stores/authStore";
import { aiCopilotApi, publicStationApi } from "@/services/api";
import { showToast } from "@/components/Toast";

// Nhận diện ý định hỏi trạm gần nhất — KHÔNG cần gọi AI
const NEARBY_KEYWORDS = [
  "gần tôi", "gần nhất", "gần đây", "xung quanh", "quanh đây",
  "trạm gần", "trạm nào gần", "tìm trạm", "nearby", "closest",
  "cách tôi", "khu vực tôi",
];
const isNearbyQuery = (msg) =>
  NEARBY_KEYWORDS.some((kw) => msg.toLowerCase().includes(kw));

// Format kết quả nearby thành Markdown đẹp
const formatNearbyResult = (stations) => {
  if (!stations || stations.length === 0)
    return "😔 Không tìm thấy trạm sạc nào trong bán kính 10km quanh vị trí của Anh/Chị.";

  const lines = stations.map((s, i) => {
    const dist = s.distanceKm != null ? ` · 📍 **${s.distanceKm.toFixed(1)} km**` : "";
    const rating = s.averageRating ? ` · ⭐ ${s.averageRating.toFixed(1)}/5` : "";
    const reviews = s.totalReviews ? ` (${s.totalReviews} đánh giá)` : "";
    return `**${i + 1}. ${s.name}**${dist}${rating}${reviews}\n   📌 ${s.address}`;
  });

  return `Tôi tìm thấy **${stations.length} trạm sạc** đang hoạt động gần Anh/Chị nhất:\n\n${lines.join("\n\n")}`;
};

const MAX_HISTORY = 6;       // BE giới hạn tối đa 6 tin nhắn history
const MAX_MSG_LENGTH = 500;  // BE giới hạn currentMessage tối đa 500 ký tự
const WARN_TIMEOUT_MS = 8000; // Sau 8s → hiện cảnh báo AI đang suy nghĩ
const HARD_TIMEOUT_MS = 15000; // Sau 15s → báo lỗi, hủy request

const ROLE_CONFIG = {
  Admin: { apiRole: "admin", label: "AI Admin", accent: "#7c3aed", emoji: "🛡️" },
  Owner: { apiRole: "owner", label: "AI Cố Vấn", accent: "#f97316", emoji: "📊" },
  Driver: { apiRole: "driver", label: "AI Trợ Lý", accent: "#0ea5e9", emoji: "⚡" },
};

const SUGGESTIONS = {
  // Admin tools: get_admin_dashboard ✔️, get_pending_stations ✔️, get_pending_disputes ✔️
  Admin: ["Báo cáo doanh thu 30 ngày gần đây?", "Trạm nào đang chờ duyệt?", "Tình trạng khiếu nại?"],
  // Owner tools: check_owner_wallet ✔️, get_owner_dashboard ✔️, get_owner_bookings ✔️
  Owner: ["Doanh thu 30 ngày qua thế nào?", "Trạm nào đang tốt nhất?", "Xem đơn đặt gần nhất"],
  // Driver tools: check_wallet_balance ✔️, find_nearby_stations ✔️
  Driver: ["Ví tôi còn bao nhiêu?", "Trạm sạc nào gần tôi nhất?"],
};

// Lời chào mặc định cho từng role — HARDCODE, KHÔNG gọi API
const WELCOME_GREETINGS = {
  Driver: "Chào Anh/Chị! 👋 Tôi là trợ lý AI ChargeSlot.\n\nTôi có thể giúp Anh/Chị:\n- 💰 Kiểm tra số dư ví\n- 🔌 Tìm trạm sạc gần tôi nhất\n\nHãy hỏi tôi bất cứ điều gì!",
  Owner: "Xin chào! 👋 Tôi là Cố vấn AI ChargeSlot.\n\nTôi có thể tư vấn:\n- 📊 Báo cáo doanh thu 30 ngày\n- 📋 Danh sách đơn đặt gần nhất\n- 💰 Số dư ví\n\nHãy hỏi tôi bất cứ điều gì!",
  Admin: "Xin chào Admin! 👋 Tôi là AI Giám sát hệ thống.\n\nTôi có thể phân tích:\n- 📈 Doanh thu nền tảng 30 ngày\n- 🏢 Trạm đang chờ duyệt\n- ⚠️ Khiếu nại chưa xử lý\n\nHãy hỏi tôi bất cứ điều gì!",
};

export default function AiCopilot() {
  const { role } = useAuthStore();
  const [open, setOpen] = useState(false);
  const [chatHistory, setChatHistory] = useState([]);
  const [input, setInput] = useState("");
  const [loading, setLoading] = useState(false);
  const [slowWarning, setSlowWarning] = useState(false); // AI đang suy nghĩ lâu (>15s)
  const bottomRef = useRef(null);
  const inputRef = useRef(null);
  const abortRef = useRef(null);
  const warnTimerRef = useRef(null);
  const hardTimerRef = useRef(null);

  const cfg = ROLE_CONFIG[role] || ROLE_CONFIG.Driver;
  const [userLocation, setUserLocation] = useState(null); // [lat, lng] | null
  const [locationStatus, setLocationStatus] = useState("idle"); // "idle"|"fetching"|"ok"|"denied"

  // Lấy GPS 1 lần khi mở chatbot — cache lại, không fetch lại mỗi tin nhắn
  useEffect(() => {
    if (!open) return;
    setTimeout(() => inputRef.current?.focus(), 100);
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });

    if (!navigator.geolocation || locationStatus !== "idle") return;
    setLocationStatus("fetching");
    navigator.geolocation.getCurrentPosition(
      (p) => {
        setUserLocation([p.coords.latitude, p.coords.longitude]);
        setLocationStatus("ok");
      },
      () => setLocationStatus("denied"),
      { enableHighAccuracy: true, timeout: 8000, maximumAge: 60000 }
    );
  }, [open]);

  const send = useCallback(async (text) => {
    const rawMsg = (text || input).trim();
    if (!rawMsg || loading) return;
    // Giới hạn 500 ký tự theo spec BE
    const msg = rawMsg.slice(0, MAX_MSG_LENGTH);
    setInput("");
    setLoading(true);
    setSlowWarning(false);

    // Optimistic: thêm tin nhắn user ngay lập tức
    const userEntry = { role: "user", content: msg };
    setChatHistory(prev => {
      const next = [...prev, userEntry];
      return next.length > MAX_HISTORY ? next.slice(-MAX_HISTORY) : next;
    });

    // ─── INTERCEPT: Hỏi trạm gần → gọi thẳng API nearby, KHÔNG dùng AI ───
    if (role === "Driver" && isNearbyQuery(msg)) {
      try {
        if (!userLocation) {
          // GPS chưa sẵn sàng
          const noGpsMsg = locationStatus === "denied"
            ? "📍 Anh/Chị chưa cấp quyền vị trí cho trình duyệt. Vui lòng bật GPS và thử lại."
            : "⏳ Đang lấy vị trí GPS... Anh/Chị vui lòng thử lại sau giây lát.";
          setChatHistory(prev => [...prev, { role: "model", content: noGpsMsg }]);
        } else {
          const [lat, lng] = userLocation;
          const stations = await publicStationApi.getNearby(lat, lng, 10, 5);
          const reply = formatNearbyResult(stations);
          setChatHistory(prev => {
            const next = [...prev, { role: "model", content: reply }];
            return next.length > MAX_HISTORY ? next.slice(-MAX_HISTORY) : next;
          });
        }
      } catch (err) {
        setChatHistory(prev => [...prev, {
          role: "model",
          content: `⚠️ Không thể tải danh sách trạm gần đây: ${err?.message || "Lỗi kết nối"}`,
          isError: true,
        }]);
      } finally {
        setLoading(false);
      }
      return; // Không gọi AI
    }
    // ─────────────────────────────────────────────────────────────────────────

    // Gửi tối đa 6 tin nhắn history gần nhất (không bao gồm tin vừa thêm)
    const historyToSend = chatHistory.slice(-MAX_HISTORY);

    // Khởi tạo AbortController cho request mới
    abortRef.current = new AbortController();

    // Timer cảnh báo AI đang suy nghĩ
    warnTimerRef.current = setTimeout(() => setSlowWarning(true), WARN_TIMEOUT_MS);
    // Timer hard timeout
    hardTimerRef.current = setTimeout(() => {
      abortRef.current?.abort();
    }, HARD_TIMEOUT_MS);

    try {
      let apiMessage = msg;
      if (userLocation) {
        apiMessage = `${msg}\n[System Info: Vị trí GPS hiện tại của người dùng là Latitude: ${userLocation[0]}, Longitude: ${userLocation[1]}]`;
      }

      const data = await aiCopilotApi.chat(cfg.apiRole, historyToSend, apiMessage, {
        signal: abortRef.current.signal
      });

      clearTimeout(warnTimerRef.current);
      clearTimeout(hardTimerRef.current);
      setSlowWarning(false);

      const reply = data?.replyMarkdown || "Xin lỗi, tôi không thể trả lời lúc này.";
      const modelEntry = { role: "model", content: reply };

      setChatHistory(prev => {
        const next = [...prev, modelEntry];
        return next.length > MAX_HISTORY ? next.slice(-MAX_HISTORY) : next;
      });
    } catch (err) {
      clearTimeout(warnTimerRef.current);
      clearTimeout(hardTimerRef.current);
      setSlowWarning(false);

      const isTimeout = err?.name === "AbortError" || err?.message?.includes("abort");
      const isBanned = err?.response?.data?.isBanned || err?.message?.includes("banned");
      let errMsg;
      if (isTimeout) {
        errMsg = "⏳ Xin lỗi, AI không phản hồi (kết nối gián đoạn). Vui lòng thử lại.";
        showToast.error("AI không phản hồi sau 15s, kết nối đã bị hủy!");
      } else if (err?.status === 401 || err?.message?.includes("401")) {
        errMsg = "🔒 Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.";
      } else if (isBanned) {
        errMsg = "🚫 Tài khoản của bạn đã bị khóa.";
        showToast.error("Tài khoản bị khóa!");
      } else {
        errMsg = `⚠️ **Lỗi:** ${err?.message || "Không thể kết nối tới AI"}`;
        showToast.error("Lỗi kết nối AI");
      }
      setChatHistory(prev => [
        ...prev,
        { role: "model", content: errMsg, isError: true },
      ]);
    } finally {
      setLoading(false);
    }
  }, [input, loading, chatHistory, cfg.apiRole, userLocation]);

  const handleKeyDown = (e) => {
    if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send(); }
  };

  const clearHistory = () => { setChatHistory([]); };

  if (!role || !ROLE_CONFIG[role]) return null;

  return (
    <>
      {/* Floating Button */}
      <button
        onClick={() => setOpen(o => !o)}
        aria-label="AI Copilot"
        className="ai-copilot-fab"
        style={{
          position: "fixed", right: 20, zIndex: 9990,
          width: 52, height: 52, borderRadius: "50%", border: "none",
          background: `linear-gradient(135deg, ${cfg.accent}, ${cfg.accent}cc)`,
          color: "#fff", fontSize: 22, cursor: "pointer",
          boxShadow: `0 4px 20px ${cfg.accent}66`,
          display: "flex", alignItems: "center", justifyContent: "center",
          transition: "transform .2s, box-shadow .2s",
        }}
        onMouseEnter={e => { e.currentTarget.style.transform = "scale(1.1)"; }}
        onMouseLeave={e => { e.currentTarget.style.transform = "scale(1)"; }}
      >
        {open ? "✕" : cfg.emoji}
        {/* Pulse ring when closed */}
        {!open && (
          <span style={{
            position: "absolute", inset: -3, borderRadius: "50%",
            border: `2px solid ${cfg.accent}66`,
            animation: "ai-ping 2s infinite",
            pointerEvents: "none",
          }} />
        )}
      </button>

      {/* Chat Panel */}
      {open && (
        <div
          className="ai-copilot-panel"
          style={{
            position: "fixed", zIndex: 9989,
            width: "min(420px, calc(100vw - 48px))",
            height: "min(580px, calc(100vh - 120px))",
            background: "#fff",
            borderRadius: 20,
            boxShadow: "0 24px 80px rgba(0,0,0,0.15), 0 4px 16px rgba(0,0,0,0.08)",
            display: "flex", flexDirection: "column",
            overflow: "hidden",
            animation: "ai-slide-up .2s cubic-bezier(0.34,1.56,0.64,1)",
          }}
        >
          {/* Header */}
          <div style={{
            padding: "14px 16px",
            background: `linear-gradient(135deg, ${cfg.accent}, ${cfg.accent}cc)`,
            display: "flex", alignItems: "center", gap: 10,
          }}>
            <div style={{
              width: 36, height: 36, borderRadius: "50%",
              background: "rgba(255,255,255,0.2)",
              display: "flex", alignItems: "center", justifyContent: "center",
              fontSize: 18,
            }}>
              {cfg.emoji}
            </div>
            <div style={{ flex: 1 }}>
              <div style={{ color: "#fff", fontWeight: 700, fontSize: 15 }}>{cfg.label}</div>
              <div style={{ color: "rgba(255,255,255,0.75)", fontSize: 11 }}>
                {loading ? "⏳ Đang xử lý..." : "● Trực tuyến"}
              </div>
            </div>
            <button
              onClick={clearHistory}
              title="Xoá lịch sử"
              style={{
                background: "rgba(255,255,255,0.15)", border: "none",
                borderRadius: 8, color: "#fff", padding: "4px 8px",
                fontSize: 11, cursor: "pointer",
              }}
            >
              🗑️ Xoá
            </button>
          </div>

          {/* Messages */}
          <div style={{
            flex: 1, overflowY: "auto", padding: "12px 14px",
            display: "flex", flexDirection: "column", gap: 10,
            background: "#f8fafc",
          }}>
            {chatHistory.length === 0 && (
              <div style={{ textAlign: "center", padding: "24px 16px" }}>
                <div style={{ fontSize: 40, marginBottom: 8 }}>{cfg.emoji}</div>
                {/* Lời chào hardcode — KHÔNG gọi API, theo đúng spec BE */}
                <div className="ai-markdown" style={{ textAlign: "left", marginBottom: 16 }}>
                  <ReactMarkdown remarkPlugins={[remarkGfm]}>
                    {WELCOME_GREETINGS[role] || `Xin chào! Tôi là ${cfg.label}. Hỏi tôi bất cứ điều gì!`}
                  </ReactMarkdown>
                </div>
                {/* Suggestions */}
                <div style={{ display: "flex", flexWrap: "wrap", gap: 6, justifyContent: "center" }}>
                  {(SUGGESTIONS[role] || []).map(s => (
                    <button key={s} onClick={() => send(s)} style={{
                      padding: "6px 12px", borderRadius: 20,
                      border: `1px solid ${cfg.accent}40`,
                      background: `${cfg.accent}10`,
                      color: cfg.accent, fontSize: 12, cursor: "pointer",
                      fontWeight: 500,
                    }}>
                      {s}
                    </button>
                  ))}
                </div>
              </div>
            )}

            {chatHistory.map((msg, idx) => {
              const isUser = msg.role === "user";
              return (
                <div key={idx} style={{ display: "flex", justifyContent: isUser ? "flex-end" : "flex-start" }}>
                  {!isUser && (
                    <div style={{
                      width: 28, height: 28, borderRadius: "50%",
                      background: `${cfg.accent}18`,
                      display: "flex", alignItems: "center",
                      justifyContent: "center", fontSize: 14,
                      marginRight: 6, flexShrink: 0, alignSelf: "flex-end",
                    }}>
                      {cfg.emoji}
                    </div>
                  )}
                  <div style={{
                    maxWidth: "80%",
                    padding: "10px 14px",
                    borderRadius: isUser ? "18px 18px 4px 18px" : "18px 18px 18px 4px",
                    background: isUser
                      ? `linear-gradient(135deg, ${cfg.accent}, ${cfg.accent}cc)`
                      : msg.isError ? "#fef2f2" : "#fff",
                    color: isUser ? "#fff" : "#1e293b",
                    fontSize: 13.5, lineHeight: 1.55,
                    boxShadow: "0 1px 4px rgba(0,0,0,0.06)",
                    border: msg.isError ? "1px solid #fecaca" : "none",
                  }}>
                    {isUser ? (
                      <span>{msg.content}</span>
                    ) : (
                      <div className="ai-markdown">
                        <ReactMarkdown remarkPlugins={[remarkGfm]}>
                          {msg.content}
                        </ReactMarkdown>
                      </div>
                    )}
                  </div>
                </div>
              );
            })}

            {/* Typing indicator */}
            {loading && (
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                  <div style={{
                    width: 28, height: 28, borderRadius: "50%",
                    background: `${cfg.accent}18`,
                    display: "flex", alignItems: "center",
                    justifyContent: "center", fontSize: 14, flexShrink: 0,
                  }}>
                    {cfg.emoji}
                  </div>
                  <div style={{
                    padding: "10px 16px", background: "#fff",
                    borderRadius: "18px 18px 18px 4px",
                    boxShadow: "0 1px 4px rgba(0,0,0,0.06)",
                    display: "flex", gap: 4, alignItems: "center",
                  }}>
                    {[0, 1, 2].map(i => (
                      <span key={i} style={{
                        width: 7, height: 7, borderRadius: "50%",
                        background: cfg.accent,
                        display: "inline-block",
                        animation: `ai-dot 1.2s ${i * 0.2}s ease-in-out infinite`,
                      }} />
                    ))}
                  </div>
                </div>
                {/* Cảnh báo sau 15s: AI đang suy nghĩ */}
                {slowWarning && (
                  <div style={{
                    fontSize: 11, color: "#f59e0b", textAlign: "center",
                    padding: "4px 12px", animation: "ai-dot 1.5s infinite",
                  }}>
                    ⏳ AI đang suy nghĩ... (có thể mất đến 30 giây)
                  </div>
                )}
              </div>
            )}
            <div ref={bottomRef} />
          </div>

          {/* Input */}
          <div style={{
            padding: "10px 12px",
            borderTop: "1px solid #e5e7eb",
            background: "#fff",
            display: "flex", gap: 8, alignItems: "flex-end",
          }}>
            <div style={{ flex: 1, display: "flex", flexDirection: "column", gap: 2 }}>
              <textarea
                ref={inputRef}
                value={input}
                onChange={e => setInput(e.target.value.slice(0, MAX_MSG_LENGTH))}
                onKeyDown={handleKeyDown}
                placeholder="Nhập câu hỏi... (Enter để gửi)"
                rows={1}
                style={{
                  width: "100%", padding: "10px 12px",
                  borderRadius: 12, border: "1.5px solid #e5e7eb",
                  fontSize: 13.5, outline: "none", resize: "none",
                  fontFamily: "inherit", lineHeight: 1.5,
                  maxHeight: 80, overflowY: "auto",
                  transition: "border-color .15s",
                  boxSizing: "border-box",
                }}
                onFocus={e => { e.target.style.borderColor = cfg.accent; }}
                onBlur={e => { e.target.style.borderColor = "#e5e7eb"; }}
              />
              {input.length > 400 && (
                <span style={{ fontSize: 10, color: input.length >= MAX_MSG_LENGTH ? "#ef4444" : "#94a3b8", textAlign: "right" }}>
                  {input.length}/{MAX_MSG_LENGTH}
                </span>
              )}
            </div>
            <button
              onClick={() => send()}
              disabled={!input.trim() || loading}
              style={{
                width: 40, height: 40, borderRadius: 11, border: "none",
                background: input.trim() && !loading
                  ? `linear-gradient(135deg, ${cfg.accent}, ${cfg.accent}cc)`
                  : "#e5e7eb",
                color: "#fff",
                cursor: input.trim() && !loading ? "pointer" : "not-allowed",
                display: "flex", alignItems: "center", justifyContent: "center",
                flexShrink: 0, transition: "all .15s",
              }}
            >
              {loading ? (
                <span style={{
                  width: 16, height: 16, border: "2px solid #fff",
                  borderTopColor: "transparent", borderRadius: "50%",
                  display: "inline-block",
                  animation: "ai-spin .7s linear infinite",
                }} />
              ) : (
                <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                  <line x1="22" y1="2" x2="11" y2="13" />
                  <polygon points="22 2 15 22 11 13 2 9 22 2" />
                </svg>
              )}
            </button>
          </div>
        </div>
      )}

      <style>{`
        /* AI Copilot FAB position */
        .ai-copilot-fab {
          bottom: 24px;
        }
        .ai-copilot-panel {
          bottom: 92px;
          right: 24px;
        }
        @media (max-width: 768px) {
          .ai-copilot-fab {
            bottom: calc(90px + env(safe-area-inset-bottom, 0px));
            width: 48px !important;
            height: 48px !important;
            right: 14px !important;
          }
          .ai-copilot-panel {
            bottom: calc(150px + env(safe-area-inset-bottom, 0px)) !important;
            right: 14px !important;
            width: calc(100vw - 28px) !important;
          }
        }
        @keyframes ai-ping {
          0%, 100% { transform: scale(1); opacity: 0.6; }
          50% { transform: scale(1.3); opacity: 0; }
        }
        @keyframes ai-slide-up {
          from { opacity: 0; transform: translateY(12px) scale(0.97); }
          to   { opacity: 1; transform: translateY(0) scale(1); }
        }
        @keyframes ai-dot {
          0%, 80%, 100% { transform: scale(0.6); opacity: 0.4; }
          40% { transform: scale(1); opacity: 1; }
        }
        @keyframes ai-spin {
          to { transform: rotate(360deg); }
        }
        .ai-markdown { font-size: 13.5px; line-height: 1.6; color: #1e293b; }
        .ai-markdown p { margin: 0 0 6px; }
        .ai-markdown p:last-child { margin: 0; }
        .ai-markdown strong { font-weight: 700; }
        .ai-markdown h1, .ai-markdown h2, .ai-markdown h3 {
          font-size: 14px; font-weight: 700; margin: 8px 0 4px;
        }
        .ai-markdown ul, .ai-markdown ol { margin: 4px 0 4px 16px; padding: 0; }
        .ai-markdown li { margin-bottom: 2px; }
        .ai-markdown table {
          border-collapse: collapse; font-size: 12px; width: 100%; margin: 6px 0;
        }
        .ai-markdown th, .ai-markdown td {
          border: 1px solid #e2e8f0; padding: 4px 8px; text-align: left;
        }
        .ai-markdown th { background: #f1f5f9; font-weight: 700; }
        .ai-markdown code {
          background: #f1f5f9; padding: 1px 5px; border-radius: 4px;
          font-size: 12px; font-family: monospace;
        }
        .ai-markdown blockquote {
          border-left: 3px solid #e2e8f0; margin: 4px 0; padding-left: 10px;
          color: #64748b; font-style: italic;
        }
      `}</style>
    </>
  );
}
