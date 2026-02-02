import { Link } from "react-router-dom";
export default function Footer() {
  return (
    <div className="bg-black/90 min-h-60 flex items-center">
      <div className="max-w-[95%] w-full mx-auto flex justify-between gap-10">
        <div className="text-white flex flex-col">
          <h1 className="text-xl mb-3 font-bold">ChargeSlot</h1>
          <p className="max-w-60">
            Giải pháp trạm sạc xe điện thông minh cho tương lai bền vững
          </p>
        </div>
        <div className="text-white flex flex-col">
          <h1 className="text-xl mb-3 font-bold">Sản phẩm</h1>
          <Link className="hover:text-orange-500">Tìm trạm sạc</Link>
          <Link className="hover:text-orange-500">Đặt chỗ</Link>
          <Link className="hover:text-orange-500">Lịch sử sạc</Link>
          <Link className="hover:text-orange-500">Ví điện tử</Link>
        </div>
        <div className="text-white flex flex-col">
          <h1 className="text-xl mb-3 font-bold">Công ty</h1>
          <Link className="hover:text-orange-500">Về chúng tôi</Link>
          <Link className="hover:text-orange-500">Blog</Link>
          <Link className="hover:text-orange-500">Tuyển dụng</Link>
          <Link className="hover:text-orange-500">Liên hệ</Link>
        </div>
        <div className="text-white flex flex-col">
          <h1 className="text-xl mb-3 font-bold">Pháp lý</h1>
          <Link className="hover:text-orange-500">Điều khoản dịch vụ</Link>
          <Link className="hover:text-orange-500">Chính sách bải mật</Link>
          <Link className="hover:text-orange-500">Cookie</Link>
          <Link className="hover:text-orange-500">Hỗ trợ</Link>
        </div>
      </div>
    </div>
  );
}
