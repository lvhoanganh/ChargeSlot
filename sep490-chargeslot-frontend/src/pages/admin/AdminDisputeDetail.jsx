import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { instance } from "@/lib/httpRequest";

/* ─── API helpers ─── */
const disputeApiAdmin = {
  getById: async (id) => {
    const { data } = await instance.get(`/dispute/${id}`);
    return data;
  },
  resolve: async (id, body) => {
    const { data } = await instance.post(`/dispute/${id}/resolve`, body);
    return data;
  },
};

const STATUS_MAP = {
  Open: { label: "Mở", cls: "bg-yellow-100 text-yellow-700", icon: "📝" },
  WaitingOwnerEvidence: { label: "Chờ Owner phản hồi", cls: "bg-orange-100 text-orange-700", icon: "⏳" },
  PendingReview: { label: "Sẵn sàng xem xét", cls: "bg-blue-100 text-blue-700", icon: "🔍" },
  ResolvedRefund: { label: "Hoàn tiền Driver", cls: "bg-green-100 text-green-700", icon: "✅" },
  ResolvedPayout: { label: "Thanh toán Owner", cls: "bg-purple-100 text-purple-700", icon: "💰" },
};

function formatDate(dateStr) {
  if (!dateStr) return "—";
  const s = String(dateStr);
  const d = new Date(s.endsWith("Z") ? s : s + "Z");
  return d.toLocaleString("vi-VN");
}

