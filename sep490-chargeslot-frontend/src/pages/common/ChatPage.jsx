import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { chatApi, bookingApi } from "@/services/api";
import ChatWindow from "@/components/ChatWindow";

const CLOSED_STATUSES = ["Completed", "Cancelled", "Rejected", "Expired", "NoShow"];

export default function ChatPage() {
  const { bookingId } = useParams();
  const navigate = useNavigate();
  const [conversationId, setConversationId] = useState(null);
  const [messages, setMessages] = useState([]);
  const [loading, setLoading] = useState(true);
  const [bookingStatus, setBookingStatus] = useState(null);

  useEffect(() => {
    if (!bookingId) return;
    // Fetch cả messages lẫn booking status song song
    Promise.all([
      chatApi.getMessages(Number(bookingId)).catch(() => null),
      bookingApi.getById(Number(bookingId)).catch(() => null),
    ]).then(([chatData, booking]) => {
      setConversationId(chatData?.conversationId || null);
      setMessages(chatData?.messages || []);
      setBookingStatus(booking?.status || null);
    }).finally(() => setLoading(false));
  }, [bookingId]);

  const handleNewMessage = useCallback((msg) => {
    setMessages(prev => {
      if (prev.some(m => m.id === msg.id)) return prev;
      return [...prev, msg];
    });
    if (!conversationId && msg.conversationId) {
      setConversationId(msg.conversationId);
    }
  }, [conversationId]);

  const handleFirstMessage = useCallback(async (msg) => {
    handleNewMessage(msg);
    try {
      const data = await chatApi.getMessages(Number(bookingId));
      if (data?.conversationId) setConversationId(data.conversationId);
    } catch { /* ignore */ }
  }, [bookingId, handleNewMessage]);

  // Chat chỉ đọc nếu booking đã kết thúc
  const isClosed = bookingStatus && CLOSED_STATUSES.includes(bookingStatus);

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 40 }}>💬</div>
        <p style={{ color: "#6b7280" }}>Đang tải tin nhắn...</p>
      </div>
    );
  }

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 80 }}>
      <div style={{ maxWidth: 700, margin: "0 auto", padding: "0 16px 16px", height: "calc(100vh - 80px)", display: "flex", flexDirection: "column" }}>
        {/* Header */}
        <div style={{ display: "flex", alignItems: "center", gap: 12, padding: "12px 0" }}>
          <button
            onClick={() => navigate(-1)}
            style={{ background: "none", border: "none", cursor: "pointer", color: "#6b7280", display: "flex", alignItems: "center", gap: 4, fontSize: 14 }}
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M19 12H5M12 19l-7-7 7-7" /></svg>
            Quay lại
          </button>
          <h2 style={{ fontSize: 16, fontWeight: 700, color: "#1e293b" }}>
            💬 Chat — Booking #{bookingId}
          </h2>
          {isClosed && (
            <span style={{
              marginLeft: "auto", fontSize: 11, fontWeight: 600,
              background: "#f1f5f9", color: "#64748b",
              padding: "3px 10px", borderRadius: 20,
            }}>
              🔒 Đã đóng
            </span>
          )}
        </div>

        {/* Thông báo nếu chat bị khoá */}
        {isClosed && (
          <div style={{
            background: "#fef9c3", border: "1px solid #fde047",
            borderRadius: 12, padding: "10px 16px", marginBottom: 8,
            fontSize: 13, color: "#713f12", display: "flex", alignItems: "center", gap: 8,
          }}>
            🔒 Phiên sạc đã kết thúc — không thể gửi tin nhắn mới.
          </div>
        )}

        {/* Chat window */}
        <div style={{ flex: 1, minHeight: 0 }}>
          <ChatWindow
            conversationId={conversationId}
            bookingId={Number(bookingId)}
            messages={messages}
            onNewMessage={conversationId ? handleNewMessage : handleFirstMessage}
            readOnly={isClosed}
          />
        </div>
      </div>
    </div>
  );
}
