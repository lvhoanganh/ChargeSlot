import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ownerContractApi } from "@/services/api";
import { showToast } from "@/components/Toast";
import { formatDateVN } from "@/utils/dateVN";

// ──────────────────────────────────
// Canvas chữ ký — standalone hook
// ──────────────────────────────────
function useSignatureCanvas(canvasRef) {
  const drawing = useRef(false);
  const lastPos = useRef(null);
  // Tỷ lệ HiDPI — cần lưu để dùng trong getPos
  const dpr = useRef(window.devicePixelRatio || 1);

  // Đồng bộ buffer canvas với CSS size + support retina
  function resizeCanvas() {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ratio = window.devicePixelRatio || 1;
    dpr.current = ratio;
    const cssW = canvas.offsetWidth;
    const cssH = canvas.offsetHeight;
    if (canvas.width !== Math.round(cssW * ratio) || canvas.height !== Math.round(cssH * ratio)) {
      canvas.width = Math.round(cssW * ratio);
      canvas.height = Math.round(cssH * ratio);
      const ctx = canvas.getContext("2d");
      ctx.scale(ratio, ratio);
    }
  }

  /**
   * Lấy toạ độ chuẩn (CSS px) so với canvas, không nhân thêm DPR
   * vì ctx đã được scale(dpr) rồi.
   */
  function getPos(e, canvas) {
    const rect = canvas.getBoundingClientRect();
    if (e.touches) {
      return {
        x: e.touches[0].clientX - rect.left,
        y: e.touches[0].clientY - rect.top,
      };
    }
    return {
      x: e.clientX - rect.left,
      y: e.clientY - rect.top,
    };
  }

  function start(e) {
    e.preventDefault();
    resizeCanvas(); // đảm bảo đúng kích thước trước khi vẽ
    drawing.current = true;
    lastPos.current = getPos(e, canvasRef.current);
  }

  function move(e) {
    e.preventDefault();
    if (!drawing.current) return;
    const canvas = canvasRef.current;
    const ctx = canvas.getContext("2d");
    const pos = getPos(e, canvas);
    ctx.beginPath();
    ctx.moveTo(lastPos.current.x, lastPos.current.y);
    ctx.lineTo(pos.x, pos.y);
    ctx.strokeStyle = "#1e293b";
    ctx.lineWidth = 2;
    ctx.lineCap = "round";
    ctx.lineJoin = "round";
    ctx.stroke();
    lastPos.current = pos;
  }

  function end(e) {
    e.preventDefault();
    drawing.current = false;
    lastPos.current = null;
  }

  function clear() {
    const canvas = canvasRef.current;
    const ctx = canvas.getContext("2d");
    // Reset và re-apply scale
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.scale(dpr.current, dpr.current);
  }

  function isEmpty() {
    const canvas = canvasRef.current;
    const ctx = canvas.getContext("2d");
    const data = ctx.getImageData(0, 0, canvas.width, canvas.height).data;
    return !data.some((v) => v !== 0);
  }

  return { start, move, end, clear, isEmpty, resizeCanvas };
}

// ──────────────────────────────────
// Badge trạng thái hợp đồng
// ──────────────────────────────────
function ContractStatusBadge({ status }) {
  const map = {
    Signed:     { label: "Đã ký",      bg: "#f0fdf4", color: "#16a34a", dot: "#16a34a" },
    Pending:    { label: "Chờ ký",     bg: "#fff7ed", color: "#ea580c", dot: "#f97316" },
    Terminated: { label: "Đã chấm dứt", bg: "#fef2f2", color: "#dc2626", dot: "#dc2626" },
    Expired:    { label: "Hết hạn",    bg: "#f3f4f6", color: "#64748b", dot: "#9ca3af" },
  };
  const cfg = map[status];
  if (!cfg) return null;
  return (
    <span style={{ display: "inline-flex", alignItems: "center", gap: 6, padding: "4px 14px", borderRadius: 50, fontSize: 13, fontWeight: 700, background: cfg.bg, color: cfg.color }}>
      <span style={{ width: 7, height: 7, borderRadius: "50%", background: cfg.dot, display: "inline-block" }} />
      {cfg.label}
    </span>
  );
}

