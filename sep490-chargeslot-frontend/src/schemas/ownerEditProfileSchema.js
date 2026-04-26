import { z } from "zod";

const trimString = (value) => String(value ?? "").trim();

export const ownerEditProfileSchema = z.object({
  email: z
    .string()
    .transform(trimString)
    .optional()
    .refine((v) => !v || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v), "Email không hợp lệ"),
  businessName: z
    .string()
    .transform(trimString)
    .refine((v) => v.length > 0, "Tên doanh nghiệp không được để trống"),
  taxCode: z
    .string()
    .transform(trimString)
    .refine((v) => /^\d{12}$/.test(v), "Mã số thuế phải đúng 12 chữ số"),
});

