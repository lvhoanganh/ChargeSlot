import { z } from "zod";

const trimString = (value) => String(value ?? "").trim();

export const driverEditProfileSchema = z.object({
  fullName: z
    .string()
    .transform(trimString)
    .refine((v) => v.length > 0, "Họ và tên không được để trống"),
  vehicleType: z.string().transform(trimString).optional(),
  licensePlate: z.string().transform(trimString).optional(),
  licenseNumber: z
    .string()
    .transform(trimString)
    .refine((v) => v === "" || /^\d{12}$/.test(v), "Số giấy phép phải đúng 12 chữ số")
    .optional(),
});