// ──────────────────────────────────
// Modal xác nhận chấm dứt hợp đồng
// ──────────────────────────────────
function TerminateModal({ onClose, onConfirm, loading }) {
  const [reason, setReason] = useState("");
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl max-w-md w-full p-6" onClick={e => e.stopPropagation()}>
        <div className="flex items-center gap-3 mb-4">
          <span className="text-red-500">
            <svg width="32" height="32" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
          </span>
          <h3 className="text-lg font-bold text-slate-900">Yêu cầu chấm dứt hợp đồng</h3>
        </div>
        <div className="bg-red-50 border border-red-200 rounded-xl p-3 mb-4 text-sm text-red-800">
          <strong>Lưu ý (Điều 6.3):</strong> Chỉ có thể chấm dứt khi <strong>không có đặt chỗ nào đang hoạt động</strong>.
          Toàn bộ trạm sạc sẽ bị đình chỉ ngay lập tức sau khi xác nhận.
        </div>
        <label className="block text-sm font-semibold text-slate-700 mb-1.5">
          Lý do chấm dứt hợp đồng <span className="text-red-500">*</span>
        </label>
        <textarea
          value={reason}
          onChange={e => setReason(e.target.value)}
          rows={3}
          placeholder="Nhập lý do yêu cầu chấm dứt hợp đồng..."
          className="w-full px-4 py-2.5 rounded-xl border border-slate-200 bg-slate-50 focus:bg-white focus:ring-2 focus:ring-red-400 focus:border-red-400 outline-none transition resize-none text-sm mb-4"
        />
        <div className="flex gap-3">
          <button
            onClick={onClose}
            className="flex-1 py-2.5 rounded-xl border border-slate-200 text-slate-600 text-sm font-semibold hover:bg-slate-100 transition"
          >
            Hủy bỏ
          </button>
          <button
            disabled={!reason.trim() || loading}
            onClick={() => onConfirm(reason.trim())}
            className="flex-1 py-2.5 rounded-xl bg-red-600 hover:bg-red-700 text-white text-sm font-bold transition disabled:opacity-60 disabled:cursor-not-allowed flex items-center justify-center gap-2"
          >
            {loading ? (
              <><div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" /> Đang xử lý...</>
            ) : "Xác nhận chấm dứt"}
          </button>
        </div>
      </div>
    </div>
  );
}

