import { Button } from "@/components/ui/button";
import { instance } from "@/lib/httpRequest";
import { forgotPasswordSchema } from "@/schemas/forgotPasswordSchema";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router-dom";

const FORGOT_PHONE_KEY = "forgotPasswordPhoneNumber";

export default function ForgotPassword() {
  const navigate = useNavigate();
  const role = localStorage.getItem("role") || "";
  const backPath = getBackPathByRole(role);
  const [apiError, setApiError] = useState("");

  const form = useForm({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: {
      phoneNumber: localStorage.getItem(FORGOT_PHONE_KEY) || "",
    },
    mode: "onTouched",
  });

  const sendOtpMutation = useMutation({
    mutationFn: async (phoneNumber) => {
      const res = await instance.post(
        `${import.meta.env.VITE_BASE_URL}/Auth/forgot-password/send-otp`,
        { phoneNumber },
      );
      return res.data;
    },
    onSuccess: (_, phoneNumber) => {
      localStorage.setItem(FORGOT_PHONE_KEY, phoneNumber);
      navigate("/verifyOtp", {
        state: { purpose: "forgot", phoneNumber },
      });
    },
    onError: (error) => {
      setApiError(getApiErrorMessage(error, "Gửi OTP thất bại. Vui lòng thử lại."));
    },
  });

  const onSubmit = (values) => {
    setApiError("");
    sendOtpMutation.mutate(values.phoneNumber);
  };

  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center px-4">
      <form
        className="max-w-[500px] w-full bg-white rounded-md shadow-md"
        onSubmit={form.handleSubmit(onSubmit)}
      >
        <div className="p-8">
          <h1 className="text-xl font-bold mb-5">Quên mật khẩu</h1>

          <div className="flex flex-col mb-5">
            <label>Số điện thoại</label>
            <input
              className="h-10 px-4 border"
              placeholder="090xxxxxxx"
              {...form.register("phoneNumber")}
            />
            {!!form.formState.errors.phoneNumber?.message && (
              <p className="mt-1 text-sm text-red-600">
                {form.formState.errors.phoneNumber.message}
              </p>
            )}
          </div>

          {!!apiError && (
            <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded">
              <p className="text-sm text-red-700">{apiError}</p>
            </div>
          )}

          <Button
            type="submit"
            className="w-full h-12 bg-orange-500 mb-5"
            disabled={sendOtpMutation.isPending}
          >
            {sendOtpMutation.isPending ? "Đang gửi..." : "Gửi OTP"}
          </Button>

          <div className="text-center">
            {role ? (
              <button
                type="button"
                className="text-blue-500 hover:underline"
                onClick={() => navigate(backPath)}
              >
                Quay lại hồ sơ
              </button>
            ) : (
              <Link to="/login" className="text-blue-500 hover:underline">
                Quay lại đăng nhập
              </Link>
            )}
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

