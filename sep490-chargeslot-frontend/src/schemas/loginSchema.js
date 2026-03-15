import { z } from "zod";

export const loginSchema = z.object({
  phoneNumber: z
    .string()
    .min(1, "Số điện thoại không được để trống")
    .regex(/^(0[3|5|7|8|9])+([0-9]{8})$/, "Số điện thoại không hợp lệ"),
  password: z
    .string()
    .min(1, "Mật khẩu không được để trống")
    .min(6, "Mật khẩu phải có ít nhất 6 ký tự"),
});
