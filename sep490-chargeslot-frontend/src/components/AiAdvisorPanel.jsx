import { useEffect, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { instance } from "@/lib/httpRequest";

/**
 * AiAdvisorPanel — Panel "Cố Vấn Chiến Lược AI"
 * Props:
 *   role {string} — 'admin' hoặc 'owner'
 */
export function AiAdvisorPanel({ role }) {
  const [insight, setInsight] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setIsLoading(true);
    setError(null);

    const url =
      role === "admin"
        ? "/admin/analytics/ai-insights"
        : "/owner/analytics/ai-insights";

    instance
      .get(url)
      .then((res) => {
        if (!cancelled) {
          setInsight(res.data?.insightMarkdown || res.data?.insight || "");
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setError(
            err?.response?.data?.message ||
            err?.message ||
            "Không thể kết nối với Cố vấn AI lúc này."
          );
        }
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });

    return () => { cancelled = true; };
  }, [role]);

  return (
    <div style={{
      background: "linear-gradient(135deg, #0f172a 0%, #1e1b4b 100%)",
      color: "#f1f5f9",
      borderRadius: 20,
      padding: "28px 32px",
      boxShadow: "0 20px 60px rgba(0,0,0,0.3), 0 0 0 1px rgba(217,119,6,0.3)",
      border: "1px solid rgba(217,119,6,0.4)",
      position: "relative",
      overflow: "hidden",
    }}>
      {/* Glow effect background */}
      <div style={{
        position: "absolute",
        top: -60,
        right: -60,
        width: 200,
        height: 200,
        background: "radial-gradient(circle, rgba(217,119,6,0.15) 0%, transparent 70%)",
        pointerEvents: "none",
      }} />
      <div style={{
        position: "absolute",
        bottom: -40,
        left: -40,
        width: 150,
        height: 150,
        background: "radial-gradient(circle, rgba(99,102,241,0.1) 0%, transparent 70%)",
        pointerEvents: "none",
      }} />

      {/* Header */}
      <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 20, position: "relative" }}>
        <div style={{
          width: 36,
          height: 36,
          borderRadius: 10,
          background: "linear-gradient(135deg, #d97706, #b45309)",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: 18,
          flexShrink: 0,
          boxShadow: "0 4px 12px rgba(217,119,6,0.4)",
        }}>
          ✨
        </div>
        <div>
          <h3 style={{ margin: 0, fontSize: 16, fontWeight: 800, color: "#fbbf24", letterSpacing: "0.3px" }}>
            CỐ VẤN CHIẾN LƯỢC AI
          </h3>
          <p style={{ margin: 0, fontSize: 12, color: "#94a3b8" }}>
            {isLoading ? (
              <span style={{ animation: "ai-pulse 1.5s ease-in-out infinite" }}>
                ⏳ đang phân tích dữ liệu và soạn báo cáo...
              </span>
            ) : error ? (
              <span style={{ color: "#f87171" }}>Lỗi kết nối AI</span>
            ) : (
              <span style={{ color: "#34d399" }}>✓ Phân tích hoàn tất</span>
            )}
          </p>
        </div>
      </div>

      {/* Divider */}
      <div style={{
        height: 1,
        background: "linear-gradient(90deg, transparent, rgba(217,119,6,0.5), transparent)",
        marginBottom: 20,
      }} />

      {/* Content */}
      {isLoading ? (
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          {[80, 100, 65, 90, 75].map((w, i) => (
            <div
              key={i}
              style={{
                height: 14,
                borderRadius: 7,
                background: "rgba(148,163,184,0.15)",
                width: `${w}%`,
                animation: `ai-pulse 1.8s ease-in-out ${i * 0.15}s infinite`,
              }}
            />
          ))}
        </div>
      ) : error ? (
        <div style={{
          background: "rgba(239,68,68,0.1)",
          border: "1px solid rgba(239,68,68,0.3)",
          borderRadius: 12,
          padding: "14px 18px",
          color: "#fca5a5",
          fontSize: 14,
        }}>
          ⚠️ {error}
        </div>
      ) : insight ? (
        <div style={{ position: "relative" }}>
          <AiMarkdownContent content={insight} />
        </div>
      ) : (
        <div style={{ color: "#64748b", fontSize: 14, textAlign: "center", padding: "20px 0" }}>
          Chưa có dữ liệu đủ để phân tích.
        </div>
      )}

      <style>{`
        @keyframes ai-pulse {
          0%, 100% { opacity: 0.4; }
          50% { opacity: 1; }
        }
        .ai-md-content h1, .ai-md-content h2, .ai-md-content h3 {
          color: #fbbf24;
          font-weight: 700;
          margin: 16px 0 8px;
          line-height: 1.3;
        }
        .ai-md-content h1 { font-size: 18px; }
        .ai-md-content h2 { font-size: 16px; }
        .ai-md-content h3 { font-size: 14px; }
        .ai-md-content p {
          color: #cbd5e1;
          font-size: 14px;
          line-height: 1.8;
          margin: 8px 0;
        }
        .ai-md-content strong {
          color: #fde68a;
          font-weight: 700;
        }
        .ai-md-content em {
          color: #a5f3fc;
          font-style: italic;
        }
        .ai-md-content ul, .ai-md-content ol {
          color: #cbd5e1;
          font-size: 14px;
          line-height: 1.8;
          padding-left: 20px;
          margin: 8px 0;
        }
        .ai-md-content li { margin: 4px 0; }
        .ai-md-content li::marker { color: #fbbf24; }
        .ai-md-content code {
          background: rgba(99,102,241,0.2);
          color: #c4b5fd;
          padding: 2px 6px;
          border-radius: 4px;
          font-size: 12px;
        }
        .ai-md-content blockquote {
          border-left: 3px solid #d97706;
          padding-left: 12px;
          margin: 12px 0;
          color: #94a3b8;
          font-style: italic;
        }
        .ai-md-content hr {
          border: none;
          border-top: 1px solid rgba(217,119,6,0.3);
          margin: 16px 0;
        }
        .ai-md-content table {
          width: 100%;
          border-collapse: collapse;
          font-size: 13px;
          margin: 12px 0;
        }
        .ai-md-content th {
          background: rgba(217,119,6,0.2);
          color: #fbbf24;
          padding: 8px 12px;
          text-align: left;
          font-weight: 700;
          border: 1px solid rgba(217,119,6,0.2);
        }
        .ai-md-content td {
          padding: 8px 12px;
          color: #cbd5e1;
          border: 1px solid rgba(148,163,184,0.1);
        }
        .ai-md-content tr:nth-child(even) td { background: rgba(255,255,255,0.03); }
      `}</style>
    </div>
  );
}

function AiMarkdownContent({ content }) {
  return (
    <div className="ai-md-content">
      <ReactMarkdown remarkPlugins={[remarkGfm]}>
        {content}
      </ReactMarkdown>
    </div>
  );
}
