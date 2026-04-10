import { z } from "zod";

export const forgotPasswordSchema = z.object({
  phoneNumber: z
    .string()
    .min(1, "Số điện thoại không được để trống")
    .regex(/^(0[3|5|7|8|9])+([0-9]{8})$/, "Số điện thoại không hợp lệ"),
});