// ──────────────────────────────────
// Main page
// ──────────────────────────────────
export default function OwnerContractPage() {
  const navigate = useNavigate();
  const canvasRef = useRef(null);
  const sig = useSignatureCanvas(canvasRef);

  // Khi component mount, resize canvas đúng kích thước thực
  useEffect(() => {
    sig.resizeCanvas();
    window.addEventListener("resize", sig.resizeCanvas);
    return () => window.removeEventListener("resize", sig.resizeCanvas);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const [loading, setLoading] = useState(true);
  const [contract, setContract] = useState(null);
  const [error, setError] = useState(null);
  const [signing, setSigning] = useState(false);
  const [signed, setSigned] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [showTerminateModal, setShowTerminateModal] = useState(false);
  const [terminating, setTerminating] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const data = await ownerContractApi.get();
        setContract(data);
        if (data?.status === "Signed") setSigned(true);
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  // ── Ký hợp đồng ──
  async function handleSign() {
    if (!canvasRef.current) return;
    if (sig.isEmpty()) {
      showToast.error("Vui lòng ký tên trước khi xác nhận.");
      return;
    }
    const base64 = canvasRef.current.toDataURL("image/png");
    setSigning(true);
    try {
      const updated = await ownerContractApi.sign(base64);
      setSigned(true);
      setContract(updated);
      showToast.success("✅ Hợp đồng đã được ký thành công! Trạm sạc chính thức hoạt động.");
    } catch (err) {
      showToast.error(err.message || "Có lỗi xảy ra khi ký hợp đồng.");
    } finally {
      setSigning(false);
    }
  }

  // ── Tải PDF ──
  async function handleDownload() {
    setDownloading(true);
    try {
      const blob = await ownerContractApi.download();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `HopDong_ChargeSlot_${contract?.contractNumber || ""}.pdf`;
      a.click();
      URL.revokeObjectURL(url);
      showToast.success("Tải hợp đồng PDF thành công!");
    } catch (err) {
      showToast.error(err.message || "Không thể tải hợp đồng. Vui lòng thử lại.");
    } finally {
      setDownloading(false);
    }
  }

  // ── Chấm dứt hợp đồng (Điều 6.3) ──
  async function handleTerminate(reason) {
    setTerminating(true);
    try {
      await ownerContractApi.terminate(reason);
      setShowTerminateModal(false);
      setContract(prev => prev ? { ...prev, status: "Terminated" } : prev);
      showToast.success("Đã gửi yêu cầu chấm dứt hợp đồng. Toàn bộ trạm sạc đã bị đình chỉ.");
    } catch (err) {
      showToast.error(err.message || "Không thể chấm dứt hợp đồng.");
    } finally {
      setTerminating(false);
    }
  }

  // ── Loading ──
  if (loading) {
    return (
      <div className="min-h-[60vh] flex items-center justify-center">
        <div className="text-center">
          <div className="w-10 h-10 border-4 border-orange-200 border-t-orange-500 rounded-full animate-spin mx-auto mb-4" />
          <p className="text-slate-500">Đang tải hợp đồng...</p>
        </div>
      </div>
    );
  }

  // ── Chưa có hợp đồng ──
  if (error || !contract) {
    return (
      <div className="min-h-screen bg-slate-50 pt-24 pb-16 px-4">
        <div className="max-w-2xl mx-auto text-center py-20">
          <div className="text-slate-400 mb-6 flex justify-center">
            <svg width="68" height="68" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" /></svg>
          </div>
          <h1 className="text-2xl font-bold text-slate-800 mb-3">Chưa có hợp đồng</h1>
          <p className="text-slate-500 leading-relaxed max-w-md mx-auto">
            Hợp đồng hợp tác sẽ được tạo tự động sau khi Admin xét duyệt và phê duyệt hồ sơ KYC của bạn.
          </p>
          <button
            onClick={() => navigate("/owner/kyc")}
            className="mt-8 inline-flex items-center gap-2 px-6 py-3 bg-orange-500 hover:bg-orange-600 text-white font-semibold rounded-xl transition shadow-lg shadow-orange-500/20"
          >
            <svg width="20" height="20" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" /></svg>
            Kiểm tra hồ sơ KYC
          </button>
        </div>
      </div>
    );
  }

  const isTerminated = contract.status === "Terminated";
  const isExpired = contract.status === "Expired";

  return (
    <div className="min-h-screen bg-slate-50 pt-24 pb-16 px-4 sm:px-6">
      <div className="max-w-5xl mx-auto">

        {/* Header */}
        <div className="flex items-center gap-3 mb-6">
          <button
            onClick={() => navigate(-1)}
            className="p-2 rounded-xl hover:bg-slate-200 transition text-slate-500"
          >
            ←
          </button>
          <div className="flex-1">
            <div className="flex items-center gap-3 flex-wrap">
              <h1 className="text-2xl font-bold text-slate-900">Hợp đồng hợp tác</h1>
              <ContractStatusBadge status={contract.status} />
            </div>
            <p className="text-sm text-slate-500 mt-0.5">
              {contract.contractNumber && (
                <span className="font-semibold text-slate-600 mr-2">#{contract.contractNumber}</span>
              )}
              {contract.signedAt && !isExpired && !isTerminated
                ? `Ký ngày: ${formatDateVN(contract.signedAt)}`
                : isTerminated
                ? "Hợp đồng đã bị chấm dứt"
                : isExpired
                ? "Hợp đồng đã hết hạn"
                : "Vui lòng đọc kỹ và ký xác nhận hợp đồng bên dưới."}
            </p>
          </div>

          {/* Nút Tải PDF — luôn hiển thị nếu có hợp đồng */}
          <button
            onClick={handleDownload}
            disabled={downloading}
            className="hidden sm:inline-flex items-center gap-2 px-4 py-2 rounded-xl border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 text-sm font-semibold transition shadow-sm disabled:opacity-60"
          >
            {downloading ? (
              <><div className="w-4 h-4 border-2 border-slate-300 border-t-slate-600 rounded-full animate-spin" /> Đang tải...</>
            ) : (
              <><svg width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" /></svg> Tải PDF</>
            )}
          </button>
        </div>

        {/* Signed success banner */}
        {signed && contract.status === "Signed" && (
          <div className="mb-6 p-4 bg-green-50 border border-green-200 rounded-2xl flex gap-3 items-start text-green-800">
            <div className="text-green-600 flex-shrink-0 mt-0.5">
              <svg width="28" height="28" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
            </div>
            <div>
              <strong className="block text-base mb-0.5">Hợp đồng đã được ký thành công!</strong>
              <p className="text-sm text-green-700">
                Trạm sạc của bạn chính thức được phép hoạt động và nhận đặt lịch từ tài xế.
                {contract.signedAt && ` Ký lúc: ${formatDateVN(contract.signedAt)}.`}
              </p>
            </div>
          </div>
        )}

        {/* Terminated / Expired banner */}
        {(isTerminated || isExpired) && (
          <div className="mb-6 p-4 bg-red-50 border border-red-200 rounded-2xl flex gap-3 items-start text-red-800">
            <div className="text-red-500 flex-shrink-0 mt-0.5">
              {isExpired ? (
                <svg width="28" height="28" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              ) : (
                <svg width="28" height="28" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" /></svg>
              )}
            </div>
            <div>
              <strong className="block text-base mb-0.5">{isExpired ? "Hợp đồng đã hết hạn" : "Hợp đồng đã chấm dứt"}</strong>
              <p className="text-sm text-red-700">
                Toàn bộ trạm sạc đã bị đình chỉ hoạt động. Bạn vẫn có thể rút số dư ví trên hệ thống.
                Để tiếp tục kinh doanh, vui lòng liên hệ Admin để yêu cầu tái ký hợp đồng mới.
              </p>
            </div>
          </div>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">

          {/* ───── HTML Contract viewer (2/3 width) ───── */}
          <div className="lg:col-span-2">
            <div className="bg-white rounded-2xl shadow-sm ring-1 ring-slate-200 overflow-hidden">
              <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 bg-slate-50/70">
                <div className="flex items-center gap-2 text-slate-600 text-sm font-semibold">
                  <svg width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                  </svg>
                  Nội dung hợp đồng
                </div>
                <ContractStatusBadge status={contract.status} />
              </div>

              {/* A4-style contract viewer — BE trả về trường contractHtml */}
              <div className="p-6 sm:p-10">
                <div
                  className="contract-html-content"
                  dangerouslySetInnerHTML={{ __html: contract.contractHtml }}
                />
              </div>
            </div>
          </div>

          {/* ───── Side panel (1/3 width) ───── */}
          <div className="lg:col-span-1">
            <div className="sticky top-24 space-y-4">

              {/* Info panel */}
              <div className="bg-white rounded-2xl ring-1 ring-slate-200 p-5 shadow-sm">
                <h3 className="text-sm font-bold text-slate-700 mb-3 flex items-center gap-2">
                  <svg width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                  Thông tin hợp đồng
                </h3>
                <div className="space-y-2 text-sm">
                  {contract.contractNumber && (
                    <div className="flex justify-between">
                      <span className="text-slate-500">Số HĐ</span>
                      <span className="font-semibold text-slate-700">{contract.contractNumber}</span>
                    </div>
                  )}
                  <div className="flex justify-between items-center">
                    <span className="text-slate-500">Trạng thái</span>
                    <ContractStatusBadge status={contract.status} />
                  </div>
                  {contract.createdAt && (
                    <div className="flex justify-between">
                      <span className="text-slate-500">Ngày tạo</span>
                      <span className="font-semibold text-slate-700">{formatDateVN(contract.createdAt)}</span>
                    </div>
                  )}
                  {contract.signedAt && (
                    <div className="flex justify-between">
                      <span className="text-slate-500">Ngày ký</span>
                      <span className="font-semibold text-green-700">{formatDateVN(contract.signedAt)}</span>
                    </div>
                  )}
                  {contract.expiresAt && (
                    <div className="flex justify-between">
                      <span className="text-slate-500">Hết hạn</span>
                      <span className="font-semibold text-slate-700">{formatDateVN(contract.expiresAt)}</span>
                    </div>
                  )}
                  {contract.contractDurationMonths && (
                    <div className="flex justify-between">
                      <span className="text-slate-500">Thời hạn</span>
                      <span className="font-semibold text-slate-700">{contract.contractDurationMonths} tháng</span>
                    </div>
                  )}
                </div>

                {/* Tải PDF — mobile + desktop */}
                <button
                  onClick={handleDownload}
                  disabled={downloading}
                  className="mt-4 w-full flex items-center justify-center gap-2 py-2.5 rounded-xl border border-slate-200 text-slate-700 text-sm font-semibold hover:bg-slate-50 transition disabled:opacity-60"
                >
                  {downloading ? (
                    <><div className="w-4 h-4 border-2 border-slate-300 border-t-slate-600 rounded-full animate-spin" /> Đang tải...</>
                  ) : (
                    <><svg width="15" height="15" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" /></svg> Tải PDF hợp đồng</>
                  )}
                </button>
              </div>

              {/* Signature canvas — chỉ hiện khi Pending (chưa ký, chưa chấm dứt) */}
              {!signed && contract.status === "Pending" && (
                <div className="bg-white rounded-2xl ring-1 ring-slate-200 p-5 shadow-sm">
                  <h3 className="text-sm font-bold text-slate-700 mb-1 flex items-center gap-2">
                    <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" /></svg>
                    Chữ ký của bạn
                  </h3>
                  <p className="text-xs text-slate-400 mb-3">Ký tay bằng chuột hoặc ngón tay vào ô bên dưới</p>

                  <div className="relative rounded-xl overflow-hidden border-2 border-dashed border-slate-200 bg-slate-50 group hover:border-orange-300 transition-colors">
                    <canvas
                      ref={canvasRef}
                      className="w-full touch-none cursor-crosshair block"
                      style={{ display: "block", height: 160 }}
                      onMouseDown={sig.start}
                      onMouseMove={sig.move}
                      onMouseUp={sig.end}
                      onMouseLeave={sig.end}
                      onTouchStart={sig.start}
                      onTouchMove={sig.move}
                      onTouchEnd={sig.end}
                    />
                    <div className="pointer-events-none absolute inset-0 flex items-center justify-center opacity-20 select-none">
                      <p className="text-slate-400 text-sm font-medium">Vẽ chữ ký tại đây</p>
                    </div>
                  </div>

                  <div className="flex gap-2 mt-3">
                    <button
                      onClick={sig.clear}
                      className="flex-1 py-2 rounded-xl border border-slate-200 text-slate-600 text-sm font-semibold hover:bg-slate-100 transition"
                    >
                      Xóa
                    </button>
                    <button
                      onClick={handleSign}
                      disabled={signing}
                      className="flex-2 flex-grow-[2] py-2 px-4 rounded-xl bg-gradient-to-r from-orange-500 to-orange-600 hover:from-orange-600 hover:to-orange-700 text-white text-sm font-bold transition shadow-lg shadow-orange-500/20 disabled:opacity-70 disabled:cursor-not-allowed flex items-center justify-center gap-2"
                    >
                      {signing ? (
                        <>
                          <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                          Đang ký...
                        </>
                      ) : (
                        <>
                          <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" /></svg>
                          Xác nhận ký
                        </>
                      )}
                    </button>
                  </div>

                  <p className="text-xs text-slate-400 mt-3 text-center leading-relaxed">
                    Bằng cách ký hợp đồng, bạn đồng ý với toàn bộ điều khoản được nêu trong tài liệu trên.
                  </p>
                </div>
              )}

              {/* Đã ký xong */}
              {contract.status === "Signed" && (
                <div className="bg-green-50 rounded-2xl ring-1 ring-green-200 p-5 text-center">
                  <div className="text-green-500 flex justify-center mb-2">
                    <svg width="44" height="44" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
                  </div>
                  <p className="text-sm font-bold text-green-700">Đã ký thành công</p>
                  <p className="text-xs text-green-600 mt-1">
                    Trạm sạc của bạn đã được kích hoạt đầy đủ.
                  </p>
                </div>
              )}

              {/* Nút chấm dứt hợp đồng — chỉ hiển thị khi Signed (Điều 6.3) */}
              {contract.status === "Signed" && (
                <div className="bg-white rounded-2xl ring-1 ring-red-100 p-5 shadow-sm">
                  <h3 className="text-sm font-bold text-red-700 mb-1">Chấm dứt hợp đồng</h3>
                  <p className="text-xs text-slate-500 mb-3 leading-relaxed">
                    Theo Điều 6.3, bạn có thể yêu cầu chấm dứt hợp đồng khi không còn booking đang hoạt động.
                    Toàn bộ trạm sạc sẽ bị đình chỉ ngay lập tức.
                  </p>
                  <button
                    onClick={() => setShowTerminateModal(true)}
                    className="w-full py-2 rounded-xl border border-red-200 text-red-600 text-sm font-semibold hover:bg-red-50 transition flex items-center justify-center gap-2"
                  >
                    <svg width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" /></svg>
                    Yêu cầu chấm dứt
                  </button>
                </div>
              )}

              {/* Trạng thái Terminated / Expired */}
              {(isTerminated || isExpired) && (
                <div className="bg-red-50 rounded-2xl ring-1 ring-red-200 p-5 text-center">
                  <div className="text-red-500 flex justify-center mb-2">
                    {isExpired ? (
                      <svg width="44" height="44" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
                    ) : (
                      <svg width="44" height="44" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" /></svg>
                    )}
                  </div>
                  <p className="text-sm font-bold text-red-700">{isExpired ? "Hợp đồng đã hết hạn" : "Hợp đồng đã chấm dứt"}</p>
                  <p className="text-xs text-red-600 mt-1">
                    Liên hệ Admin để tiếp tục sử dụng dịch vụ.
                  </p>
                </div>
              )}

            </div>
          </div>
        </div>
      </div>

      {/* Modal chấm dứt */}
      {showTerminateModal && (
        <TerminateModal
          onClose={() => setShowTerminateModal(false)}
          onConfirm={handleTerminate}
          loading={terminating}
        />
      )}

      {/* CSS cho nội dung HTML hợp đồng */}
      <style>{`
        .contract-html-content {
          font-family: 'Times New Roman', Times, Georgia, serif;
          font-size: 14px;
          line-height: 1.8;
          color: #1e293b;
        }
        .contract-html-content h1,
        .contract-html-content h2,
        .contract-html-content h3 {
          font-family: inherit;
          text-align: center;
          font-weight: 700;
          margin: 1.2em 0 0.8em;
          color: #0f172a;
        }
        .contract-html-content h1 { font-size: 18px; text-transform: uppercase; letter-spacing: 1px; }
        .contract-html-content h2 { font-size: 15px; }
        .contract-html-content h3 { font-size: 14px; text-align: left; }
        .contract-html-content p {
          margin: 0.6em 0;
          text-align: justify;
        }
        .contract-html-content table {
          width: 100%;
          border-collapse: collapse;
          margin: 1em 0;
          font-size: 13px;
        }
        .contract-html-content table th,
        .contract-html-content table td {
          border: 1px solid #cbd5e1;
          padding: 8px 12px;
          text-align: left;
        }
        .contract-html-content table th {
          background: #f8fafc;
          font-weight: 700;
        }
        .contract-html-content ul, .contract-html-content ol {
          padding-left: 24px;
          margin: 0.6em 0;
        }
        .contract-html-content li { margin: 0.3em 0; }
        .contract-html-content strong { font-weight: 700; }
        .contract-html-content hr {
          border: none;
          border-top: 1px solid #e2e8f0;
          margin: 1.5em 0;
        }
        .contract-html-content .signature-area {
          display: grid;
          grid-template-columns: 1fr 1fr;
          gap: 32px;
          margin-top: 40px;
          text-align: center;
        }
      `}</style>
    </div>
  );
}
