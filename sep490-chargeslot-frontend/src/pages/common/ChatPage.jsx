import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { chatApi } from "@/services/api";
import ChatWindow from "@/components/ChatWindow";

export default function ChatPage() {
  const { bookingId } = useParams();
  const navigate = useNavigate();
  const [conversationId, setConversationId] = useState(null);
  const [messages, setMessages] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!bookingId) return;
    chatApi.getMessages(Number(bookingId))
      .then(data => {
        setConversationId(data?.conversationId || null);
        setMessages(data?.messages || []);
      })
      .catch(() => {
        setConversationId(null);
        setMessages([]);
      })
      .finally(() => setLoading(false));
  }, [bookingId]);

  const handleNewMessage = useCallback((msg) => {
    setMessages(prev => {
      // Avoid duplicates
      if (prev.some(m => m.id === msg.id)) return prev;
      return [...prev, msg];
    });
    // If we didn't have a conversationId before, we might now
    if (!conversationId && msg.conversationId) {
      setConversationId(msg.conversationId);
    }
  }, [conversationId]);

  // First message via REST — set conversationId after
  const handleFirstMessage = useCallback(async (msg) => {
    handleNewMessage(msg);
    // Re-fetch to get conversationId
    try {
      const data = await chatApi.getMessages(Number(bookingId));
      if (data?.conversationId) {
        setConversationId(data.conversationId);
      }
    } catch { /* ignore */ }
  }, [bookingId, handleNewMessage]);

  const role = (localStorage.getItem("role") || "").toLowerCase();

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
        <div style={{
          display: "flex", alignItems: "center", gap: 12, padding: "12px 0",
        }}>
          <button
            onClick={() => navigate(-1)}
            style={{
              background: "none", border: "none", cursor: "pointer",
              color: "#6b7280", display: "flex", alignItems: "center", gap: 4, fontSize: 14,
            }}
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M19 12H5M12 19l-7-7 7-7" /></svg>
            Quay lại
          </button>
          <h2 style={{ fontSize: 16, fontWeight: 700, color: "#1e293b" }}>
            💬 Chat — Booking #{bookingId}
          </h2>
        </div>

        {/* Chat window */}
        <div style={{ flex: 1, minHeight: 0 }}>
          <ChatWindow
            conversationId={conversationId}
            bookingId={Number(bookingId)}
            messages={messages}
            onNewMessage={conversationId ? handleNewMessage : handleFirstMessage}
          />
        </div>
      </div>
    </div>
  );
}
