/**
 * Mock data cho các trạm sạc xe điện (Hà Nội - theo format hành chính mới).
 * Đã bỏ cấp Quận/Huyện, chỉ giữ: số nhà + đường + phường + thành phố.
 */
export const mockStations = [
  {
    id: 1,
    name: "ChargeSlot Hoàn Kiếm",
    address: "12 Tràng Tiền, Phường Tràng Tiền, Hà Nội",
    description:
      "Trạm sạc xe điện hiện đại nằm ngay trung tâm, gần Hồ Gươm. Hỗ trợ nhiều loại xe điện phổ biến.",
    latitude: 21.0248,
    longitude: 105.8542,
    operationalStatus: "Open",
    approvalStatus: "Approved",
    images: [
      {
        id: 1,
        imageUrl:
          "https://images.unsplash.com/photo-1558618666-fcd25c85f82e?w=400",
      },
    ],
    operatingHours: [
      { dayOfWeek: 0, openTime: "06:00", closeTime: "22:00", isClosed: false },
      { dayOfWeek: 1, openTime: "06:00", closeTime: "22:00", isClosed: false },
      { dayOfWeek: 2, openTime: "06:00", closeTime: "22:00", isClosed: false },
      { dayOfWeek: 3, openTime: "06:00", closeTime: "22:00", isClosed: false },
      { dayOfWeek: 4, openTime: "06:00", closeTime: "22:00", isClosed: false },
      { dayOfWeek: 5, openTime: "07:00", closeTime: "21:00", isClosed: false },
      { dayOfWeek: 6, openTime: "07:00", closeTime: "21:00", isClosed: false },
    ],
    chargingSlots: [
      {
        id: 1,
        slotName: "Slot A1",
        connectorType: "Type 2",
        powerOutput: 22,
        pricePerHour: 35000,
        status: "Available",
      },
      {
        id: 2,
        slotName: "Slot A2",
        connectorType: "CCS2",
        powerOutput: 50,
        pricePerHour: 55000,
        status: "Available",
      },
      {
        id: 3,
        slotName: "Slot A3",
        connectorType: "Type 2",
        powerOutput: 22,
        pricePerHour: 35000,
        status: "Occupied",
      },
    ],
  },
  {
    id: 2,
    name: "EV Station Cầu Giấy",
    address: "168 Xuân Thủy, Phường Dịch Vọng Hậu, Hà Nội",
    description:
      "Trạm sạc nhanh gần khu đại học, phù hợp cho sinh viên và cư dân khu vực.",
    latitude: 21.0378,
    longitude: 105.7828,
    operationalStatus: "Open",
    approvalStatus: "Approved",
    images: [
      {
        id: 2,
        imageUrl:
          "https://images.unsplash.com/photo-1593941707882-a5bba14938c7?w=400",
      },
    ],
    operatingHours: [
      { dayOfWeek: 0, openTime: "00:00", closeTime: "23:59", isClosed: false },
      { dayOfWeek: 1, openTime: "00:00", closeTime: "23:59", isClosed: false },
      { dayOfWeek: 2, openTime: "00:00", closeTime: "23:59", isClosed: false },
      { dayOfWeek: 3, openTime: "00:00", closeTime: "23:59", isClosed: false },
      { dayOfWeek: 4, openTime: "00:00", closeTime: "23:59", isClosed: false },
      { dayOfWeek: 5, openTime: "00:00", closeTime: "23:59", isClosed: false },
      { dayOfWeek: 6, openTime: "00:00", closeTime: "23:59", isClosed: false },
    ],
    chargingSlots: [
      {
        id: 4,
        slotName: "Slot B1",
        connectorType: "CCS2",
        powerOutput: 100,
        pricePerHour: 75000,
        status: "Available",
      },
      {
        id: 5,
        slotName: "Slot B2",
        connectorType: "CCS2",
        powerOutput: 100,
        pricePerHour: 75000,
        status: "Available",
      },
    ],
  },
  {
    id: 3,
    name: "Green Charge Hà Đông",
    address: "215 Quang Trung, Phường La Khê, Hà Nội",
    description:
      "Trạm sạc khu đô thị, thuận tiện cho cư dân và khách hàng mua sắm.",
    latitude: 20.9718,
    longitude: 105.7773,
    operationalStatus: "Open",
    approvalStatus: "Approved",
    images: [
      {
        id: 3,
        imageUrl:
          "https://images.unsplash.com/photo-1647166545674-ce28ce93bdca?w=400",
      },
    ],
    operatingHours: [
      { dayOfWeek: 0, openTime: "05:00", closeTime: "23:00", isClosed: false },
      { dayOfWeek: 1, openTime: "05:00", closeTime: "23:00", isClosed: false },
      { dayOfWeek: 2, openTime: "05:00", closeTime: "23:00", isClosed: false },
      { dayOfWeek: 3, openTime: "05:00", closeTime: "23:00", isClosed: false },
      { dayOfWeek: 4, openTime: "05:00", closeTime: "23:00", isClosed: false },
      { dayOfWeek: 5, openTime: "05:00", closeTime: "23:00", isClosed: false },
      { dayOfWeek: 6, openTime: "05:00", closeTime: "23:00", isClosed: false },
    ],
    chargingSlots: [
      {
        id: 6,
        slotName: "Slot C1",
        connectorType: "Type 2",
        powerOutput: 11,
        pricePerHour: 25000,
        status: "Available",
      },
      {
        id: 7,
        slotName: "Slot C2",
        connectorType: "Type 2",
        powerOutput: 22,
        pricePerHour: 35000,
        status: "Occupied",
      },
      {
        id: 8,
        slotName: "Slot C3",
        connectorType: "CCS2",
        powerOutput: 50,
        pricePerHour: 55000,
        status: "Available",
      },
      {
        id: 9,
        slotName: "Slot C4",
        connectorType: "CHAdeMO",
        powerOutput: 50,
        pricePerHour: 55000,
        status: "Maintenance",
      },
    ],
  },
  {
    id: 4,
    name: "PowerHub Long Biên",
    address: "27 Nguyễn Văn Cừ, Phường Ngọc Lâm, Hà Nội",
    description:
      "Trạm sạc công suất cao, hỗ trợ sạc nhanh cho các dòng xe cao cấp.",
    latitude: 21.0455,
    longitude: 105.8728,
    operationalStatus: "Open",
    approvalStatus: "Approved",
    images: [
      {
        id: 4,
        imageUrl:
          "https://images.unsplash.com/photo-1617886903355-9354e07a7500?w=400",
      },
    ],
    operatingHours: [
      { dayOfWeek: 0, openTime: "06:00", closeTime: "22:00", isClosed: false },
      { dayOfWeek: 1, openTime: "06:00", closeTime: "22:00", isClosed: false },
      { dayOfWeek: 2, openTime: "06:00", closeTime: "22:00", isClosed: false },
      { dayOfWeek: 3, openTime: "06:00", closeTime: "22:00", isClosed: false },
      { dayOfWeek: 4, openTime: "06:00", closeTime: "22:00", isClosed: false },
      { dayOfWeek: 5, openTime: "06:00", closeTime: "22:00", isClosed: false },
      { dayOfWeek: 6, openTime: "00:00", closeTime: "00:00", isClosed: true },
    ],
    chargingSlots: [
      {
        id: 10,
        slotName: "Slot D1",
        connectorType: "CCS2",
        powerOutput: 150,
        pricePerHour: 95000,
        status: "Available",
      },
      {
        id: 11,
        slotName: "Slot D2",
        connectorType: "CCS2",
        powerOutput: 150,
        pricePerHour: 95000,
        status: "Available",
      },
    ],
  },
  {
    id: 5,
    name: "SunCharge Nam Từ Liêm",
    address: "72 Phạm Hùng, Phường Mỹ Đình 1, Hà Nội",
    description:
      "Trạm sạc phù hợp cho taxi điện và xe công nghệ.",
    latitude: 21.0285,
    longitude: 105.768,
    operationalStatus: "Maintenance",
    approvalStatus: "Approved",
    images: [
      {
        id: 5,
        imageUrl:
          "https://images.unsplash.com/photo-1606229365485-93a3b8ee0385?w=400",
      },
    ],
    operatingHours: [
      { dayOfWeek: 0, openTime: "00:00", closeTime: "23:59", isClosed: false },
      { dayOfWeek: 1, openTime: "00:00", closeTime: "23:59", isClosed: false },
      { dayOfWeek: 2, openTime: "00:00", closeTime: "23:59", isClosed: false },
      { dayOfWeek: 3, openTime: "00:00", closeTime: "23:59", isClosed: false },
      { dayOfWeek: 4, openTime: "00:00", closeTime: "23:59", isClosed: false },
      { dayOfWeek: 5, openTime: "00:00", closeTime: "23:59", isClosed: false },
      { dayOfWeek: 6, openTime: "00:00", closeTime: "23:59", isClosed: false },
    ],
    chargingSlots: [
      {
        id: 12,
        slotName: "Slot E1",
        connectorType: "Type 2",
        powerOutput: 22,
        pricePerHour: 35000,
        status: "Maintenance",
      },
      {
        id: 13,
        slotName: "Slot E2",
        connectorType: "CCS2",
        powerOutput: 50,
        pricePerHour: 55000,
        status: "Maintenance",
      },
      {
        id: 14,
        slotName: "Slot E3",
        connectorType: "Type 2",
        powerOutput: 11,
        pricePerHour: 25000,
        status: "Maintenance",
      },
    ],
  },
];