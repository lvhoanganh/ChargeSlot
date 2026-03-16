import { z } from "zod";

const trimString = (value) => String(value ?? "").trim();

export const ownerEditProfileSchema = z.object({
  fullName: z
    .string()
    .transform(trimString)
    .refine((v) => v.length > 0, "Họ và tên không được để trống"),
  businessName: z
    .string()
    .transform(trimString)
    .refine((v) => v.length > 0, "Tên doanh nghiệp không được để trống"),
  taxCode: z
    .string()
    .transform(trimString)
    .refine((v) => /^\d{10}$/.test(v), "Mã số thuế phải đúng 10 chữ số"),
});

