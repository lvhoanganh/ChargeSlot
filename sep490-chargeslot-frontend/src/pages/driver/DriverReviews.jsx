import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { bookingApi, reviewApi } from "@/services/api";

const STARS = [1, 2, 3, 4, 5];

export default function DriverReviews() {
  const navigate = useNavigate();
  const [bookings, setBookings] = useState([]);
  const [loading, setLoading] = useState(true);
  const [reviewForm, setReviewForm] = useState(null); // { bookingId, rating, comment, isAnonymous }
  const [submitting, setSubmitting] = useState(false);
  const [successMsg, setSuccessMsg] = useState("");
  const [errorMsg, setErrorMsg] = useState("");

  useEffect(() => {
    // Dùng history endpoint: trả về booking Completed/Cancelled/Rejected/Expired
    // pageSize=200 để tránh bị mất booking cũ
    bookingApi.getDriverHistory(1, 200)
      .then((data) => {
        const list = Array.isArray(data) ? data : data?.items || [];
        setBookings(list);
      })
      .catch(() => setBookings([]))
      .finally(() => setLoading(false));
  }, []);

  // Chỉ lọc Completed (bỏ Cancelled/Rejected)
  const completedBookings = bookings.filter(
    (b) => b.status === "Completed" || b.status === "completed"
  );

  // Với cùng 1 trạm → chỉ hiển thị booking mới nhất (endTime lớn nhất)
  const latestPerStation = Object.values(
    completedBookings.reduce((acc, b) => {
      const key = b.stationId || b.stationName || b.id;
      if (!acc[key] || new Date(b.endTime) > new Date(acc[key].endTime)) {
        acc[key] = b;
      }
      return acc;
    }, {})
  ).sort((a, b) => new Date(b.endTime) - new Date(a.endTime));

  const handleOpenReview = (bookingId) => {
    setReviewForm({ bookingId, rating: 5, comment: "", isAnonymous: false });
    setErrorMsg("");
    setSuccessMsg("");
  };

  const handleSubmitReview = async () => {
    if (!reviewForm) return;
    setSubmitting(true);
    setErrorMsg("");
    try {
      await reviewApi.create({
        bookingId: reviewForm.bookingId,
        rating: reviewForm.rating,
        comment: reviewForm.comment || undefined,
        isAnonymous: reviewForm.isAnonymous || false,
      });
      setSuccessMsg("Đánh giá thành công! Cảm ơn bạn ");
      setReviewForm(null);
      // Reload bookings sau khi đánh giá — dùng getDriverHistory (pageSize lớn để lấy đủ)
      const refreshData = await bookingApi.getDriverHistory(1, 200);
      setBookings(refreshData?.items ?? (Array.isArray(refreshData) ? refreshData : []));
    } catch (err) {
      const msg = err?.message || "Gửi đánh giá thất bại. Có thể bạn đã đánh giá booking này.";
      setErrorMsg(msg);
    } finally {
      setSubmitting(false);
    }
  };

  const toVN = (t) => {
    const d = new Date(String(t).replace("Z", ""));
    return d.toLocaleString("vi-VN", { hour: "2-digit", minute: "2-digit", day: "2-digit", month: "2-digit", year: "numeric", hour12: false });
  };

  if (loading) {
    return (
      <div style={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: "60vh" }}>
        <div style={{ width: 40, height: 40, border: "4px solid #e5e7eb", borderTopColor: "#f97316", borderRadius: "50%", animation: "spin 1s linear infinite" }} />
        <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
      </div>
    );
  }

  return (
    <div style={{ maxWidth: 700, margin: "100px auto 40px", padding: "0 16px" }}>
      <h1 style={{ fontSize: 24, fontWeight: 800, color: "#1e293b", marginBottom: 6 }}> Đánh giá trạm sạc</h1>
      <p style={{ fontSize: 14, color: "#64748b", marginBottom: 24 }}>
        Đánh giá các lần sạc đã hoàn thành để giúp cải thiện dịch vụ.
      </p>

      {successMsg && (
        <div style={{ background: "#f0fdf4", color: "#16a34a", padding: "12px 16px", borderRadius: 12, marginBottom: 16, fontSize: 14, border: "1px solid #bbf7d0" }}>
           {successMsg}
        </div>
      )}

      {latestPerStation.length === 0 ? (
        <div style={{ textAlign: "center", padding: "60px 20px", background: "#f8fafc", borderRadius: 16 }}>
          <div style={{ fontSize: 48, marginBottom: 12 }}></div>
          <p style={{ fontSize: 16, color: "#64748b", fontWeight: 600 }}>Chưa có booking nào hoàn thành</p>
          <p style={{ fontSize: 13, color: "#94a3b8", marginTop: 4 }}>Hoàn thành phiên sạc để có thể đánh giá.</p>
          <button
            onClick={() => navigate("/driver/map")}
            style={{ marginTop: 20, padding: "10px 24px", borderRadius: 10, border: "none", background: "linear-gradient(135deg, #f97316, #ea580c)", color: "#fff", fontWeight: 700, cursor: "pointer" }}
          >
            Tìm trạm sạc
          </button>
        </div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          {latestPerStation.map((b) => (
            <div
              key={b.id}
              style={{
                background: "#fff",
                borderRadius: 16,
                border: "1px solid #e5e7eb",
                padding: 20,
                boxShadow: "0 1px 3px rgba(0,0,0,0.04)",
              }}
            >
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 10 }}>
                <div>
                  <div style={{ fontSize: 15, fontWeight: 700, color: "#1e293b" }}>
                    {b.stationName || "Trạm sạc"}
                  </div>
                  <div style={{ fontSize: 12, color: "#94a3b8", marginTop: 2 }}>
                    Cổng sạc: {b.slotName} · {toVN(b.startTime)} → {toVN(b.endTime)}
                  </div>
                </div>
                <span style={{
                  fontSize: 11, fontWeight: 600, padding: "3px 10px", borderRadius: 20,
                  background: "#f0fdf4", color: "#16a34a",
                }}>
                  Hoàn thành
                </span>
              </div>

              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", fontSize: 13, color: "#64748b", marginBottom: 10 }}>
                <span>Tổng: <strong style={{ color: "#f97316" }}>{b.totalAmount?.toLocaleString("vi-VN")}đ</strong></span>
                <span>Thời lượng: {Math.round(b.durationHours * 60)} phút</span>
              </div>

              {/* Review form or button */}
              {reviewForm?.bookingId === b.id ? (
                <div style={{ background: "#fffbeb", borderRadius: 12, padding: 16, border: "1px solid #fde68a" }}>
                  <div style={{ fontSize: 13, fontWeight: 700, color: "#92400e", marginBottom: 10 }}>Đánh giá của bạn</div>

                  {/* Stars */}
                  <div style={{ display: "flex", gap: 6, marginBottom: 12 }}>
                    {STARS.map((star) => (
                      <button
                        key={star}
                        onClick={() => setReviewForm({ ...reviewForm, rating: star })}
                        style={{
                          background: "none", border: "none", cursor: "pointer", fontSize: 28, padding: 0,
                          color: star <= reviewForm.rating ? "#f59e0b" : "#d1d5db",
                          transition: "transform 0.15s",
                          transform: star <= reviewForm.rating ? "scale(1.15)" : "scale(1)",
                        }}
                      >
                        ★
                      </button>
                    ))}
                    <span style={{ fontSize: 13, color: "#92400e", alignSelf: "center", marginLeft: 8 }}>
                      {reviewForm.rating}/5
                    </span>
                  </div>

                  {/* Comment */}
                  <textarea
                    placeholder="Nhận xét (tùy chọn)..."
                    value={reviewForm.comment}
                    onChange={(e) => setReviewForm({ ...reviewForm, comment: e.target.value })}
                    rows={3}
                    style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1.5px solid #e5e7eb", fontSize: 13, resize: "vertical", outline: "none", boxSizing: "border-box" }}
                  />

                  {/* Anonymous checkbox */}
                  <label style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 10, cursor: "pointer", fontSize: 13, color: "#374151" }}>
                    <input
                      type="checkbox"
                      checked={reviewForm.isAnonymous || false}
                      onChange={(e) => setReviewForm({ ...reviewForm, isAnonymous: e.target.checked })}
                      style={{ width: 16, height: 16, accentColor: "#f97316", cursor: "pointer" }}
                    />
                    <span>Đánh giá <strong>ẩn danh</strong> (giấu tên của bạn)</span>
                  </label>

                  {errorMsg && (
                    <div style={{ color: "#dc2626", fontSize: 12, marginTop: 8 }}>️ {errorMsg}</div>
                  )}

                  <div style={{ display: "flex", gap: 10, marginTop: 12 }}>
                    <button
                      onClick={handleSubmitReview}
                      disabled={submitting}
                      style={{
                        flex: 1, padding: "10px 0", borderRadius: 10, border: "none",
                        background: submitting ? "#d1d5db" : "linear-gradient(135deg, #f97316, #ea580c)",
                        color: "#fff", fontWeight: 700, fontSize: 13, cursor: submitting ? "not-allowed" : "pointer",
                      }}
                    >
                      {submitting ? "Đang gửi..." : "Gửi đánh giá "}
                    </button>
                    <button
                      onClick={() => { setReviewForm(null); setErrorMsg(""); }}
                      style={{
                        padding: "10px 20px", borderRadius: 10, border: "1.5px solid #e5e7eb",
                        background: "#fff", color: "#64748b", fontWeight: 600, fontSize: 13, cursor: "pointer",
                      }}
                    >
                      Hủy
                    </button>
                  </div>
                </div>
              ) : (
                <button
                  onClick={() => handleOpenReview(b.id)}
                  style={{
                    width: "100%", padding: "10px 0", borderRadius: 10, border: "2px solid #f97316",
                    background: "#fff7ed", color: "#ea580c", fontWeight: 700, fontSize: 13,
                    cursor: "pointer", transition: "all 0.2s",
                  }}
                  onMouseEnter={(e) => { e.target.style.background = "#f97316"; e.target.style.color = "#fff"; }}
                  onMouseLeave={(e) => { e.target.style.background = "#fff7ed"; e.target.style.color = "#ea580c"; }}
                >
                   Viết đánh giá
                </button>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
