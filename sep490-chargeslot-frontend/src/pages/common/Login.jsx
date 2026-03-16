import { Button } from "@/components/ui/button";
import { Link, useNavigate } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { loginSchema } from "@/schemas/loginSchema";
import { useAuthStore } from "@/stores/authStore";
import { useState } from "react";
export default function Login() {
  const navigate = useNavigate();
  const [serverError, setServerError] = useState("");

  const { login } = useAuthStore();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(loginSchema),
  });

  const onSubmit = async (data) => {
    setServerError("");
    try {
      await login(data.phoneNumber, data.password);
      navigate("/");
    } catch (err) {
      setServerError(
        typeof err === "string" ? err : "Đăng nhập thất bại. Vui lòng thử lại.",
      );
    }
  };
  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center">
      <form
        className="max-w-[500px] w-full bg-white rounded-md shadow-md"
        onSubmit={handleSubmit(onSubmit)}
      >
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
              {...register("phoneNumber")}
            />
            {errors.phoneNumber && (
              <p className="text-red-500 text-sm mt-1">
                {errors.phoneNumber.message}
              </p>
            )}
          </div>
          <div className="flex flex-col mb-5">
            <label className="text-gray-500">Mật khẩu</label>
            <input
              type="password"
              className="w-full h-10 px-4 border placeholder:text-gray-500 outline-none"
              placeholder="Nhập mật khẩu..."
              {...register("password")}
            />
            {errors.password && (
              <p className="text-red-500 text-sm mt-1">
                {errors.password.message}
              </p>
            )}
          </div>
          {serverError && (
            <p className="mb-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {serverError}
            </p>
          )}
          <Link
            to="/forgotPassword"
            className="hover:underline block text-blue-500 mb-5 "
          >
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