export default function AdminDisputeDetail() {
  const { disputeId } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [showResolveModal, setShowResolveModal] = useState(false);
  const [isDriverWin, setIsDriverWin] = useState(true);
  const [adminNote, setAdminNote] = useState("");

  const { data: dispute, isLoading, error } = useQuery({
    queryKey: ["admin-dispute", disputeId],
    queryFn: () => disputeApiAdmin.getById(Number(disputeId)),
  });

  const resolveMutation = useMutation({
    mutationFn: ({ isDriverWin, adminNote }) =>
      disputeApiAdmin.resolve(Number(disputeId), { isDriverWin, adminNote }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-dispute", disputeId] });
      queryClient.invalidateQueries({ queryKey: ["admin-disputes-pending"] });
      setShowResolveModal(false);
    },
    onError: (err) => {
      const msg = err?.response?.data?.message || err?.message || "Lỗi không xác định";
      alert("Lỗi: " + msg);
    },
  });

  if (isLoading) {
    return (
      <div className="max-w-[95%] mx-auto pt-28 pb-10 text-center">
        <div className="text-lg text-slate-500">⏳ Đang tải chi tiết khiếu nại...</div>
      </div>
    );
  }

  if (error || !dispute) {
    return (
      <div className="max-w-[95%] mx-auto pt-28 pb-10 text-center">
        <div className="text-lg text-red-500">❌ {error?.message || "Không tìm thấy khiếu nại"}</div>
        <button onClick={() => navigate("/admin/disputes")} className="mt-4 px-4 py-2 bg-blue-500 text-white rounded-lg cursor-pointer">
          ← Danh sách khiếu nại
        </button>
      </div>
    );
  }

  const st = STATUS_MAP[dispute.status] || STATUS_MAP.Open;
  const canResolve = dispute.status === "PendingReview" || dispute.status === "WaitingOwnerEvidence";
  const isResolved = dispute.status === "ResolvedRefund" || dispute.status === "ResolvedPayout";

  return (
    <div className="max-w-[900px] w-full mx-auto pt-28 pb-10 px-4">
      {/* Back */}
      <button
        onClick={() => navigate("/admin/disputes")}
        className="mb-4 text-sm text-gray-500 hover:text-gray-700 flex items-center gap-1 cursor-pointer"
      >
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M19 12H5M12 19l-7-7 7-7"/></svg>
        Danh sách khiếu nại
      </button>

      {/* Header */}
      <div className="bg-white border rounded-xl p-6 mb-6">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h1 className="text-xl font-bold">Khiếu nại #{dispute.id}</h1>
            <p className="text-sm text-gray-500 mt-1">Booking #{dispute.bookingId}</p>
          </div>
          <span className={`px-3 py-1.5 rounded-full text-sm font-semibold ${st.cls}`}>
            {st.icon} {st.label}
          </span>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
          <div><span className="text-gray-500">Người khiếu nại:</span> <span className="font-medium">{dispute.createdByName}</span></div>
          <div><span className="text-gray-500">Ngày tạo:</span> <span className="font-medium">{formatDate(dispute.createdAt)}</span></div>
          {dispute.resolvedAt && <div><span className="text-gray-500">Ngày xử lý:</span> <span className="font-medium">{formatDate(dispute.resolvedAt)}</span></div>}
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
        {/* Driver's complaint */}
        <div className="bg-white border rounded-xl p-6">
          <h2 className="font-bold text-base mb-4 flex items-center gap-2">
            <span className="w-8 h-8 bg-red-100 rounded-full flex items-center justify-center text-sm">🚗</span>
            Khiếu nại từ Driver
          </h2>

          <div className="mb-3">
            <span className="text-xs text-gray-500 uppercase tracking-wider">Lý do</span>
            <p className="text-sm font-medium mt-1">{dispute.reason}</p>
          </div>

          <div className="mb-3">
            <span className="text-xs text-gray-500 uppercase tracking-wider">Mô tả</span>
            <p className="text-sm mt-1 bg-gray-50 p-3 rounded-lg leading-relaxed">{dispute.description}</p>
          </div>

          {dispute.evidences?.length > 0 && (
            <div>
              <span className="text-xs text-gray-500 uppercase tracking-wider">Bằng chứng ({dispute.evidences.length})</span>
              <div className="flex flex-wrap gap-2 mt-2">
                {dispute.evidences.map((ev) => (
                  <a key={ev.id} href={ev.fileUrl} target="_blank" rel="noopener noreferrer"
                    className="block w-20 h-20 rounded-lg overflow-hidden border-2 border-gray-200 hover:border-blue-400 transition-colors bg-gray-100">
                    {ev.fileType === "image" ? (
                      <img src={ev.fileUrl} alt="evidence" className="w-full h-full object-cover" />
                    ) : (
                      <div className="flex items-center justify-center h-full text-2xl">
                        {ev.fileType === "video" ? "🎬" : "📄"}
                      </div>
                    )}
                  </a>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Owner's response */}
        <div className="bg-white border rounded-xl p-6">
          <h2 className="font-bold text-base mb-4 flex items-center gap-2">
            <span className="w-8 h-8 bg-orange-100 rounded-full flex items-center justify-center text-sm">🏢</span>
            Phản hồi từ Owner
          </h2>

          {dispute.ownerResponse ? (
            <div className="mb-3">
              <span className="text-xs text-gray-500 uppercase tracking-wider">Nội dung phản hồi</span>
              <p className="text-sm mt-1 bg-gray-50 p-3 rounded-lg leading-relaxed">{dispute.ownerResponse}</p>
            </div>
          ) : (
            <div className="text-center py-8">
              <span className="text-3xl">⏳</span>
              <p className="text-sm text-gray-400 mt-2">Chưa có phản hồi từ Owner</p>
            </div>
          )}
        </div>
      </div>

      {/* Admin resolution (if resolved) */}
      {isResolved && (
        <div className={`border rounded-xl p-6 mb-6 ${dispute.status === "ResolvedRefund" ? "bg-green-50 border-green-200" : "bg-purple-50 border-purple-200"}`}>
          <h2 className="font-bold text-base mb-3">⚖️ Kết quả xử lý</h2>
          <div className={`inline-block px-3 py-1.5 rounded-full text-sm font-semibold mb-3 ${st.cls}`}>
            {dispute.status === "ResolvedRefund" ? "✅ Driver thắng — Hoàn tiền" : "💰 Owner thắng — Thanh toán"}
          </div>
          {dispute.adminNote && (
            <div>
              <span className="text-xs text-gray-500 uppercase tracking-wider">Ghi chú</span>
              <p className="text-sm mt-1">{dispute.adminNote}</p>
            </div>
          )}
        </div>
      )}

      {/* Action button */}
      {canResolve && (
        <div className="flex justify-center">
          <button
            onClick={() => setShowResolveModal(true)}
            className="px-8 py-3 bg-blue-600 hover:bg-blue-700 text-white font-semibold rounded-xl shadow-lg shadow-blue-200 transition-all cursor-pointer text-base"
          >
            ⚖️ Phán quyết khiếu nại
          </button>
        </div>
      )}

      {/* Resolve Modal */}
      {showResolveModal && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
          <div className="bg-white rounded-xl p-6 w-[500px] max-w-[95vw]">
            <h2 className="text-lg font-bold mb-4">⚖️ Phán quyết khiếu nại #{dispute.id}</h2>

            {/* Decision */}
            <div className="mb-4">
              <label className="block text-sm font-medium text-gray-700 mb-3">Kết quả phán quyết *</label>
              <div className="flex flex-col gap-3">
                <label
                  className={`flex items-center gap-3 p-3 rounded-xl border-2 cursor-pointer transition-all ${
                    isDriverWin ? "border-green-500 bg-green-50" : "border-gray-200 hover:border-gray-300"
                  }`}
                >
                  <input type="radio" checked={isDriverWin} onChange={() => setIsDriverWin(true)} className="accent-green-600" />
                  <div>
                    <span className="font-semibold text-sm">✅ Driver thắng — Hoàn tiền</span>
                    <p className="text-xs text-gray-500 mt-0.5">Tiền từ ESCROW sẽ hoàn về ví Driver</p>
                  </div>
                </label>
                <label
                  className={`flex items-center gap-3 p-3 rounded-xl border-2 cursor-pointer transition-all ${
                    !isDriverWin ? "border-purple-500 bg-purple-50" : "border-gray-200 hover:border-gray-300"
                  }`}
                >
                  <input type="radio" checked={!isDriverWin} onChange={() => setIsDriverWin(false)} className="accent-purple-600" />
                  <div>
                    <span className="font-semibold text-sm">💰 Owner thắng — Thanh toán</span>
                    <p className="text-xs text-gray-500 mt-0.5">Tiền từ ESCROW sẽ chuyển cho Owner (trừ phí nền tảng)</p>
                  </div>
                </label>
              </div>
            </div>

            {/* Admin note */}
            <div className="mb-4">
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Ghi chú phán quyết <span className="text-red-500">*</span>
              </label>
              <textarea
                value={adminNote}
                onChange={(e) => setAdminNote(e.target.value)}
                placeholder="Lý do phán quyết, giải thích cho các bên..."
                maxLength={2000}
                className="w-full h-24 rounded-lg border px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-200 resize-none"
              />
            </div>

            <div className="flex justify-end gap-2">
              <button
                onClick={() => { setShowResolveModal(false); setAdminNote(""); }}
                className="px-4 py-2 border rounded-md cursor-pointer"
                disabled={resolveMutation.isPending}
              >
                Hủy
              </button>
              <button
                onClick={() => resolveMutation.mutate({ isDriverWin, adminNote })}
                disabled={resolveMutation.isPending || !adminNote.trim()}
                className={`px-4 py-2 rounded-md text-white cursor-pointer ${
                  isDriverWin ? "bg-green-500 hover:bg-green-600" : "bg-purple-500 hover:bg-purple-600"
                } disabled:opacity-50 disabled:cursor-not-allowed`}
              >
                {resolveMutation.isPending ? "Đang xử lý..." : "Xác nhận phán quyết"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
