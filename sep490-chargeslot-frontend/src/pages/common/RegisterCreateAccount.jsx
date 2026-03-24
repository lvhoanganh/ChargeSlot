import { Button } from "@/components/ui/button";
import { Link, useNavigate } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import { instance } from "@/lib/httpRequest";
import { useAuthStore } from "@/stores/authStore";
import { useState } from "react";
import { showToast } from "@/components/Toast";

const register = async ({ phoneNumber, fullName, password, role }) => {
  const res = await instance.post(
    `${import.meta.env.VITE_BASE_URL}/Auth/register`,
    {
      phoneNumber,
      fullName,
      password,
      role,
    },
  );
  return res.data;
};

export default function RegisterCreateAccount() {
  const [fullName, setFullName] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState("Driver");
  const { phoneNumber } = useAuthStore();
  const navigate = useNavigate();

  const registerMutation = useMutation({
    mutationFn: register,
    onSuccess: () => {
      showToast.success("Đăng ký tài khoản thành công!");
      navigate("/login");
    },
    onError: (error) => {
      console.error("Failed to register:", error);
      showToast.error("Đăng ký tài khoản thất bại. Vui lòng thử lại.");
    },
  });

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!phoneNumber) {
      showToast.warning("Vui lòng quay lại trang đăng ký để nhập số điện thoại.");
      navigate("/register");
      return;
    }
    registerMutation.mutate({ phoneNumber, fullName, password, role });
  };

  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center">
      <form
        className="max-w-[500px] w-full bg-white rounded-md shadow-md"
        onSubmit={handleSubmit}
      >
        <div className="p-8">
          <h1 className="text-xl font-bold mb-5">Tạo tài khoản</h1>
          <p className="mb-5 text-gray-600">
            Số điện thoại: <strong>{phoneNumber}</strong>
          </p>
          <div className="flex flex-col mb-5">
            <label>Họ và tên</label>
            <input
              className="h-10 px-4 border"
              placeholder="Nhập họ và tên"
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              required
            />
          </div>
          <div className="flex flex-col mb-5">
            <label>Mật khẩu</label>
            <input
              type="password"
              className="h-10 px-4 border"
              placeholder="Nhập mật khẩu"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </div>
          <div className="flex flex-col mb-5">
            <label>Vai trò</label>
            <select
              className="h-10 px-4 border"
              value={role}
              onChange={(e) => setRole(e.target.value)}
              required
            >
              <option value="Driver">Driver</option>
              <option value="Owner">Owner</option>
            </select>
          </div>
          <Button
            type="submit"
            className="w-full h-12 bg-orange-500 mb-5 cursor-pointer hover:bg-green-500"
            disabled={registerMutation.isPending}
          >
            {registerMutation.isPending ? "Đang đăng ký..." : "Đăng ký"}
          </Button>
          <div className="text-center hover:underline">
            <Link to="/login" className="text-blue-500">
              Đã có tài khoản? Đăng nhập
            </Link>
          </div>
        </div>
      </form>
    </div>
  );
}
