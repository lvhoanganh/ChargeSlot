import { z } from "zod";

export const verifyOtpSchema = z.object({
  otp: z
    .string()
    .min(4, "OTP không hợp lệ")
    .max(6, "OTP không hợp lệ")
    .regex(/^[0-9]{4,6}$/, "OTP chỉ gồm 4-6 chữ số"),
});

