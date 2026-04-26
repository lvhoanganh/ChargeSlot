import { useNavigate } from "react-router-dom";
import ChargeSlotLogo from "@/components/ChargeSlotLogo";

export default function NotFound() {
  const navigate = useNavigate();

  return (
    <div className="cs-404">
      <div className="cs-404__content">
        <div className="cs-404__icon-wrap">
          <ChargeSlotLogo size={56} />
        </div>

        <div className="cs-404__number">
          <span className="cs-404__4">4</span>
          <span className="cs-404__0">
            <svg width="80" height="80" viewBox="0 0 80 80" fill="none">
              <circle cx="40" cy="40" r="36" stroke="url(#g404)" strokeWidth="6" strokeLinecap="round" strokeDasharray="8 12" />
              <defs>
                <linearGradient id="g404" x1="0" y1="0" x2="80" y2="80">
                  <stop offset="0%" stopColor="#f97316" />
                  <stop offset="100%" stopColor="#ea580c" />
                </linearGradient>
              </defs>
            </svg>
          </span>
          <span className="cs-404__4">4</span>
        </div>

        <h1 className="cs-404__title">Trang không tồn tại</h1>
        <p className="cs-404__desc">
          Xin lỗi, trang bạn đang tìm kiếm không tồn tại hoặc đã được di chuyển. 
          Hãy quay lại trang chủ hoặc sử dụng menu điều hướng.
        </p>

        <div className="cs-404__actions">
          <button className="cs-404__btn cs-404__btn--primary" onClick={() => navigate("/")}>
             Về trang chủ
          </button>
          <button className="cs-404__btn cs-404__btn--secondary" onClick={() => navigate(-1)}>
            ← Quay lại
          </button>
        </div>
      </div>

      <style>{`
        .cs-404 {
          min-height: 100vh;
          display: flex;
          align-items: center;
          justify-content: center;
          padding: 40px 24px;
          background: linear-gradient(180deg, #fefefe 0%, #f8fafc 50%, #fff7ed 100%);
        }
        .cs-404__content {
          text-align: center;
          max-width: 480px;
          animation: cs-fadeInUp 0.6s ease-out;
        }
        .cs-404__icon-wrap {
          margin-bottom: 24px;
          animation: cs-float 3s ease-in-out infinite;
        }
        .cs-404__number {
          display: flex;
          align-items: center;
          justify-content: center;
          gap: 8px;
          margin-bottom: 24px;
        }
        .cs-404__4 {
          font-size: 80px;
          font-weight: 900;
          background: linear-gradient(135deg, #f97316, #ea580c);
          -webkit-background-clip: text;
          -webkit-text-fill-color: transparent;
          background-clip: text;
          line-height: 1;
        }
        .cs-404__0 {
          display: flex;
          align-items: center;
          animation: cs-spin-slow 12s linear infinite;
        }
        .cs-404__title {
          font-size: 24px;
          font-weight: 800;
          color: #1e293b;
          margin-bottom: 12px;
        }
        .cs-404__desc {
          font-size: 15px;
          color: #64748b;
          line-height: 1.7;
          margin-bottom: 32px;
        }
        .cs-404__actions {
          display: flex;
          align-items: center;
          justify-content: center;
          gap: 12px;
        }
        .cs-404__btn {
          padding: 12px 28px;
          border-radius: 12px;
          font-size: 14px;
          font-weight: 600;
          cursor: pointer;
          transition: all 0.3s cubic-bezier(0.4,0,0.2,1);
          border: none;
          display: inline-flex;
          align-items: center;
          gap: 6px;
        }
        .cs-404__btn--primary {
          background: linear-gradient(135deg, #f97316, #ea580c);
          color: white;
          box-shadow: 0 6px 24px rgba(249,115,22,0.3);
        }
        .cs-404__btn--primary:hover {
          transform: translateY(-2px);
          box-shadow: 0 10px 32px rgba(249,115,22,0.4);
        }
        .cs-404__btn--secondary {
          background: white;
          color: #374151;
          border: 1.5px solid #e5e7eb;
        }
        .cs-404__btn--secondary:hover {
          background: #f9fafb;
          border-color: #d1d5db;
          transform: translateY(-1px);
        }
        @media (max-width: 480px) {
          .cs-404__4 { font-size: 56px; }
          .cs-404__0 svg { width: 56px; height: 56px; }
          .cs-404__actions { flex-direction: column; }
        }
      `}</style>
    </div>
  );
}
