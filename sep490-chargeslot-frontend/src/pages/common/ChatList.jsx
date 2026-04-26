import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { chatApi } from "@/services/api";
import { getCurrentRole } from "@/services/api";
import Pagination from "@/components/Pagination";

export default function ChatList() {
  const navigate = useNavigate();
  const [conversations, setConversations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const role = (localStorage.getItem("role") || "").toLowerCase();

  useEffect(() => {
    setLoading(true);
    chatApi.getConversations(page, 20)
      .then(data => {
        const list = Array.isArray(data) ? data : (data?.items ?? []);
        setConversations(list);
        setTotalCount(data?.totalCount ?? data?.total ?? list.length);
      })
      .catch(() => setConversations([]))
      .finally(() => setLoading(false));
  }, [page]);

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 40 }}></div>
        <p style={{ color: "#6b7280" }}>Đang tải cuộc trò chuyện...</p>
      </div>
    );
  }

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90 }}>
      <div style={{ maxWidth: 700, margin: "0 auto", padding: "0 16px 40px" }}>
        <h1 style={{ fontSize: 24, fontWeight: 800, color: "#1e293b", marginBottom: 20 }}>
           Tin nhắn
        </h1>

        {conversations.length === 0 ? (
          <div style={{
            textAlign: "center", padding: 40, background: "#fff", borderRadius: 16,
            boxShadow: "0 2px 8px rgba(0,0,0,0.04)",
          }}>
            <div style={{ fontSize: 48, marginBottom: 8 }}></div>
            <p style={{ color: "#6b7280" }}>Chưa có cuộc trò chuyện nào</p>
            <p style={{ color: "#9ca3af", fontSize: 13, marginTop: 4 }}>
              Bắt đầu chat từ chi tiết booking!
            </p>
          </div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {conversations.map(conv => {
              const chatPath = role === "owner"
                ? `/owner/chat/${conv.bookingId}`
                : `/driver/chat/${conv.bookingId}`;

              return (
                <div
                  key={conv.id}
                  onClick={() => navigate(chatPath)}
                  style={{
                    background: "#fff", borderRadius: 16, padding: "16px 20px",
                    boxShadow: "0 2px 8px rgba(0,0,0,0.04)", display: "flex",
                    alignItems: "center", gap: 14, cursor: "pointer",
                    border: conv.unreadCount > 0 ? "2px solid #f97316" : "1px solid #f1f5f9",
                    transition: "all .15s",
                  }}
                >
                  {/* Avatar */}
                  <div style={{
                    width: 48, height: 48, borderRadius: 14,
                    background: "linear-gradient(135deg, #f97316, #ea580c)",
                    display: "flex", alignItems: "center", justifyContent: "center",
                    color: "#fff", fontWeight: 700, fontSize: 18, flexShrink: 0,
                  }}>
                    {(conv.otherUserName || "?")[0]?.toUpperCase()}
                  </div>

                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                      <span style={{ fontWeight: 700, fontSize: 14, color: "#1e293b" }}>
                        {conv.otherUserName}
                      </span>
                      <span style={{ fontSize: 11, color: "#94a3b8", flexShrink: 0 }}>
                         {conv.stationName}
                      </span>
                    </div>
                    <p style={{
                      fontSize: 13, color: conv.unreadCount > 0 ? "#1e293b" : "#6b7280",
                      fontWeight: conv.unreadCount > 0 ? 600 : 400,
                      marginTop: 2, overflow: "hidden", textOverflow: "ellipsis",
                      whiteSpace: "nowrap",
                    }}>
                      {conv.lastMessage || "Chưa có tin nhắn"}
                    </p>
                  </div>

                  <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 4, flexShrink: 0 }}>
                    <span style={{ fontSize: 11, color: "#94a3b8" }}>
                      {conv.lastMessageAt ? new Date(String(conv.lastMessageAt).replace("Z", "")).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" }) : ""}
                    </span>
                    {conv.unreadCount > 0 && (
                      <span style={{
                        background: "#f97316", color: "#fff", borderRadius: 10,
                        padding: "2px 8px", fontSize: 11, fontWeight: 700,
                        minWidth: 20, textAlign: "center",
                      }}>
                        {conv.unreadCount}
                      </span>
                    )}
                  </div>
                </div>
              );
            })}
            <Pagination 
              page={page} 
              totalCount={totalCount} 
              pageSize={20} 
              onPageChange={(p) => setPage(p)} 
            />
          </div>
        )}
      </div>
    </div>
  );
}
