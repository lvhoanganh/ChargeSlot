import { useState, useEffect } from "react";
import { stationApi, reviewApi } from "@/services/api";
import { showToast } from "@/components/Toast";

const STARS = [1, 2, 3, 4, 5];

const toVN = (dt) => {
  if (!dt) return "";
  const s = String(dt);
  return new Date(String(s).replace("Z", "")).toLocaleString("vi-VN");
};

export default function OwnerReviews() {
  const [stations, setStations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [selectedStation, setSelectedStation] = useState(null);
  const [summary, setSummary] = useState(null);
  const [reviews, setReviews] = useState([]);
  const [reviewLoading, setReviewLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [replyingId, setReplyingId] = useState(null);
  const [replyText, setReplyText] = useState("");
  const [replySubmitting, setReplySubmitting] = useState(false);

  // Load owner's stations
  useEffect(() => {
    stationApi.getAll()
      .then((data) => {
        const list = Array.isArray(data) ? data : data?.items || [];
        const approvedList = list.filter(st => st.approvalStatus === "Approved");
        setStations(approvedList);
        if (approvedList.length > 0) loadStation(approvedList[0]);
      })
      .catch(() => setStations([]))
      .finally(() => setLoading(false));
  }, []);

  function loadStation(station) {
    setSelectedStation(station);
    setPage(1);
    setReviewLoading(true);
    Promise.all([
      reviewApi.getSummary(station.id).catch(() => null),
      reviewApi.getByStation(station.id, 1, 20).catch(() => []),
    ]).then(([sum, revs]) => {
      setSummary(sum);
      setReviews(Array.isArray(revs) ? revs : revs?.items || []);
    }).finally(() => setReviewLoading(false));
  }

  async function handleReply(reviewId) {
    if (!replyText.trim()) return;
    setReplySubmitting(true);
    try {
      await reviewApi.reply(reviewId, replyText.trim());
      showToast.success("Đã gửi phản hồi!");
      setReplyingId(null);
      setReplyText("");
      // Reload reviews
      const revs = await reviewApi.getByStation(selectedStation.id, 1, 20).catch(() => []);
      setReviews(Array.isArray(revs) ? revs : revs?.items || []);
    } catch (err) {
      showToast.error(err?.message || "Lỗi gửi phản hồi");
    } finally {
      setReplySubmitting(false);
    }
  }

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 40 }}>⭐</div>
        <p style={{ color: "#6b7280" }}>Đang tải...</p>
      </div>
    );
  }

  if (stations.length === 0) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 48, marginBottom: 12 }}>📋</div>
        <p style={{ fontSize: 16, color: "#64748b", fontWeight: 600 }}>Bạn chưa có trạm sạc nào</p>
      </div>
    );
  }

  const avgRating = summary?.averageRating ?? 0;
  const totalReviews = summary?.totalReviews ?? 0;

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90 }}>
      <div style={{ maxWidth: 800, margin: "0 auto", padding: "0 16px 40px" }}>
        <h1 style={{ fontSize: 24, fontWeight: 800, color: "#1e293b", marginBottom: 6 }}>
          ⭐ Đánh giá trạm sạc
        </h1>
        <p style={{ fontSize: 14, color: "#64748b", marginBottom: 20 }}>
          Xem đánh giá từ driver và phản hồi.
        </p>

        {/* Station selector */}
        <div style={{ marginBottom: 20 }}>
          <label style={{ fontSize: 13, fontWeight: 600, color: "#6b7280", marginBottom: 6, display: "block" }}>
            Chọn trạm sạc
          </label>
          <select
            value={selectedStation?.id || ""}
            onChange={(e) => {
              const st = stations.find(s => s.id === Number(e.target.value));
              if (st) loadStation(st);
            }}
            style={{
              width: "100%", padding: "12px 16px", borderRadius: 12,
              border: "1.5px solid #e5e7eb", fontSize: 14, fontWeight: 600,
              color: "#1e293b", background: "#fff", cursor: "pointer",
              outline: "none", appearance: "auto",
            }}
          >
            {stations.map((st) => (
              <option key={st.id} value={st.id}>{st.name}</option>
            ))}
          </select>
        </div>

        {/* Rating summary */}
        {summary && (
          <div style={{
            background: "#fff", borderRadius: 20, padding: 24,
            boxShadow: "0 2px 12px rgba(0,0,0,0.06)", marginBottom: 24,
            display: "flex", gap: 24, alignItems: "center", flexWrap: "wrap",
          }}>
            <div style={{ textAlign: "center", minWidth: 100 }}>
              <div style={{ fontSize: 42, fontWeight: 800, color: "#f59e0b" }}>
                {avgRating.toFixed(1)}
              </div>
              <div style={{ display: "flex", justifyContent: "center", gap: 2, marginBottom: 4 }}>
                {STARS.map((s) => (
                  <span key={s} style={{ fontSize: 16, color: s <= Math.round(avgRating) ? "#f59e0b" : "#d1d5db" }}>★</span>
                ))}
              </div>
              <div style={{ fontSize: 12, color: "#6b7280" }}>{totalReviews} đánh giá</div>
            </div>
            <div style={{ flex: 1, minWidth: 200 }}>
              {[5, 4, 3, 2, 1].map((star) => {
                const count = summary[`star${star}`] || 0;
                const pct = totalReviews > 0 ? (count / totalReviews) * 100 : 0;
                return (
                  <div key={star} style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 4 }}>
                    <span style={{ fontSize: 12, fontWeight: 600, color: "#6b7280", width: 40 }}>{star} ★</span>
                    <div style={{ flex: 1, height: 8, background: "#f1f5f9", borderRadius: 4, overflow: "hidden" }}>
                      <div style={{ height: "100%", width: `${pct}%`, background: "#f59e0b", borderRadius: 4, transition: "width 0.5s" }} />
                    </div>
                    <span style={{ fontSize: 11, color: "#94a3b8", width: 24, textAlign: "right" }}>{count}</span>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* Reviews list */}
        {reviewLoading ? (
          <div style={{ textAlign: "center", padding: 40 }}>
            <p style={{ color: "#6b7280" }}>Đang tải đánh giá...</p>
          </div>
        ) : reviews.length === 0 ? (
          <div style={{
            textAlign: "center", padding: 40, background: "#fff", borderRadius: 16,
            boxShadow: "0 2px 8px rgba(0,0,0,0.04)",
          }}>
            <div style={{ fontSize: 48, marginBottom: 8 }}>📝</div>
            <p style={{ color: "#6b7280" }}>Chưa có đánh giá nào cho trạm này</p>
          </div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
            {reviews.map((r) => (
              <div key={r.id} style={{
                background: "#fff", borderRadius: 16, padding: 20,
                boxShadow: "0 2px 8px rgba(0,0,0,0.04)",
              }}>
                {/* Header */}
                <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 8 }}>
                  <div style={{
                    width: 36, height: 36, borderRadius: "50%",
                    background: "#f1f5f9", display: "flex", alignItems: "center",
                    justifyContent: "center", fontSize: 16, flexShrink: 0,
                  }}>
                    👤
                  </div>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontWeight: 700, fontSize: 14, color: "#1e293b" }}>
                      {r.driverName || "Driver"}
                    </div>
                    <div style={{ fontSize: 11, color: "#94a3b8" }}>
                      {toVN(r.createdAt)}
                    </div>
                  </div>
                  <div style={{ display: "flex", gap: 1 }}>
                    {STARS.map((s) => (
                      <span key={s} style={{ fontSize: 14, color: s <= r.rating ? "#f59e0b" : "#d1d5db" }}>★</span>
                    ))}
                  </div>
                </div>

                {/* Comment */}
                {r.comment && (
                  <p style={{ fontSize: 14, color: "#374151", lineHeight: 1.5, marginBottom: 8 }}>
                    {r.comment}
                  </p>
                )}

                {/* Owner reply */}
                {r.ownerReply && (
                  <div style={{
                    background: "#f0fdf4", borderRadius: 10, padding: "10px 14px",
                    borderLeft: "3px solid #22c55e", marginBottom: 8,
                  }}>
                    <div style={{ fontSize: 11, fontWeight: 700, color: "#16a34a", marginBottom: 4 }}>
                      Phản hồi của bạn
                    </div>
                    <p style={{ fontSize: 13, color: "#374151", margin: 0 }}>{r.ownerReply}</p>
                    {r.ownerRepliedAt && (
                      <div style={{ fontSize: 10, color: "#94a3b8", marginTop: 4 }}>{toVN(r.ownerRepliedAt)}</div>
                    )}
                  </div>
                )}

                {/* Reply form */}
                {!r.ownerReply && (
                  <>
                    {replyingId === r.id ? (
                      <div style={{ marginTop: 8 }}>
                        <textarea
                          value={replyText}
                          onChange={(e) => setReplyText(e.target.value)}
                          placeholder="Nhập phản hồi..."
                          rows={2}
                          style={{
                            width: "100%", padding: 10, borderRadius: 8,
                            border: "1px solid #e5e7eb", fontSize: 13,
                            resize: "vertical", outline: "none", boxSizing: "border-box",
                          }}
                        />
                        <div style={{ display: "flex", gap: 8, marginTop: 8 }}>
                          <button
                            onClick={() => handleReply(r.id)}
                            disabled={replySubmitting || !replyText.trim()}
                            style={{
                              padding: "8px 16px", borderRadius: 8, border: "none",
                              background: replySubmitting ? "#d1d5db" : "#22c55e",
                              color: "#fff", fontWeight: 700, fontSize: 12,
                              cursor: replySubmitting ? "not-allowed" : "pointer",
                            }}
                          >
                            {replySubmitting ? "Đang gửi..." : "Gửi phản hồi"}
                          </button>
                          <button
                            onClick={() => { setReplyingId(null); setReplyText(""); }}
                            style={{
                              padding: "8px 16px", borderRadius: 8,
                              border: "1px solid #e5e7eb", background: "#fff",
                              color: "#6b7280", fontWeight: 600, fontSize: 12,
                              cursor: "pointer",
                            }}
                          >
                            Hủy
                          </button>
                        </div>
                      </div>
                    ) : (
                      <button
                        onClick={() => { setReplyingId(r.id); setReplyText(""); }}
                        style={{
                          marginTop: 4, padding: "6px 14px", borderRadius: 8,
                          border: "1px solid #e5e7eb", background: "#f8fafc",
                          color: "#6b7280", fontWeight: 600, fontSize: 12,
                          cursor: "pointer",
                        }}
                      >
                        💬 Phản hồi
                      </button>
                    )}
                  </>
                )}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
