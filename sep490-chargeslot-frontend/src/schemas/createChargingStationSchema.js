import { z } from "zod";

const emptyToUndefined = (value) => {
  if (value === "" || value === null || value === undefined) {
    return undefined;
  }
  return value;
};

const requiredNumber = (label, min = 0) =>
  z.preprocess(
    emptyToUndefined,
    z.coerce
      .number({
        invalid_type_error: `${label} không hợp lệ`,
      })
      .min(min, `${label} phải lớn hơn hoặc bằng ${min}`),
  );

const optionalNumber = (label, min, max) => {
  let schema = z.coerce.number({
    invalid_type_error: `${label} không hợp lệ`,
  });

  if (typeof min === "number") {
    schema = schema.min(min, `${label} phải lớn hơn hoặc bằng ${min}`);
  }

  if (typeof max === "number") {
    schema = schema.max(max, `${label} phải nhỏ hơn hoặc bằng ${max}`);
  }

  return z.preprocess(emptyToUndefined, schema.optional());
};

const timeRegex = /^([01]\d|2[0-3]):[0-5]\d(:[0-5]\d)?$/;

const operatingHourSchema = z
  .object({
    dayOfWeek: z.coerce.number().int().min(0).max(6),
    isClosed: z.boolean(),
    openTime: z.string().optional(),
    closeTime: z.string().optional(),
  })
  .superRefine((value, context) => {
    if (value.isClosed) {
      return;
    }

    if (!value.openTime) {
      context.addIssue({
        code: z.ZodIssueCode.custom,
        message: "Vui lòng nhập giờ mở cửa",
        path: ["openTime"],
      });
    }

    if (!value.closeTime) {
      context.addIssue({
        code: z.ZodIssueCode.custom,
        message: "Vui lòng nhập giờ đóng cửa",
        path: ["closeTime"],
      });
    }

    if (value.openTime && !timeRegex.test(value.openTime)) {
      context.addIssue({
        code: z.ZodIssueCode.custom,
        message: "Giờ mở cửa không đúng định dạng",
        path: ["openTime"],
      });
    }

    if (value.closeTime && !timeRegex.test(value.closeTime)) {
      context.addIssue({
        code: z.ZodIssueCode.custom,
        message: "Giờ đóng cửa không đúng định dạng",
        path: ["closeTime"],
      });
    }

    if (
      value.openTime &&
      value.closeTime &&
      value.openTime >= value.closeTime
    ) {
      context.addIssue({
        code: z.ZodIssueCode.custom,
        message: "Giờ đóng cửa phải sau giờ mở cửa",
        path: ["closeTime"],
      });
    }
  });

const slotSchema = z.object({
  slotName: z.string().trim().min(1, "Tên trụ sạc là bắt buộc"),
  connectorType: z.string().trim().min(1, "Loại đầu sạc là bắt buộc"),
  powerKw: requiredNumber("Công suất", 0),
  basePricePerHour: requiredNumber("Giá theo giờ", 0),
  positionX: optionalNumber("Tọa độ X", 0),
  positionY: optionalNumber("Tọa độ Y", 0),
  status: z.string().optional(),
});

export const createChargingStationSchema = z.object({
  name: z.string().trim().min(1, "Tên trạm là bắt buộc").max(255),
  address: z.string().trim().min(1, "Địa chỉ là bắt buộc").max(300),
  description: z.string().trim().max(2000).optional(),
  latitude: optionalNumber("Vĩ độ", -90, 90),
  longitude: optionalNumber("Kinh độ", -180, 180),
  layoutImageUrl: z.preprocess(
    emptyToUndefined,
    z.string().url("URL sơ đồ không hợp lệ").max(500).optional(),
  ),
  layoutWidth: requiredNumber("Số cột sơ đồ", 1),
  layoutHeight: requiredNumber("Số hàng sơ đồ", 1),
  operatingHours: z
    .array(operatingHourSchema)
    .min(1, "Cần ít nhất một cấu hình giờ hoạt động"),
  slots: z.array(slotSchema).min(1, "Cần ít nhất một trụ sạc"),
});
