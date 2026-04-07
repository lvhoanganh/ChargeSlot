using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.DTOs.Admin;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChargeSlot.Tests.Helpers
{
    public static class TestDbHelper
    {
        public static ChargeSlotDbContext CreateInMemoryDb(string? dbName = null)
        {
            dbName ??= Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<ChargeSlotDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new ChargeSlotDbContext(options);
        }

        public static async Task SeedSystemWalletsAsync(ChargeSlotDbContext db)
        {
            db.Wallets.AddRange(
                new Wallet { Id = 901, SystemCode = "ESCROW", WalletType = WalletType.System, AvailableBalance = 1000000, FrozenBalance = 0, CreatedAt = DateTime.Now },
                new Wallet { Id = 902, SystemCode = "PLATFORM_REVENUE", WalletType = WalletType.System, AvailableBalance = 0, FrozenBalance = 0, CreatedAt = DateTime.Now },
                new Wallet { Id = 903, SystemCode = "CLEARING", WalletType = WalletType.System, AvailableBalance = 0, FrozenBalance = 0, CreatedAt = DateTime.Now }
            );
            await db.SaveChangesAsync();
        }

        public static async Task<(ApplicationUser user, Driver driver)> SeedDriverAsync(
            ChargeSlotDbContext db, int userId = 1, string phone = "0912345678", string status = "ACTIVE", decimal walletBalance = 5_000_000)
        {
            var user = new ApplicationUser { Id = userId, UserName = phone, PhoneNumber = phone, FullName = $"Driver_{userId}", Email = $"driver{userId}@test.com", EmailConfirmed = true, Status = status, CreatedAt = DateTime.Now, };
            var driver = new Driver { UserId = userId, User = user };
            var wallet = new Wallet { WalletType = WalletType.Driver, UserId = userId, AvailableBalance = walletBalance, CreatedAt = DateTime.Now };
            db.Users.Add(user);
            db.Driver.Add(driver);
            db.Wallets.Add(wallet);
            await db.SaveChangesAsync();
            return (user, driver);
        }

        public static async Task<(ApplicationUser user, Owner owner)> SeedOwnerAsync(
            ChargeSlotDbContext db, int userId = 2, string phone = "0987654321", string status = "ACTIVE", decimal walletBalance = 2_000_000)
        {
            var user = new ApplicationUser { Id = userId, UserName = phone, PhoneNumber = phone, FullName = $"Owner_{userId}", Email = $"owner{userId}@test.com", EmailConfirmed = true, Status = status, CreatedAt = DateTime.Now, };
            var owner = new Owner { UserId = userId, User = user, BusinessName = "Mock Business", TaxCode = "N/A" };
            var wallet = new Wallet { WalletType = WalletType.Owner, UserId = userId, AvailableBalance = walletBalance, CreatedAt = DateTime.Now };
            db.Users.Add(user);
            db.Owner.Add(owner);
            db.Wallets.Add(wallet);
            await db.SaveChangesAsync();
            return (user, owner);
        }

        /// <summary>
        /// Tạo StartTime rơi đúng block 30 phút (xx:00 hoặc xx:30).
        /// </summary>
        public static DateTime NextAlignedStartTime(int addHours = 2)
        {
            var now = DateTime.Now.AddHours(addHours);
            // Đưa về đầu giờ :00 nếu minute < 30, hoặc :30 nếu >= 30
            var minute = now.Minute < 30 ? 0 : 30;
            return new DateTime(now.Year, now.Month, now.Day, now.Hour, minute, 0);
        }

        public static async Task<(ChargingStation station, List<ChargingSlot> slots)> SeedStationWithSlotsAsync(
            ChargeSlotDbContext db, int ownerId, int slotCount = 3, OperationalStatus operationalStatus = OperationalStatus.Active)
        {
            var station = new ChargingStation
            {
                Id = 1,
                OwnerUserId = ownerId,
                Name = "Test Station",
                Address = "123 Test St",
                Latitude = 10.0m,
                Longitude = 106.0m,
                ApprovalStatus = ApprovalStatus.Approved,
                OperationalStatus = operationalStatus,
                CreatedAt = DateTime.Now
            };

            var slots = new List<ChargingSlot>();
            for (int i = 1; i <= slotCount; i++)
            {
                slots.Add(new ChargingSlot
                {
                    Id = 100 + i,
                    StationId = station.Id,
                    ChargingStation = station,
                    SlotName = $"Slot {i}",
                    Status = SlotStatus.Active,
                    CreatedAt = DateTime.Now
                });
            }

            db.ChargingStations.Add(station);
            db.ChargingSlots.AddRange(slots);
            await db.SaveChangesAsync();

            return (station, slots);
        }

        public static async Task<Booking> SeedBookingAsync(ChargeSlotDbContext db, int driverId, int stationOwnerId, int bookingId = 1, BookingStatus status = BookingStatus.WaitingOwner)
        {
            var (station, slots) = await SeedStationWithSlotsAsync(db, stationOwnerId, 1);
            var startTime = NextAlignedStartTime(2);
            var booking = new Booking
            {
                Id = bookingId, DriverUserId = driverId,
                SlotId = slots[0].Id, ChargingSlot = slots[0],
                Status = status, StartTime = startTime, EndTime = startTime.AddHours(1),
                DurationHours = 1,
                TotalAmount = 100_000, CreatedAt = DateTime.Now
            };
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();
            return booking;
        }

        public static UpdateSystemConfigsDto GetDefaultConfigs()
        {
            return new UpdateSystemConfigsDto
            {
                RefundPolicy100_Hrs = 24,
                RefundPolicy50_Hrs = 2,
                Payment_Expiry_Minutes = 30,
                CheckIn_Window_Minutes = 15,
                NoShow_Grace_Minutes = 30,
                Slot_Buffer_Minutes = 15,
                VAT_Rate = 0.08m,
                Platform_Fee_Rate = 0.05m,
                Loyalty_Earn_Rate = 0.05m,
                Dispute_Limit_Per_Month = 5,
                Dispute_OwnerEvidence_Hours = 48,
                Dispute_AdminReview_Hours = 72,
                Ban_Duration_Days_Permanent = 365,
                Ban_Duration_Days_FirstOffense = 30,
                OTP_Expiry_Minutes = 5,
                SecondaryPassword = "Mock"
            };
        }
    }
}
