using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Seeds
{
    /// <summary>
    /// Seed dữ liệu demo cho toàn bộ flow.
    /// Gọi: await DataSeeder.SeedAsync(app.Services);
    /// </summary>
    public static class DataSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

            // ──── Kiểm tra đã seed chưa ────
            if (await db.Driver.AnyAsync()) return;

            var now = DateTime.UtcNow;

            // ══════════════════════════════════════════
            // 1. ROLES (nếu chưa có)
            // ══════════════════════════════════════════
            foreach (var role in new[] { "Admin", "Owner", "Driver" })
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole<int> { Name = role });
            }

            // ══════════════════════════════════════════
            // 2. USERS
            // ══════════════════════════════════════════

            // --- Admin ---
            var admin = new ApplicationUser
            {
                UserName = "admin",
                FullName = "Admin Hệ Thống",
                PhoneNumber = "0900000001",
                IsPhoneVerified = true,
                Status = "ACTIVE",
                CreatedAt = now
            };
            await CreateUserWithRole(userManager, admin, "Admin@123", "Admin");

            // --- Owners ---
            var owner1 = new ApplicationUser
            {
                UserName = "0911111111",
                FullName = "Trần Văn Minh",
                PhoneNumber = "0911111111",
                IsPhoneVerified = true,
                Status = "ACTIVE",
                CreatedAt = now
            };
            await CreateUserWithRole(userManager, owner1, "Owner@123", "Owner");

            var owner2 = new ApplicationUser
            {
                UserName = "0911111112",
                FullName = "Nguyễn Thị Lan",
                PhoneNumber = "0911111112",
                IsPhoneVerified = true,
                Status = "ACTIVE",
                CreatedAt = now
            };
            await CreateUserWithRole(userManager, owner2, "Owner@123", "Owner");

            // --- Drivers ---
            var driver1 = new ApplicationUser
            {
                UserName = "0922222221",
                FullName = "Lê Văn An",
                PhoneNumber = "0922222221",
                IsPhoneVerified = true,
                Status = "ACTIVE",
                CreatedAt = now
            };
            await CreateUserWithRole(userManager, driver1, "Driver@123", "Driver");

            var driver2 = new ApplicationUser
            {
                UserName = "0922222222",
                FullName = "Phạm Văn Bình",
                PhoneNumber = "0922222222",
                IsPhoneVerified = true,
                Status = "ACTIVE",
                CreatedAt = now
            };
            await CreateUserWithRole(userManager, driver2, "Driver@123", "Driver");

            var driver3 = new ApplicationUser
            {
                UserName = "0922222223",
                FullName = "Hoàng Thị Chi",
                PhoneNumber = "0922222223",
                IsPhoneVerified = true,
                Status = "ACTIVE",
                CreatedAt = now
            };
            await CreateUserWithRole(userManager, driver3, "Driver@123", "Driver");

            // ══════════════════════════════════════════
            // 3. OWNER / DRIVER PROFILES
            // ══════════════════════════════════════════

            db.Owner.AddRange(
                new Owner { UserId = owner1.Id, BusinessName = "Sạc Minh Phát", TaxCode = "0312345678", CreatedAt = now },
                new Owner { UserId = owner2.Id, BusinessName = "Sạc Lan Anh", TaxCode = "0398765432", CreatedAt = now }
            );

            db.Driver.AddRange(
                new Driver { UserId = driver1.Id, VehicleType = "Honda Air Blade", LicensePlate = "59-X1 12345", CreatedAt = now },
                new Driver { UserId = driver2.Id, VehicleType = "Yamaha NVX", LicensePlate = "59-X2 67890", CreatedAt = now },
                new Driver { UserId = driver3.Id, VehicleType = "VinFast Klara", LicensePlate = "59-X3 11111", CreatedAt = now }
            );
            await db.SaveChangesAsync();

            // ══════════════════════════════════════════
            // 4. WALLETS (drivers, owners, SYSTEM)
            // ══════════════════════════════════════════

            db.Wallets.AddRange(
                // Driver wallets
                new Wallet { UserId = driver1.Id, WalletType = WalletType.Driver, AvailableBalance = 500_000, FrozenBalance = 0, CreatedAt = now },
                new Wallet { UserId = driver2.Id, WalletType = WalletType.Driver, AvailableBalance = 300_000, FrozenBalance = 0, CreatedAt = now },
                new Wallet { UserId = driver3.Id, WalletType = WalletType.Driver, AvailableBalance = 200_000, FrozenBalance = 0, CreatedAt = now },
                // Owner wallets
                new Wallet { UserId = owner1.Id, WalletType = WalletType.Owner, AvailableBalance = 1_000_000, FrozenBalance = 0, CreatedAt = now },
                new Wallet { UserId = owner2.Id, WalletType = WalletType.Owner, AvailableBalance = 800_000, FrozenBalance = 0, CreatedAt = now }
            );
            await db.SaveChangesAsync();
            
            // System wallets (ESCROW, vv) đã được seed tự động qua HasData trong ChargeSlotDbContext

            // ══════════════════════════════════════════
            // 5. CHARGING STATIONS (3 trạm)
            // ══════════════════════════════════════════

            var station1 = new ChargingStation
            {
                OwnerUserId = owner1.Id,
                Name = "Trạm Sạc Nguyễn Trãi",
                Address = "123 Nguyễn Trãi, Phường 2, Quận 5, TP.HCM",
                Description = "Trạm sạc xe máy điện, nằm tại ngã tư Nguyễn Trãi - Trần Phú, có mái che.",
                Latitude = 10.7580m,
                Longitude = 106.6682m,
                ApprovalStatus = ApprovalStatus.Approved,
                OperationalStatus = OperationalStatus.Active,
                SubmittedAt = now.AddDays(-10),
                ReviewedAt = now.AddDays(-9),
                ReviewedByUserId = admin.Id,
                AverageRating = 4.0m,
                TotalReviews = 2,
                CreatedAt = now.AddDays(-15)
            };

            var station2 = new ChargingStation
            {
                OwnerUserId = owner1.Id,
                Name = "Trạm Sạc Lê Lợi",
                Address = "456 Lê Lợi, Phường Bến Thành, Quận 1, TP.HCM",
                Description = "Trạm sạc trung tâm, gần chợ Bến Thành, 24/7.",
                Latitude = 10.7725m,
                Longitude = 106.6980m,
                ApprovalStatus = ApprovalStatus.Approved,
                OperationalStatus = OperationalStatus.Active,
                SubmittedAt = now.AddDays(-8),
                ReviewedAt = now.AddDays(-7),
                ReviewedByUserId = admin.Id,
                AverageRating = 0m,
                TotalReviews = 0,
                CreatedAt = now.AddDays(-12)
            };

            var station3 = new ChargingStation
            {
                OwnerUserId = owner2.Id,
                Name = "Trạm Sạc Phạm Văn Đồng",
                Address = "789 Phạm Văn Đồng, Phường Hiệp Bình Chánh, TP. Thủ Đức",
                Description = "Trạm sạc rộng rãi, có chỗ đậu xe ô tô. Gần Giga Mall.",
                Latitude = 10.8396m,
                Longitude = 106.7200m,
                ApprovalStatus = ApprovalStatus.Approved,
                OperationalStatus = OperationalStatus.Active,
                SubmittedAt = now.AddDays(-6),
                ReviewedAt = now.AddDays(-5),
                ReviewedByUserId = admin.Id,
                AverageRating = 5.0m,
                TotalReviews = 1,
                CreatedAt = now.AddDays(-10)
            };

            db.ChargingStations.AddRange(station1, station2, station3);
            await db.SaveChangesAsync();

            // ══════════════════════════════════════════
            // 6. STATION IMAGES
            // ══════════════════════════════════════════

            db.StationImages.AddRange(
                new StationImage { StationId = station1.Id, ImageUrl = "/uploads/stations/demo/station1_1.jpg" },
                new StationImage { StationId = station1.Id, ImageUrl = "/uploads/stations/demo/station1_2.jpg" },
                new StationImage { StationId = station2.Id, ImageUrl = "/uploads/stations/demo/station2_1.jpg" },
                new StationImage { StationId = station3.Id, ImageUrl = "/uploads/stations/demo/station3_1.jpg" },
                new StationImage { StationId = station3.Id, ImageUrl = "/uploads/stations/demo/station3_2.jpg" }
            );

            // ══════════════════════════════════════════
            // 7. OPERATING HOURS (tất cả mở 6:00-22:00, CN nghỉ)
            // ══════════════════════════════════════════

            foreach (var sid in new[] { station1.Id, station2.Id, station3.Id })
            {
                for (byte dow = 0; dow <= 6; dow++)
                {
                    db.StationOperatingHours.Add(new StationOperatingHours
                    {
                        StationId = sid,
                        DayOfWeek = dow,
                        IsClosed = dow == 0, // Chủ nhật nghỉ
                        OpenTime = dow == 0 ? null : new TimeOnly(6, 0),
                        CloseTime = dow == 0 ? null : new TimeOnly(22, 0)
                    });
                }
            }

            // Station2 mở 24/7
            var s2Hours = db.ChangeTracker.Entries<StationOperatingHours>()
                .Where(e => e.Entity.StationId == station2.Id).ToList();
            foreach (var h in s2Hours)
            {
                h.Entity.IsClosed = false;
                h.Entity.OpenTime = new TimeOnly(0, 0);
                h.Entity.CloseTime = new TimeOnly(23, 59);
            }
            await db.SaveChangesAsync();

            // ═══════════════════════════════════════════════════════════════
            // 8. CHARGING SLOTS (QR code tokens cố định để test check-in)
            //    FE scan QR → gửi token → BE match slot → check-in
            // ═══════════════════════════════════════════════════════════════

            var slotA1 = new ChargingSlot { StationId = station1.Id, SlotName = "A1", PositionX = 20, PositionY = 30, QrCodeToken = "SLOT-A1-QR", Status = SlotStatus.Active, CreatedAt = now };
            var slotA2 = new ChargingSlot { StationId = station1.Id, SlotName = "A2", PositionX = 50, PositionY = 30, QrCodeToken = "SLOT-A2-QR", Status = SlotStatus.Active, CreatedAt = now };
            var slotA3 = new ChargingSlot { StationId = station1.Id, SlotName = "A3", PositionX = 80, PositionY = 30, QrCodeToken = "SLOT-A3-QR", Status = SlotStatus.Active, CreatedAt = now };

            var slotB1 = new ChargingSlot { StationId = station2.Id, SlotName = "B1", PositionX = 25, PositionY = 40, QrCodeToken = "SLOT-B1-QR", Status = SlotStatus.Active, CreatedAt = now };
            var slotB2 = new ChargingSlot { StationId = station2.Id, SlotName = "B2", PositionX = 75, PositionY = 40, QrCodeToken = "SLOT-B2-QR", Status = SlotStatus.Active, CreatedAt = now };

            var slotC1 = new ChargingSlot { StationId = station3.Id, SlotName = "C1", PositionX = 30, PositionY = 50, QrCodeToken = "SLOT-C1-QR", Status = SlotStatus.Active, CreatedAt = now };
            var slotC2 = new ChargingSlot { StationId = station3.Id, SlotName = "C2", PositionX = 70, PositionY = 50, QrCodeToken = "SLOT-C2-QR", Status = SlotStatus.Active, CreatedAt = now };
            var slotC3 = new ChargingSlot { StationId = station3.Id, SlotName = "C3", PositionX = 50, PositionY = 80, QrCodeToken = "SLOT-C3-QR", Status = SlotStatus.Active, CreatedAt = now };

            db.ChargingSlots.AddRange(slotA1, slotA2, slotA3, slotB1, slotB2, slotC1, slotC2, slotC3);
            await db.SaveChangesAsync();

            // ══════════════════════════════════════════
            // 9. STATION PRICING (giá theo khung giờ)
            // ══════════════════════════════════════════

            // Station 1: Sáng rẻ, chiều đắt, tối trung bình
            db.StationPricings.AddRange(
                new StationPricing { StationId = station1.Id, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(12, 0), PricePerHour = 5_000, Priority = 0, EffectiveFrom = now.AddDays(-15), IsActive = true, CreatedAt = now },
                new StationPricing { StationId = station1.Id, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(18, 0), PricePerHour = 8_000, Priority = 0, EffectiveFrom = now.AddDays(-15), IsActive = true, CreatedAt = now },
                new StationPricing { StationId = station1.Id, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(22, 0), PricePerHour = 6_000, Priority = 0, EffectiveFrom = now.AddDays(-15), IsActive = true, CreatedAt = now }
            );

            // Station 2: 24/7 giá đều
            db.StationPricings.AddRange(
                new StationPricing { StationId = station2.Id, StartTime = new TimeOnly(0, 0), EndTime = new TimeOnly(23, 59), PricePerHour = 7_000, Priority = 0, EffectiveFrom = now.AddDays(-12), IsActive = true, CreatedAt = now }
            );

            // Station 3: 3 khung, cuối tuần đắt hơn
            db.StationPricings.AddRange(
                new StationPricing { StationId = station3.Id, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(11, 0), PricePerHour = 4_000, Priority = 0, EffectiveFrom = now.AddDays(-10), IsActive = true, CreatedAt = now },
                new StationPricing { StationId = station3.Id, StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(17, 0), PricePerHour = 6_000, Priority = 0, EffectiveFrom = now.AddDays(-10), IsActive = true, CreatedAt = now },
                new StationPricing { StationId = station3.Id, StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(22, 0), PricePerHour = 5_000, Priority = 0, EffectiveFrom = now.AddDays(-10), IsActive = true, CreatedAt = now },
                // Cuối tuần giá cao hơn (Saturday = 6)
                new StationPricing { StationId = station3.Id, DayOfWeek = 6, StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(22, 0), PricePerHour = 10_000, Priority = 1, EffectiveFrom = now.AddDays(-10), IsActive = true, CreatedAt = now }
            );
            await db.SaveChangesAsync();

            // ══════════════════════════════════════════
            // 10. EXTRA SERVICES
            // ══════════════════════════════════════════

            db.ExtraServices.AddRange(
                new ExtraService { StationId = station1.Id, ServiceName = "Cho thuê củ sạc", Description = "Củ sạc USB-C 65W", Price = 10_000, TotalStock = 5, IsActive = true, CreatedAt = now },
                new ExtraService { StationId = station1.Id, ServiceName = "Bơm lốp", Description = "Bơm lốp xe máy miễn phí", Price = 0, IsActive = true, CreatedAt = now },
                new ExtraService { StationId = station3.Id, ServiceName = "Cho thuê củ sạc", Description = "Củ sạc nhanh 100W", Price = 15_000, TotalStock = 3, IsActive = true, CreatedAt = now },
                new ExtraService { StationId = station3.Id, ServiceName = "Nước uống", Description = "Nước suối miễn phí khi sạc", Price = 0, IsActive = true, CreatedAt = now }
            );
            await db.SaveChangesAsync();

            // ════════════════════════════════════════════════════════
            // 11. BOOKINGS (nhiều trạng thái khác nhau để demo full flow)
            //     ★ = booking sẵn sàng test ngay khi seed
            // ════════════════════════════════════════════════════════

            var tomorrow = now.Date.AddDays(1);

            // Booking 1: COMPLETED (Driver An → Station 1, Slot A1) — đã xong, có đánh giá
            var booking1 = new Booking
            {
                DriverUserId = driver1.Id, SlotId = slotA1.Id,
                StartTime = now.AddDays(-3).Date.AddHours(8),
                EndTime = now.AddDays(-3).Date.AddHours(10),
                DurationHours = 2, TotalAmount = 10_000,
                Status = BookingStatus.Completed,
                CreatedAt = now.AddDays(-4)
            };

            // Booking 2: COMPLETED (Driver Bình → Station 3, Slot C1) — đã xong, có đánh giá
            var booking2 = new Booking
            {
                DriverUserId = driver2.Id, SlotId = slotC1.Id,
                StartTime = now.AddDays(-2).Date.AddHours(14),
                EndTime = now.AddDays(-2).Date.AddHours(16),
                DurationHours = 2, TotalAmount = 12_000,
                Status = BookingStatus.Completed,
                CreatedAt = now.AddDays(-3)
            };

            // ★ Booking 3: PAID — SẴN SÀNG CHECK-IN NGAY!
            // (Driver An → Station 2, Slot B1)
            // StartTime = now + 5 phút → nằm trong cửa sổ ±15 phút
            // Check-in bằng QR: "SLOT-B1-QR"
            var booking3 = new Booking
            {
                DriverUserId = driver1.Id, SlotId = slotB1.Id,
                StartTime = now.AddMinutes(5),
                EndTime = now.AddHours(2).AddMinutes(5),
                DurationHours = 2, TotalAmount = 14_000,
                Status = BookingStatus.Paid,
                CreatedAt = now.AddMinutes(-30)
            };

            // ★ Booking 4: WAITING OWNER — Owner chưa accept
            // (Driver Chi → Station 1, Slot A2)
            // Test: login owner_minh → Accept/Reject
            var booking4 = new Booking
            {
                DriverUserId = driver3.Id, SlotId = slotA2.Id,
                StartTime = now.AddHours(3),
                EndTime = now.AddHours(5),
                DurationHours = 2, TotalAmount = 16_000,
                Status = BookingStatus.WaitingOwner,
                CreatedAt = now
            };

            // ★ Booking 5: PENDING PAYMENT — Chờ thanh toán 2 tiếng nữa
            // (Driver Bình → Station 3, Slot C2)
            // Test: login driver_binh → PayByWallet hoặc VNPay
            var booking5 = new Booking
            {
                DriverUserId = driver2.Id, SlotId = slotC2.Id,
                StartTime = now.AddHours(4),
                EndTime = now.AddHours(6),
                DurationHours = 2, TotalAmount = 12_000,
                Status = BookingStatus.PendingPayment,
                PaymentExpiresAt = now.AddHours(2),
                CreatedAt = now
            };

            // Booking 6: COMPLETED nhưng có Dispute (Driver Chi → Station 1, Slot A3)
            var booking6 = new Booking
            {
                DriverUserId = driver3.Id, SlotId = slotA3.Id,
                StartTime = now.AddDays(-1).Date.AddHours(10),
                EndTime = now.AddDays(-1).Date.AddHours(12),
                DurationHours = 2, TotalAmount = 16_000,
                Status = BookingStatus.Completed,
                CreatedAt = now.AddDays(-2)
            };

            // ★ Booking 7: IN PROGRESS — đang sạc, Owner có thể Stop
            // (Driver An → Station 3, Slot C3)
            // StartTime 1h trước, EndTime 1h nữa
            // Test: login owner_lan → StopCharging
            var booking7 = new Booking
            {
                DriverUserId = driver1.Id, SlotId = slotC3.Id,
                StartTime = now.AddHours(-1),
                EndTime = now.AddHours(1),
                DurationHours = 2, TotalAmount = 10_000,
                Status = BookingStatus.InProgress,
                CheckedInAt = now.AddHours(-1),
                CreatedAt = now.AddHours(-2)
            };

            // ★ Booking 8: COMPLETED nhưng CHƯA ĐÁNH GIÁ — test CreateReview
            // (Driver Bình → Station 2, Slot B2)
            // Test: login driver_binh → POST /api/reviews { bookingId: X, rating: 5, comment: "..." }
            var booking8 = new Booking
            {
                DriverUserId = driver2.Id, SlotId = slotB2.Id,
                StartTime = now.AddDays(-1).Date.AddHours(9),
                EndTime = now.AddDays(-1).Date.AddHours(11),
                DurationHours = 2, TotalAmount = 14_000,
                Status = BookingStatus.Completed,
                CheckedInAt = now.AddDays(-1).Date.AddHours(9),
                CreatedAt = now.AddDays(-2)
            };

            db.Bookings.AddRange(booking1, booking2, booking3, booking4, booking5, booking6, booking7, booking8);
            await db.SaveChangesAsync();

            // Đánh dấu slot C3 là đang bận vì booking7 đang sạc
            slotC3.Status = SlotStatus.Booked;
            await db.SaveChangesAsync();

            // ══════════════════════════════════════════
            // 12. PAYMENTS
            // ══════════════════════════════════════════

            db.Payments.AddRange(
                new Payment { BookingId = booking1.Id, Amount = booking1.TotalAmount, PaymentMethod = PaymentMethod.Wallet, Status = PaymentStatus.Completed, PaidAt = now.AddDays(-4), GatewayTxnRef = $"WALLET_{now.Ticks}", CreatedAt = now.AddDays(-4) },
                new Payment { BookingId = booking2.Id, Amount = booking2.TotalAmount, PaymentMethod = PaymentMethod.EWallet, Status = PaymentStatus.Completed, PaidAt = now.AddDays(-3), GatewayTxnRef = $"VNP_{now.Ticks}", CreatedAt = now.AddDays(-3) },
                new Payment { BookingId = booking3.Id, Amount = booking3.TotalAmount, PaymentMethod = PaymentMethod.Wallet, Status = PaymentStatus.Completed, PaidAt = now, GatewayTxnRef = $"WALLET_{now.Ticks + 1}", CreatedAt = now },
                new Payment { BookingId = booking6.Id, Amount = booking6.TotalAmount, PaymentMethod = PaymentMethod.EWallet, Status = PaymentStatus.Completed, PaidAt = now.AddDays(-2), GatewayTxnRef = $"VNP_{now.Ticks + 2}", CreatedAt = now.AddDays(-2) },
                new Payment { BookingId = booking7.Id, Amount = booking7.TotalAmount, PaymentMethod = PaymentMethod.Wallet, Status = PaymentStatus.Completed, PaidAt = now.AddHours(-2), GatewayTxnRef = $"WALLET_{now.Ticks + 3}", CreatedAt = now.AddHours(-2) },
                new Payment { BookingId = booking8.Id, Amount = booking8.TotalAmount, PaymentMethod = PaymentMethod.Wallet, Status = PaymentStatus.Completed, PaidAt = now.AddDays(-1).Date.AddHours(8), GatewayTxnRef = $"WALLET_{now.Ticks + 4}", CreatedAt = now.AddDays(-1).Date.AddHours(8) }
            );
            await db.SaveChangesAsync();

            // ══════════════════════════════════════════
            // 13. CHARGING SESSIONS
            // ══════════════════════════════════════════

            db.ChargingSessions.AddRange(
                // Booking 1: hoàn thành
                new ChargingSession { BookingId = booking1.Id, CheckinTime = booking1.StartTime, ActualStartTime = booking1.StartTime, ActualEndTime = booking1.EndTime, ActualDurationHours = 2, CreatedAt = booking1.StartTime },
                // Booking 2: hoàn thành
                new ChargingSession { BookingId = booking2.Id, CheckinTime = booking2.StartTime, ActualStartTime = booking2.StartTime, ActualEndTime = booking2.EndTime, ActualDurationHours = 2, CreatedAt = booking2.StartTime },
                // Booking 6: hoàn thành (có dispute)
                new ChargingSession { BookingId = booking6.Id, CheckinTime = booking6.StartTime, ActualStartTime = booking6.StartTime, ActualEndTime = booking6.EndTime, ActualDurationHours = 2, CreatedAt = booking6.StartTime },
                // Booking 7: đang sạc
                new ChargingSession { BookingId = booking7.Id, CheckinTime = now.AddHours(-1), ActualStartTime = now.AddHours(-1), CreatedAt = now.AddHours(-1) },
                // Booking 8: hoàn thành (chưa rating)
                new ChargingSession { BookingId = booking8.Id, CheckinTime = booking8.StartTime, ActualStartTime = booking8.StartTime, ActualEndTime = booking8.EndTime, ActualDurationHours = 2, CreatedAt = booking8.StartTime }
            );
            await db.SaveChangesAsync();

            // ══════════════════════════════════════════
            // 14. INVOICES (cho booking completed)
            // ══════════════════════════════════════════

            db.Invoices.AddRange(
                new Invoice { BookingId = booking1.Id, TotalAmount = 10_000, ChargingAmount = 8_700, VatAmount = 800, PlatformFee = 500, ServiceAmount = 0, Status = InvoiceStatus.Confirmed, CreatedAt = booking1.EndTime },
                new Invoice { BookingId = booking2.Id, TotalAmount = 12_000, ChargingAmount = 10_440, VatAmount = 960, PlatformFee = 600, ServiceAmount = 0, Status = InvoiceStatus.Confirmed, CreatedAt = booking2.EndTime },
                new Invoice { BookingId = booking6.Id, TotalAmount = 16_000, ChargingAmount = 13_920, VatAmount = 1_280, PlatformFee = 800, ServiceAmount = 0, Status = InvoiceStatus.Confirmed, CreatedAt = booking6.EndTime },
                new Invoice { BookingId = booking8.Id, TotalAmount = 14_000, ChargingAmount = 12_180, VatAmount = 1_120, PlatformFee = 700, ServiceAmount = 0, Status = InvoiceStatus.Confirmed, CreatedAt = booking8.EndTime }
            );
            await db.SaveChangesAsync();

            // ══════════════════════════════════════════
            // 15. RATINGS & REVIEWS
            // ══════════════════════════════════════════

            db.Ratings.AddRange(
                // Station 1 — 2 đánh giá (có BookingId hợp lệ)
                new Rating { BookingId = booking1.Id, StationId = station1.Id, DriverUserId = driver1.Id, Score = 5, Comment = "Trạm sạc rất sạch sẽ, mát mẻ!", OwnerReply = "Cảm ơn bạn đã ghé thăm!", OwnerRepliedAt = now.AddDays(-2), CreatedAt = now.AddDays(-3) },
                new Rating { BookingId = booking6.Id, StationId = station1.Id, DriverUserId = driver3.Id, Score = 3, Comment = "Sạc hơi chậm, nhưng chủ trạm thân thiện.", CreatedAt = now.AddDays(-1) },

                // Station 3 — 1 đánh giá (có BookingId hợp lệ)
                new Rating { BookingId = booking2.Id, StationId = station3.Id, DriverUserId = driver2.Id, Score = 5, Comment = "Trạm rộng rãi, tiện nghi, 5 sao!", OwnerReply = "Trạm luôn welcome bạn!", OwnerRepliedAt = now.AddDays(-1), CreatedAt = now.AddDays(-2) }
                // ★ Booking 8 (Station 2) CHƯA có rating → test CreateReview
            );
            await db.SaveChangesAsync();

            // ══════════════════════════════════════════
            // 16. DISPUTE (1 dispute PendingReview cho admin xử lý)
            // ══════════════════════════════════════════

            var dispute = new Dispute
            {
                BookingId = booking6.Id,
                CreatedByUserId = driver3.Id,
                Reason = "Phiên sạc bị ngắt giữa chừng, xe không đầy pin",
                Description = "Tôi đặt sạc 2 tiếng nhưng chỉ được 1.5 tiếng thì bị ngắt. Chủ trạm không giải thích.",
                Status = DisputeStatus.PendingReview,
                OwnerResponse = "Do nguồn điện khu vực bị mất tạm thời, không phải lỗi của trạm.",
                CreatedAt = now.AddHours(-12)
            };
            db.Disputes.Add(dispute);
            await db.SaveChangesAsync();

            // ══════════════════════════════════════════
            // 17. FAVORITES
            // ══════════════════════════════════════════

            db.FavoriteStations.AddRange(
                new FavoriteStation { DriverUserId = driver1.Id, StationId = station1.Id, CreatedAt = now.AddDays(-5) },
                new FavoriteStation { DriverUserId = driver1.Id, StationId = station3.Id, CreatedAt = now.AddDays(-3) },
                new FavoriteStation { DriverUserId = driver2.Id, StationId = station3.Id, CreatedAt = now.AddDays(-4) },
                new FavoriteStation { DriverUserId = driver2.Id, StationId = station2.Id, CreatedAt = now.AddDays(-2) },
                new FavoriteStation { DriverUserId = driver3.Id, StationId = station3.Id, CreatedAt = now.AddDays(-1) },
                new FavoriteStation { DriverUserId = driver3.Id, StationId = station1.Id, CreatedAt = now }
            );
            await db.SaveChangesAsync();

            // ══════════════════════════════════════════
            // 18. NOTIFICATIONS (mẫu)
            // ══════════════════════════════════════════

            db.Notifications.AddRange(
                new Notification { UserId = driver1.Id, Title = "Đặt chỗ được chấp nhận", Content = "Yêu cầu đặt chỗ tại slot B1 — trạm Sạc Lê Lợi (09:00 - 11:00) đã được chấp nhận.", Type = NotificationType.Booking, IsRead = false, CreatedAt = now },
                new Notification { UserId = owner1.Id, Title = "Khiếu nại mới từ Driver", Content = "Hoàng Thị Chi khiếu nại về phiên sạc tại trạm Trạm Sạc Nguyễn Trãi.", Type = NotificationType.Dispute, IsRead = false, CreatedAt = now.AddHours(-12) },
                new Notification { UserId = driver3.Id, Title = "Owner đã phản hồi khiếu nại", Content = "Chủ trạm Trạm Sạc Nguyễn Trãi đã nộp bằng chứng phản hồi. Chờ Admin xem xét.", Type = NotificationType.Dispute, IsRead = false, CreatedAt = now.AddHours(-6) },
                new Notification { UserId = admin.Id, Title = "Khiếu nại cần xử lý", Content = "Khiếu nại tại trạm Trạm Sạc Nguyễn Trãi đã có phản hồi từ Owner. Sẵn sàng xem xét.", Type = NotificationType.Dispute, IsRead = false, CreatedAt = now.AddHours(-6) },
                new Notification { UserId = driver2.Id, Title = "Thanh toán thành công", Content = "Thanh toán 12,000đ cho slot C2 — trạm Trạm Sạc Phạm Văn Đồng thành công.", Type = NotificationType.Payment, IsRead = true, CreatedAt = now.AddDays(-3) }
            );
            await db.SaveChangesAsync();
        }

        private static async Task CreateUserWithRole(UserManager<ApplicationUser> userManager, ApplicationUser user, string password, string role)
        {
            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}
