import { Button } from "@/components/ui/button";
import { Link } from "react-router-dom";
export default function Login() {
  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center">
      <form className="max-w-[500px] w-full bg-white rounded-md shadow-md">
        <div className="p-8">
          <h1 className="text-xl font-bold mb-5">
            Đăng nhập tài khoản của bạn
          </h1>
          <div className="flex flex-col mb-5">
            <label className="text-gray-500">Số điện thoại</label>
            <input
              type="text"
              className="w-full h-10 px-4 border placeholder:text-gray-500 outline-none"
              placeholder="Nhập số điện thoại..."
            />
          </div>
          <div className="flex flex-col mb-5">
            <label className="text-gray-500">Mật khẩu</label>
            <input
              type="password"
              className="w-full h-10 px-4 border placeholder:text-gray-500 outline-none"
              placeholder="Nhập mật khẩu..."
            />
          </div>
          <Link className="hover:underline block text-blue-500 mb-5 ">
            Quên mật khẩu ?
          </Link>
          <Button className="w-full h-12 bg-orange-500 hover:bg-green-500 cursor-pointer mb-5">
            Đăng nhập
          </Button>
          <div className="text-center">
            <span>Chưa có tài khoản? </span>
            <Link to="/register" className="hover:underline text-blue-500 mb-5">
              Đăng ký
            </Link>
          </div>
        </div>
      </form>
    </div>
  );
}
