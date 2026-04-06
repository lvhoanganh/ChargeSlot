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
            var user = new ApplicationUser { Id = userId, PhoneNumber = phone, Status = status, CreatedAt = DateTime.Now, };
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
            var user = new ApplicationUser { Id = userId, PhoneNumber = phone, Status = status, CreatedAt = DateTime.Now, };
            var owner = new Owner { UserId = userId, User = user };
            var wallet = new Wallet { WalletType = WalletType.Owner, UserId = userId, AvailableBalance = walletBalance, CreatedAt = DateTime.Now };
            db.Users.Add(user);
            db.Owner.Add(owner);
            db.Wallets.Add(wallet);
            await db.SaveChangesAsync();
            return (user, owner);
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
                    SlotName = "Slot 1",
                    
                    
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
            var booking = new Booking
            {
                Id = bookingId, DriverUserId = driverId,
                SlotId = slots[0].Id, ChargingSlot = slots[0],
                Status = status, StartTime = DateTime.Now.AddHours(2), EndTime = DateTime.Now.AddHours(3),
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
                
                
                
                
                
                
                
                SecondaryPassword = "Mock"
            };
        }
    }
}


