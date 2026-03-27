import { useNavigate } from "react-router-dom";
import { useState } from "react";
import ChargeSlotLogo from "@/components/ChargeSlotLogo";

export default function HomePage() {
  const navigate = useNavigate();
  const [expandedFeature, setExpandedFeature] = useState(null);

  const features = [
    {
      icon: "🗺️",
      title: "Tìm trạm nhanh",
      desc: "Tìm kiếm và lọc trạm sạc trên bản đồ tương tác Google Maps.",
      detail: [
        "Mở \"Tìm trạm\" trên thanh điều hướng để xem bản đồ các trạm sạc",
        "Lọc trạm theo đánh giá 1–5 sao, sắp xếp theo đánh giá cao nhất hoặc nhiều đánh giá nhất",
        "Bấm vào marker trạm để xem: tên, địa chỉ, số slot trống, đánh giá, khoảng cách",
        "Nhấn \"Chỉ đường\" để mở Google Maps dẫn đường đến trạm",
        "Nhấn \"Chi tiết\" để xem giá theo khung giờ, giờ hoạt động, dịch vụ bổ sung và đánh giá chi tiết",
      ],
    },
    {
      icon: "📅",
      title: "Đặt lịch dễ dàng",
      desc: "Chọn slot sạc, khung giờ, dịch vụ phụ và xác nhận booking ngay.",
      detail: [
        "Từ trang chi tiết trạm, nhấn \"Đặt lịch sạc\" để vào form đặt lịch",
        "Chọn slot sạc (xem trạng thái: Trống / Có lịch đặt / Bảo trì)",
        "Chọn ngày giờ bắt đầu và thời lượng (0.5h – 24h), hệ thống tự tính tiền theo khung giá",
        "Thêm dịch vụ bổ sung nếu có (cáp sạc, nước, bơm lốp…) và dùng điểm tích lũy để giảm giá",
        "Xác nhận đặt lịch → Chủ trạm duyệt → Thanh toán → Check-in tại trạm",
      ],
    },
    {
      icon: "💰",
      title: "Thanh toán tiện lợi",
      desc: "Ví điện tử tích hợp, nạp tiền qua VNPay và thanh toán tự động.",
      detail: [
        "Vào mục \"Ví tiền\" từ thanh điều hướng để xem số dư và số tiền đang giữ",
        "Nạp tiền vào ví qua cổng thanh toán VNPay",
        "Khi booking được duyệt, tiền tự động trừ từ ví (BookingPayment)",
        "Nếu hủy booking, tiền được hoàn lại vào ví (BookingCancel/Refund)",
        "Xem toàn bộ lịch sử giao dịch: nạp tiền, thanh toán, hoàn tiền với thời gian chi tiết",
      ],
    },
    {
      icon: "⭐",
      title: "Điểm tích lũy",
      desc: "Tích điểm mỗi lần sạc xe, dùng điểm giảm giá cho booking tiếp theo.",
      detail: [
        "Vào mục \"Điểm thưởng\" để xem tổng điểm tích lũy hiện tại (1 điểm = 1 VND)",
        "Mỗi lần hoàn tất booking, bạn nhận điểm theo tỷ lệ tích (ví dụ: 5% giá trị booking)",
        "Khi đặt lịch mới, bạn có thể dùng điểm để giảm giá (tối đa theo tỷ lệ quy định)",
        "Theo dõi lịch sử tích/dùng điểm, mỗi giao dịch gắn với booking tương ứng",
      ],
    },
    {
      icon: "📊",
      title: "Quản lý trạm sạc",
      desc: "Dành cho Chủ trạm: tạo trạm, quản lý booking và tương tác với tài xế.",
      detail: [
        "Đăng ký vai trò \"Chủ trạm\" khi tạo tài khoản để truy cập trang quản lý",
        "Tạo trạm sạc với đầy đủ thông tin: vị trí trên bản đồ, slot sạc, giá theo khung giờ, giờ hoạt động",
        "Thêm dịch vụ bổ sung (cáp sạc, nước...) với giá và số lượng tồn kho",
        "Duyệt hoặc từ chối booking từ tài xế, theo dõi trạng thái phiên sạc theo thời gian thực",
        "Xem và phản hồi đánh giá từ tài xế, nhắn tin trực tiếp qua hệ thống chat",
      ],
    },
    {
      icon: "🔒",
      title: "Bảo mật tuyệt đối",
      desc: "Xác thực OTP, quét QR check-in và bảo vệ thông tin cá nhân.",
      detail: [
        "Đăng ký bằng số điện thoại, xác thực OTP qua SMS trước khi tạo tài khoản",
        "Quên mật khẩu? Nhận OTP qua SMS để xác thực và đặt mật khẩu mới",
        "Check-in tại trạm bằng quét QR code — hệ thống tự xác thực booking (trạng thái Paid + khung giờ)",
        "Đổi mật khẩu bất cứ lúc nào từ trang hồ sơ cá nhân",
      ],
    },
  ];

  const toggleFeature = (i) => {
    setExpandedFeature(expandedFeature === i ? null : i);
  };

  const steps = [
    { num: "01", title: "Tìm trạm sạc", desc: "Mở bản đồ và tìm trạm sạc" },
    { num: "02", title: "Đặt lịch", desc: "Chọn khung giờ và trụ sạc phù hợp" },
    { num: "03", title: "Check-in", desc: "Quét QR code tại trạm để bắt đầu sạc" },
    { num: "04", title: "Sạc & Thanh toán", desc: "Theo dõi phiên sạc và thanh toán" },
  ];

  return (
    <div>
      {/* ===== HERO SECTION ===== */}
      <section className="cs-hero">
        <div className="cs-hero__bg" />
        <div className="cs-hero__content">
          <div className="cs-hero__badge cs-animate-fadeInUp">
            <span>⚡</span> Nền tảng #1 Việt Nam
          </div>
          <h1 className="cs-hero__title cs-animate-fadeInUp-delay-1">
            Đặt lịch sạc xe điện<br />
            <span className="cs-hero__title-accent">dễ dàng & thông minh</span>
          </h1>
          <p className="cs-hero__desc cs-animate-fadeInUp-delay-2">
            ChargeSlot giúp bạn tìm trạm sạc, đặt lịch và thanh toán chỉ trong vài chạm.
            Trải nghiệm sự tiện lợi với hệ thống quản lý trạm sạc hiện đại nhất.
          </p>
          <div className="cs-hero__actions cs-animate-fadeInUp-delay-3">
            <button className="cs-hero__btn cs-hero__btn--primary" onClick={() => navigate("/driver/map")}>
              🗺️ Tìm trạm sạc ngay
            </button>
            <button className="cs-hero__btn cs-hero__btn--secondary" onClick={() => navigate("/register")}>
              Đăng ký miễn phí
            </button>
          </div>
        </div>
      </section>

      {/* ===== FEATURES SECTION ===== */}
      <section className="cs-features">
        <div className="cs-features__container">
          <div className="cs-features__header">
            <span className="cs-features__badge">✨ Tính năng nổi bật</span>
            <h2 className="cs-features__title">Tại sao chọn ChargeSlot?</h2>
            <p className="cs-features__subtitle">
              Giải pháp toàn diện cho cả tài xế và chủ trạm sạc xe điện — bấm vào để xem chi tiết
            </p>
          </div>
          <div className="cs-features__grid">
            {features.map((f, i) => (
              <div
                key={i}
                className={`cs-feature-card ${expandedFeature === i ? "cs-feature-card--expanded" : ""}`}
                onClick={() => toggleFeature(i)}
              >
                <div className="cs-feature-card__header">
                  <div className="cs-feature-card__icon">{f.icon}</div>
                  <svg
                    className={`cs-feature-card__chevron ${expandedFeature === i ? "cs-feature-card__chevron--open" : ""}`}
                    width="20" height="20" fill="none" stroke="currentColor" viewBox="0 0 24 24"
                  >
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                  </svg>
                </div>
                <h3 className="cs-feature-card__title">{f.title}</h3>
                <p className="cs-feature-card__desc">{f.desc}</p>

                {/* Expandable detail section */}
                <div className={`cs-feature-card__detail ${expandedFeature === i ? "cs-feature-card__detail--open" : ""}`}>
                  <div className="cs-feature-card__detail-inner">
                    <div className="cs-feature-card__detail-divider" />
                    <p className="cs-feature-card__detail-label">📋 Cách sử dụng:</p>
                    <ol className="cs-feature-card__detail-steps">
                      {f.detail.map((step, j) => (
                        <li key={j}>
                          <span className="cs-feature-card__step-num">{j + 1}</span>
                          <span>{step}</span>
                        </li>
                      ))}
                    </ol>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ===== HOW IT WORKS ===== */}
      <section className="cs-steps">
        <div className="cs-steps__container">
          <div className="cs-steps__header">
            <span className="cs-features__badge">🚀 Hướng dẫn</span>
            <h2 className="cs-features__title">Sử dụng ChargeSlot như thế nào?</h2>
            <p className="cs-features__subtitle">Chỉ 4 bước đơn giản để bắt đầu sạc xe</p>
          </div>
          <div className="cs-steps__grid">
            {steps.map((s, i) => (
              <div key={i} className="cs-step-card">
                <span className="cs-step-card__num">{s.num}</span>
                <h3 className="cs-step-card__title">{s.title}</h3>
                <p className="cs-step-card__desc">{s.desc}</p>
                {i < steps.length - 1 && <div className="cs-step-card__arrow">→</div>}
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ===== CTA SECTION ===== */}
      <section className="cs-cta">
        <div className="cs-cta__container">
          <ChargeSlotLogo size={48} dark />
          <h2 className="cs-cta__title">Bắt đầu sạc xe thông minh ngay hôm nay</h2>
          <p className="cs-cta__desc">
            Đăng ký miễn phí và khám phá hàng trăm trạm sạc trên toàn quốc.
          </p>
          <div className="cs-cta__actions">
            <button className="cs-hero__btn cs-hero__btn--primary" onClick={() => navigate("/register")}>
              Đăng ký miễn phí
            </button>
            <button className="cs-hero__btn cs-hero__btn--secondary" onClick={() => navigate("/login")}>
              Đăng nhập
            </button>
          </div>
        </div>
      </section>

      <style>{`
        /* ===== HERO SECTION ===== */
        .cs-hero {
          position: relative;
          min-height: 600px;
          display: flex;
          align-items: center;
          justify-content: center;
          padding: 120px 24px 80px;
          overflow: hidden;
          background: linear-gradient(135deg, #1e293b 0%, #0f172a 50%, #1a1a2e 100%);
        }
        .cs-hero__bg {
          position: absolute;
          inset: 0;
          background: 
            radial-gradient(600px circle at 30% 40%, rgba(249,115,22,0.15), transparent 60%),
            radial-gradient(400px circle at 70% 60%, rgba(234,88,12,0.1), transparent 50%);
        }
        .cs-hero__content {
          position: relative;
          z-index: 1;
          text-align: center;
          max-width: 720px;
        }
        .cs-hero__badge {
          display: inline-flex;
          align-items: center;
          gap: 6px;
          padding: 6px 16px;
          background: rgba(249,115,22,0.15);
          border: 1px solid rgba(249,115,22,0.3);
          border-radius: 50px;
          color: #fb923c;
          font-size: 13px;
          font-weight: 600;
          margin-bottom: 24px;
        }
        .cs-hero__title {
          font-size: 48px;
          font-weight: 900;
          color: white;
          line-height: 1.15;
          margin-bottom: 20px;
          letter-spacing: -1px;
        }
        .cs-hero__title-accent {
          background: linear-gradient(135deg, #f97316, #fb923c, #fbbf24);
          -webkit-background-clip: text;
          -webkit-text-fill-color: transparent;
          background-clip: text;
        }
        .cs-hero__desc {
          font-size: 17px;
          line-height: 1.7;
          color: #94a3b8;
          margin-bottom: 36px;
          max-width: 560px;
          margin-left: auto;
          margin-right: auto;
        }
        .cs-hero__actions {
          display: flex;
          align-items: center;
          justify-content: center;
          gap: 14px;
          margin-bottom: 48px;
        }
        .cs-hero__btn {
          padding: 14px 32px;
          border-radius: 14px;
          font-size: 15px;
          font-weight: 700;
          cursor: pointer;
          transition: all 0.3s cubic-bezier(0.4,0,0.2,1);
          border: none;
          display: inline-flex;
          align-items: center;
          gap: 8px;
        }
        .cs-hero__btn--primary {
          background: linear-gradient(135deg, #f97316, #ea580c);
          color: white;
          box-shadow: 0 8px 30px rgba(249,115,22,0.35);
        }
        .cs-hero__btn--primary:hover {
          transform: translateY(-3px);
          box-shadow: 0 12px 40px rgba(249,115,22,0.45);
        }
        .cs-hero__btn--secondary {
          background: rgba(255,255,255,0.08);
          color: white;
          border: 1.5px solid rgba(255,255,255,0.15);
        }
        .cs-hero__btn--secondary:hover {
          background: rgba(255,255,255,0.15);
          transform: translateY(-2px);
        }
        .cs-hero__stats {
          display: inline-flex;
          align-items: center;
          gap: 28px;
          padding: 20px 36px;
          background: rgba(255,255,255,0.05);
          backdrop-filter: blur(20px);
          border: 1px solid rgba(255,255,255,0.08);
          border-radius: 20px;
        }
        .cs-hero__stat { text-align: center; }
        .cs-hero__stat-num {
          display: block;
          font-size: 28px;
          font-weight: 800;
          color: #f97316;
        }
        .cs-hero__stat-label {
          font-size: 13px;
          color: #64748b;
          margin-top: 2px;
        }
        .cs-hero__stat-divider {
          width: 1px;
          height: 36px;
          background: rgba(255,255,255,0.1);
        }
        
        @media (max-width: 640px) {
          .cs-hero { padding: 100px 16px 60px; min-height: auto; }
          .cs-hero__title { font-size: 30px; }
          .cs-hero__desc { font-size: 15px; }
          .cs-hero__actions { flex-direction: column; }
          .cs-hero__stats { flex-direction: column; gap: 16px; padding: 20px; }
          .cs-hero__stat-divider { width: 40px; height: 1px; }
        }

        /* ===== FEATURES SECTION ===== */
        .cs-features {
          padding: 80px 24px;
          background: #f8fafc;
        }
        .cs-features__container, .cs-steps__container, .cs-cta__container {
          max-width: 1200px;
          margin: 0 auto;
        }
        .cs-features__header, .cs-steps__header {
          text-align: center;
          margin-bottom: 48px;
        }
        .cs-features__badge {
          display: inline-block;
          padding: 6px 16px;
          background: #fff7ed;
          color: #ea580c;
          font-size: 13px;
          font-weight: 600;
          border-radius: 50px;
          margin-bottom: 14px;
        }
        .cs-features__title {
          font-size: 32px;
          font-weight: 800;
          color: #1e293b;
          margin-bottom: 10px;
          letter-spacing: -0.5px;
        }
        .cs-features__subtitle {
          font-size: 16px;
          color: #64748b;
        }
        .cs-features__grid {
          display: grid;
          grid-template-columns: repeat(3, 1fr);
          gap: 24px;
        }
        @media (max-width: 900px) {
          .cs-features__grid { grid-template-columns: repeat(2, 1fr); }
        }
        @media (max-width: 560px) {
          .cs-features__grid { grid-template-columns: 1fr; }
          .cs-features__title { font-size: 24px; }
        }
        .cs-feature-card {
          background: white;
          border: 1px solid rgba(0,0,0,0.06);
          border-radius: 20px;
          padding: 32px 24px;
          transition: all 0.3s cubic-bezier(0.4,0,0.2,1);
          cursor: pointer;
          position: relative;
        }
        .cs-feature-card:hover {
          transform: translateY(-6px);
          box-shadow: 0 16px 48px rgba(0,0,0,0.1);
          border-color: rgba(249,115,22,0.2);
        }
        .cs-feature-card--expanded {
          border-color: rgba(249,115,22,0.3);
          box-shadow: 0 16px 48px rgba(249,115,22,0.12);
          transform: translateY(-4px);
        }
        .cs-feature-card__header {
          display: flex;
          align-items: flex-start;
          justify-content: space-between;
        }
        .cs-feature-card__icon {
          width: 52px;
          height: 52px;
          border-radius: 14px;
          background: linear-gradient(135deg, #fff7ed, #fed7aa);
          display: flex;
          align-items: center;
          justify-content: center;
          font-size: 24px;
          margin-bottom: 18px;
        }
        .cs-feature-card__chevron {
          color: #94a3b8;
          transition: transform 0.3s, color 0.3s;
          flex-shrink: 0;
          margin-top: 4px;
        }
        .cs-feature-card__chevron--open {
          transform: rotate(180deg);
          color: #f97316;
        }
        .cs-feature-card__title {
          font-size: 17px;
          font-weight: 700;
          color: #1e293b;
          margin-bottom: 8px;
        }
        .cs-feature-card__desc {
          font-size: 14px;
          color: #64748b;
          line-height: 1.6;
        }

        /* Expandable Detail */
        .cs-feature-card__detail {
          max-height: 0;
          overflow: hidden;
          transition: max-height 0.4s cubic-bezier(0.4, 0, 0.2, 1);
        }
        .cs-feature-card__detail--open {
          max-height: 400px;
        }
        .cs-feature-card__detail-inner {
          padding-top: 4px;
        }
        .cs-feature-card__detail-divider {
          height: 1px;
          background: linear-gradient(90deg, transparent, rgba(249,115,22,0.2), transparent);
          margin: 16px 0;
        }
        .cs-feature-card__detail-label {
          font-size: 13px;
          font-weight: 700;
          color: #ea580c;
          margin-bottom: 12px;
        }
        .cs-feature-card__detail-steps {
          list-style: none;
          padding: 0;
          margin: 0;
          display: flex;
          flex-direction: column;
          gap: 10px;
        }
        .cs-feature-card__detail-steps li {
          display: flex;
          align-items: flex-start;
          gap: 10px;
          font-size: 13px;
          color: #475569;
          line-height: 1.5;
        }
        .cs-feature-card__step-num {
          width: 22px;
          height: 22px;
          border-radius: 50%;
          background: linear-gradient(135deg, #fff7ed, #fed7aa);
          color: #ea580c;
          font-size: 11px;
          font-weight: 700;
          display: flex;
          align-items: center;
          justify-content: center;
          flex-shrink: 0;
          margin-top: 1px;
        }

        /* ===== STEPS SECTION ===== */
        .cs-steps {
          padding: 80px 24px;
          background: white;
        }
        .cs-steps__grid {
          display: grid;
          grid-template-columns: repeat(4, 1fr);
          gap: 24px;
          position: relative;
        }
        @media (max-width: 768px) {
          .cs-steps__grid { grid-template-columns: repeat(2, 1fr); }
        }
        @media (max-width: 480px) {
          .cs-steps__grid { grid-template-columns: 1fr; }
        }
        .cs-step-card {
          text-align: center;
          padding: 28px 20px;
          position: relative;
        }
        .cs-step-card__num {
          display: inline-block;
          font-size: 36px;
          font-weight: 900;
          background: linear-gradient(135deg, #f97316, #ea580c);
          -webkit-background-clip: text;
          -webkit-text-fill-color: transparent;
          background-clip: text;
          margin-bottom: 12px;
        }
        .cs-step-card__title {
          font-size: 17px;
          font-weight: 700;
          color: #1e293b;
          margin-bottom: 8px;
        }
        .cs-step-card__desc {
          font-size: 14px;
          color: #64748b;
          line-height: 1.5;
        }
        .cs-step-card__arrow {
          display: none;
          position: absolute;
          right: -14px;
          top: 40px;
          font-size: 24px;
          color: #cbd5e1;
          font-weight: 300;
        }
        @media (min-width: 769px) {
          .cs-step-card__arrow { display: block; }
        }

        /* ===== CTA SECTION ===== */
        .cs-cta {
          padding: 80px 24px;
          background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%);
        }
        .cs-cta__container {
          text-align: center;
        }
        .cs-cta__title {
          font-size: 32px;
          font-weight: 800;
          color: white;
          margin-top: 24px;
          margin-bottom: 12px;
          letter-spacing: -0.5px;
        }
        .cs-cta__desc {
          font-size: 16px;
          color: #94a3b8;
          margin-bottom: 32px;
          max-width: 500px;
          margin-left: auto;
          margin-right: auto;
        }
        .cs-cta__actions {
          display: flex;
          align-items: center;
          justify-content: center;
          gap: 14px;
        }
        @media (max-width: 480px) {
          .cs-cta__actions { flex-direction: column; }
          .cs-cta__title { font-size: 24px; }
        }
      `}</style>
    </div>
  );
}
