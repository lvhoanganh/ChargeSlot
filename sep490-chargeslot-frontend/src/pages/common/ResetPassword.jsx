import { Button } from "@/components/ui/button";
import { instance } from "@/lib/httpRequest";
import { resetPasswordSchema } from "@/schemas/resetPasswordSchema";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { useLocation, useNavigate } from "react-router-dom";

const FORGOT_PHONE_KEY = "forgotPasswordPhoneNumber";

export default function ResetPassword() {
  const navigate = useNavigate();
  const location = useLocation();

  const role = localStorage.getItem("role") || "";
  const backPath = getBackPathByRole(role);

  const initialPhone = useMemo(() => {
    return (
      location.state?.phoneNumber ||
      localStorage.getItem(FORGOT_PHONE_KEY) ||
      localStorage.getItem("phoneNumber") ||
      ""
    );
  }, [location.state?.phoneNumber]);

  const [apiError, setApiError] = useState("");

  const form = useForm({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: {
      phoneNumber: initialPhone,
      newPassword: "",
      confirmPassword: "",
    },
    mode: "onTouched",
  });

  const resetPasswordMutation = useMutation({
    mutationFn: async ({ phoneNumber, newPassword }) => {
      const res = await instance.post(
        `${import.meta.env.VITE_BASE_URL}/Auth/reset-password`,
        { phoneNumber, newPassword },
      );
      return res.data;
    },
    onSuccess: () => {
      localStorage.removeItem(FORGOT_PHONE_KEY);
      if (role) navigate(backPath, { replace: true });
      else navigate("/login", { replace: true });
    },
    onError: (error) => {
      setApiError(
        getApiErrorMessage(
          error,
          "Không thể đặt lại mật khẩu. Vui lòng thử lại.",
        ),
      );
    },
  });

  const onSubmit = (values) => {
    setApiError("");
    resetPasswordMutation.mutate({
      phoneNumber: values.phoneNumber,
      newPassword: values.newPassword,
    });
  };

  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center px-4">
      <form
        className="max-w-[500px] w-full bg-white rounded-md shadow-md"
        onSubmit={form.handleSubmit(onSubmit)}
      >
        <div className="p-8">
          <h1 className="text-xl font-bold mb-6">Đặt lại mật khẩu</h1>

          <div className="flex flex-col mb-4">
            <label className="text-gray-500">Số điện thoại</label>
            <input
              className="w-full h-10 px-4 border outline-none"
              placeholder="090xxxxxxx"
              {...form.register("phoneNumber")}
            />
            {!!form.formState.errors.phoneNumber?.message && (
              <p className="mt-1 text-sm text-red-600">
                {form.formState.errors.phoneNumber.message}
              </p>
            )}
          </div>

          <div className="flex flex-col mb-4">
            <label className="text-gray-500">Mật khẩu mới</label>
            <input
              type="password"
              className="w-full h-10 px-4 border outline-none"
              placeholder="Nhập mật khẩu mới..."
              {...form.register("newPassword")}
            />
            {!!form.formState.errors.newPassword?.message && (
              <p className="mt-1 text-sm text-red-600">
                {form.formState.errors.newPassword.message}
              </p>
            )}
          </div>

          <div className="flex flex-col mb-6">
            <label className="text-gray-500">Xác nhận mật khẩu mới</label>
            <input
              type="password"
              className="w-full h-10 px-4 border outline-none"
              placeholder="Nhập lại mật khẩu mới..."
              {...form.register("confirmPassword")}
            />
            {!!form.formState.errors.confirmPassword?.message && (
              <p className="mt-1 text-sm text-red-600">
                {form.formState.errors.confirmPassword.message}
              </p>
            )}
          </div>

          {!!apiError && (
            <div className="mb-5 p-3 bg-red-50 border border-red-200 rounded">
              <p className="text-sm text-red-700">{apiError}</p>
            </div>
          )}

          <div className="flex gap-3">
            <div className="w-1/2">
              <Button
                type="button"
                className="w-full h-12 bg-gray-300 hover:bg-gray-400 text-black"
                onClick={() => navigate(role ? backPath : "/forgotPassword")}
                disabled={resetPasswordMutation.isPending}
              >
                Huỷ
              </Button>
            </div>

            <Button
              type="submit"
              className="w-1/2 h-12 bg-orange-500 hover:bg-orange-600"
              disabled={resetPasswordMutation.isPending}
            >
              {resetPasswordMutation.isPending ? "Đang cập nhật..." : "Cập nhật"}
            </Button>
          </div>
        </div>
      </form>
    </div>
  );
}

function getBackPathByRole(role) {
  const r = String(role || "").trim().toLowerCase();
  if (r === "driver") return "/driver/driver-profile";
  if (r === "owner") return "/owner/owner-profile";
  return "/";
}

function getApiErrorMessage(error, fallback) {
  return (
    error?.response?.data?.message ||
    error?.response?.data?.error ||
    error?.message ||
    fallback
  );
}

