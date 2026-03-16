import { Button } from "@/components/ui/button";
import { instance } from "@/lib/httpRequest";
import { verifyOtpSchema } from "@/schemas/verifyOtpSchema";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { useLocation, useNavigate } from "react-router-dom";

const FORGOT_PHONE_KEY = "forgotPasswordPhoneNumber";

export default function VerifyOtp() {
  const navigate = useNavigate();
  const location = useLocation();

  const purpose = location.state?.purpose || "forgot";
  const [apiError, setApiError] = useState("");
  const [cooldownSeconds, setCooldownSeconds] = useState(0);

  const phoneNumber = useMemo(() => {
    return (
      location.state?.phoneNumber ||
      localStorage.getItem(FORGOT_PHONE_KEY) ||
      ""
    );
  }, [location.state?.phoneNumber]);

  const form = useForm({
    resolver: zodResolver(verifyOtpSchema),
    defaultValues: { otp: "" },
    mode: "onTouched",
  });

  useEffect(() => {
    if (!phoneNumber) {
      navigate("/forgotPassword", { replace: true });
    }
  }, [phoneNumber, navigate]);

  useEffect(() => {
    if (cooldownSeconds <= 0) return;
    const t = setInterval(() => setCooldownSeconds((s) => s - 1), 1000);
    return () => clearInterval(t);
  }, [cooldownSeconds]);

  const verifyOtpMutation = useMutation({
    mutationFn: async ({ phoneNumber, otp }) => {
      const url =
        purpose === "register"
          ? `${import.meta.env.VITE_BASE_URL}/Auth/register/verify-otp`
          : `${import.meta.env.VITE_BASE_URL}/Auth/forgot-password/verify-otp`;

      const res = await instance.post(url, { phoneNumber, otp });
      return res.data;
    },
    onSuccess: () => {
      setApiError("");
      if (purpose === "register") {
        navigate("/register/create-account", { replace: true });
        return;
      }
      navigate("/reset-password", { replace: true, state: { phoneNumber } });
    },
    onError: (error) => {
      setApiError(getApiErrorMessage(error, "Xác thực OTP thất bại. Vui lòng thử lại."));
    },
  });

  const resendOtpMutation = useMutation({
    mutationFn: async (phoneNumber) => {
      const url =
        purpose === "register"
          ? `${import.meta.env.VITE_BASE_URL}/Auth/register/send-otp`
          : `${import.meta.env.VITE_BASE_URL}/Auth/forgot-password/send-otp`;

      const res = await instance.post(url, { phoneNumber });
      return res.data;
    },
    onSuccess: () => {
      setApiError("");
      setCooldownSeconds(60);
    },
    onError: (error) => {
      setApiError(getApiErrorMessage(error, "Gửi lại OTP thất bại. Vui lòng thử lại."));
    },
  });

  const title =
    purpose === "register" ? "Xác thực OTP – Đăng ký" : "Xác thực OTP – Quên mật khẩu";

  return (
    <div className="min-h-screen bg-[#f3f4f5] flex justify-center items-center px-4">
      <form
        className="max-w-[500px] w-full bg-white rounded-xl shadow-md"
        onSubmit={form.handleSubmit((values) => {
          setApiError("");
          verifyOtpMutation.mutate({ phoneNumber, otp: values.otp });
        })}
      >
        <div className="p-8">
          <h1 className="text-xl font-bold mb-4">{title}</h1>

          <p className="mb-5 text-gray-600">
            Mã OTP đã được gửi đến số điện thoại: <strong>{phoneNumber}</strong>
          </p>

          <div className="flex flex-col mb-3">
            <label className="mb-1">Mã OTP</label>
            <input
              className="h-11 px-4 border rounded-md"
              placeholder="123456"
              maxLength={6}
              {...form.register("otp")}
            />
            {!!form.formState.errors.otp?.message && (
              <p className="mt-1 text-sm text-red-600">
                {form.formState.errors.otp.message}
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
            className="w-full h-12 bg-orange-500 hover:bg-orange-600 mb-4"
            disabled={verifyOtpMutation.isPending}
          >
            {verifyOtpMutation.isPending ? "Đang xác nhận..." : "Xác nhận OTP"}
          </Button>

          <button
            type="button"
            className="text-blue-500 w-full text-sm disabled:opacity-50"
            onClick={() => resendOtpMutation.mutate(phoneNumber)}
            disabled={resendOtpMutation.isPending || cooldownSeconds > 0}
          >
            {cooldownSeconds > 0
              ? `Gửi lại OTP (${cooldownSeconds}s)`
              : resendOtpMutation.isPending
                ? "Đang gửi..."
                : "Gửi lại OTP"}
          </button>
        </div>
      </form>
    </div>
  );
}

function getApiErrorMessage(error, fallback) {
  return (
    error?.response?.data?.message ||
    error?.response?.data?.error ||
    error?.message ||
    fallback
  );
}

