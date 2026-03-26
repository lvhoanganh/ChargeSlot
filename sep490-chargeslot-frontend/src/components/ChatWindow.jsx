import { useState, useEffect, useRef, useCallback } from "react";
import * as signalR from "@microsoft/signalr";

const API_BASE = "http://localhost:5162";

/**
 * Reusable ChatWindow component — used by both Driver and Owner
 * Props: { conversationId, bookingId, messages: [], onNewMessage, currentUserId }
 */
export default function ChatWindow({ conversationId, bookingId, messages = [], onNewMessage, currentUserId }) {
  const [input, setInput] = useState("");
  const [sending, setSending] = useState(false);
  const [connection, setConnection] = useState(null);
  const messagesEndRef = useRef(null);
  const connectionRef = useRef(null);

  // Scroll to bottom on new messages
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  // SignalR connection
  useEffect(() => {
    if (!conversationId) return;

    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/chat`, {
        accessTokenFactory: () => localStorage.getItem("accessToken"),
      })
      .withAutomaticReconnect()
      .build();

    conn.on("ReceiveMessage", (message) => {
      onNewMessage?.(message);
    });

    conn.on("MessagesRead", (convId, readerUserId) => {
      // Could update read status here
    });

    conn.start()
      .then(() => {
        conn.invoke("JoinConversation", conversationId).catch(() => {});
        connectionRef.current = conn;
        setConnection(conn);
      })
      .catch(err => console.error("SignalR connection error:", err));

    return () => {
      if (connectionRef.current) {
        connectionRef.current.invoke("LeaveConversation", conversationId).catch(() => {});
        connectionRef.current.stop().catch(() => {});
      }
    };
  }, [conversationId]);

  // Mark as read when component mounts with conversationId
  useEffect(() => {
    if (connection && conversationId) {
      connection.invoke("MarkAsRead", conversationId).catch(() => {});
    }
  }, [connection, conversationId, messages.length]);

  const handleSend = useCallback(async () => {
    const text = input.trim();
    if (!text || sending) return;

    setSending(true);
    try {
      // If we have a SignalR connection and conversationId, use SignalR
      if (connection && conversationId) {
        await connection.invoke("SendMessage", conversationId, text);
      } else {
        // Fallback: use REST API (first message scenario)
        const { chatApi } = await import("@/services/api");
        const msg = await chatApi.sendMessage(bookingId, text);
        onNewMessage?.(msg);
      }
      setInput("");
    } catch (err) {
      console.error("Send error:", err);
    } finally {
      setSending(false);
    }
  }, [input, sending, connection, conversationId, bookingId, onNewMessage]);

  const userId = currentUserId || Number(localStorage.getItem("userId"));

  return (
    <div style={{
      display: "flex", flexDirection: "column", height: "100%",
      background: "#f8fafc", borderRadius: 16, overflow: "hidden",
    }}>
      {/* Messages */}
      <div style={{
        flex: 1, overflow: "auto", padding: "16px 16px 8px",
        display: "flex", flexDirection: "column", gap: 8,
      }}>
        {messages.length === 0 && (
          <div style={{ textAlign: "center", padding: 40, color: "#94a3b8" }}>
            <div style={{ fontSize: 36, marginBottom: 8 }}>💬</div>
            <p style={{ fontSize: 14 }}>Chưa có tin nhắn. Hãy gửi tin nhắn đầu tiên!</p>
          </div>
        )}

        {messages.map((msg, idx) => {
          const isMine = msg.senderUserId === userId;
          return (
            <div key={msg.id || idx} style={{
              display: "flex", justifyContent: isMine ? "flex-end" : "flex-start",
            }}>
              <div style={{
                maxWidth: "75%",
                padding: "10px 14px",
                borderRadius: isMine ? "16px 16px 4px 16px" : "16px 16px 16px 4px",
                background: isMine
                  ? "linear-gradient(135deg, #f97316, #ea580c)"
                  : "#fff",
                color: isMine ? "#fff" : "#1e293b",
                boxShadow: "0 1px 4px rgba(0,0,0,0.06)",
              }}>
                {!isMine && (
                  <div style={{ fontSize: 11, fontWeight: 700, color: "#f97316", marginBottom: 2 }}>
                    {msg.senderName}
                  </div>
                )}
                <div style={{ fontSize: 14, lineHeight: 1.5 }}>{msg.content}</div>
                <div style={{
                  fontSize: 10, marginTop: 4, textAlign: "right",
                  opacity: 0.7,
                  color: isMine ? "rgba(255,255,255,0.8)" : "#94a3b8",
                }}>
                  {new Date(String(msg.createdAt).replace("Z", "")).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}
                  {isMine && msg.isRead && " ✓✓"}
                </div>
              </div>
            </div>
          );
        })}
        <div ref={messagesEndRef} />
      </div>

      {/* Input */}
      <div style={{
        padding: "12px 16px", background: "#fff",
        borderTop: "1px solid #e5e7eb",
        display: "flex", alignItems: "center", gap: 8,
      }}>
        <input
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={e => e.key === "Enter" && !e.shiftKey && handleSend()}
          placeholder="Nhập tin nhắn..."
          style={{
            flex: 1, padding: "10px 14px", borderRadius: 12,
            border: "1.5px solid #e5e7eb", fontSize: 14, outline: "none",
          }}
        />
        <button
          onClick={handleSend}
          disabled={!input.trim() || sending}
          style={{
            width: 42, height: 42, borderRadius: 12, border: "none",
            background: input.trim() ? "linear-gradient(135deg, #f97316, #ea580c)" : "#e5e7eb",
            color: "#fff", cursor: input.trim() ? "pointer" : "not-allowed",
            display: "flex", alignItems: "center", justifyContent: "center",
            transition: "all .15s",
          }}
        >
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
            <line x1="22" y1="2" x2="11" y2="13" />
            <polygon points="22 2 15 22 11 13 2 9 22 2" />
          </svg>
        </button>
      </div>
    </div>
  );
}
