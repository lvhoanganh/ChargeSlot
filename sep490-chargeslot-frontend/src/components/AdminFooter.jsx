import { Link } from "react-router-dom";

export default function AdminFooter() {
  return (
    <div className="bg-black/90 min-h-60 flex items-center">
      <div className="max-w-[95%] w-full mx-auto flex justify-between gap-10">
        <div className="text-white flex flex-col">
          <h1 className="text-xl mb-3 font-bold">ChargeSlot - Admin</h1>
          <p className="max-w-60">
            Trung tâm quản trị dành cho việc kiểm duyệt tài khoản, báo cáo, trạm và giải quyết tranh chấp.
          </p>
        </div>
        <div className="text-white flex flex-col">
          <h1 className="text-xl mb-3 font-bold">Quản lý tài khoản</h1>
          <Link to="/admin/manage-users" className="hover:text-orange-500">
            Xem danh sách tài khoản
          </Link>
          <Link to="/admin/manage-users" className="hover:text-orange-500">
            Tìm kiếm tài khoản
          </Link>
          <Link to="/admin/manage-users" className="hover:text-orange-500">
            Ban tài khoản
          </Link>
        </div>
        <div className="text-white flex flex-col">
          <h1 className="text-xl mb-3 font-bold">Quản lý vận hành</h1>
          <Link to="/admin/approve-station" className="hover:text-orange-500">
            Duyệt hoặc từ chối trạm sạc
          </Link>
          <Link to="/admin/resolve-dispute" className="hover:text-orange-500">
            Giải quyết tranh chấp
          </Link>
          <Link
            to="/admin/view-financial-report"
            className="hover:text-orange-500"
          >
            Báo cáo tài chính
          </Link>
        </div>
      </div>
    </div>
  );
}
