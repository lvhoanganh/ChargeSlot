import { Link } from "react-router-dom";
import ChargeSlotLogo from "@/components/ChargeSlotLogo";

export default function OwnerFooter() {
  const currentYear = new Date().getFullYear();

  return (
    <footer className="cs-footer cs-footer--owner">
      <div className="cs-footer__container">
        <div className="cs-footer__grid">
          {/* Brand column */}
          <div className="cs-footer__brand-col">
            <div className="cs-footer__brand">
              <ChargeSlotLogo size={30} showText suffix="Owner" dark />
            </div>
            <p className="cs-footer__desc">
              Quản lý trạm sạc, theo dõi lịch đặt, phiên sạc và dịch vụ khác.
            </p>
          </div>

          {/* Owner Features */}
          <div className="cs-footer__links-col">
            <h3 className="cs-footer__heading">Quản lý</h3>
            <ul className="cs-footer__links">
              <li><Link to="/stations" className="cs-footer__link">Trạm sạc</Link></li>
              <li><Link to="/stations/add" className="cs-footer__link">Tạo trạm mới</Link></li>
              <li><Link to="/owner/booking-requests" className="cs-footer__link">Lịch đặt</Link></li>
              <li><Link to="/owner/active-sessions" className="cs-footer__link">Phiên sạc</Link></li>
              <li><Link to="/owner/extra-services" className="cs-footer__link">Dịch vụ thêm</Link></li>
            </ul>
          </div>

          {/* Account & More */}
          <div className="cs-footer__links-col">
            <h3 className="cs-footer__heading">Tài khoản</h3>
            <ul className="cs-footer__links">
              <li><Link to="/owner/owner-profile" className="cs-footer__link">Hồ sơ cá nhân</Link></li>
              <li><Link to="/owner/wallet" className="cs-footer__link">Ví tiền</Link></li>
              <li><Link to="/owner/reviews" className="cs-footer__link">Đánh giá</Link></li>
              <li><Link to="/owner/chat-list" className="cs-footer__link">Nhắn tin</Link></li>
            </ul>
          </div>
        </div>

        <div className="cs-footer__divider" />
      </div>

      <style>{`
        .cs-footer {
          color: #94a3b8;
          padding: 48px 0 0;
        }
        .cs-footer--owner {
          background: linear-gradient(180deg, #0f172a 0%, #1e293b 100%);
        }
        .cs-footer__container {
          max-width: 1400px;
          width: 90%;
          margin: 0 auto;
        }
        .cs-footer__grid {
          display: grid;
          grid-template-columns: 1.5fr 1fr 1fr;
          gap: 40px;
        }
        @media (max-width: 768px) {
          .cs-footer__grid { grid-template-columns: 1fr; gap: 28px; }
        }
        .cs-footer__brand {
          display: flex;
          align-items: center;
          gap: 8px;
          margin-bottom: 14px;
        }
        .cs-footer__brand-icon { font-size: 24px; }
        .cs-footer__brand-text {
          font-size: 20px;
          font-weight: 800;
          letter-spacing: -0.5px;
          background: linear-gradient(135deg, #f97316, #fb923c);
          -webkit-background-clip: text;
          -webkit-text-fill-color: transparent;
          background-clip: text;
        }
        .cs-footer__desc {
          font-size: 14px;
          line-height: 1.6;
          color: #64748b;
          max-width: 300px;
        }
        .cs-footer__heading {
          font-size: 14px;
          font-weight: 700;
          color: #e2e8f0;
          text-transform: uppercase;
          letter-spacing: 0.08em;
          margin-bottom: 16px;
        }
        .cs-footer__links {
          list-style: none;
          padding: 0;
          margin: 0;
          display: flex;
          flex-direction: column;
          gap: 10px;
        }
        .cs-footer__link {
          font-size: 14px;
          color: #64748b;
          text-decoration: none;
          transition: all 0.2s;
          display: inline-flex;
          align-items: center;
          gap: 6px;
        }
        .cs-footer__link:hover {
          color: #f97316;
          transform: translateX(3px);
        }
        .cs-footer__divider {
          height: 1px;
          background: linear-gradient(90deg, transparent, rgba(249, 115, 22, 0.15), transparent);
          margin: 36px 0 0;
        }
        .cs-footer__bottom {
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 20px 0;
        }
        .cs-footer__copyright, .cs-footer__tagline {
          font-size: 13px;
          color: #475569;
        }
        @media (max-width: 480px) {
          .cs-footer__bottom { flex-direction: column; gap: 8px; text-align: center; }
        }
      `}</style>
    </footer>
  );
}
