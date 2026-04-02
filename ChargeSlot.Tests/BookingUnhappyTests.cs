using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Booking;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Implementation;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChargeSlot.Tests
{
    public class BookingUnhappyTests
    {
        private readonly ChargeSlotDbContext _dbContext;
        private readonly BookingService _bookingService;

        public BookingUnhappyTests()
        {
            var options = new DbContextOptionsBuilder<ChargeSlotDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new ChargeSlotDbContext(options);

            var bookingRepo = new BookingRepository(_dbContext);
            var slotRepo = new ChargingSlotRepository(_dbContext);
            var walletRepo = new WalletRepository(_dbContext);
            
            var mockNotifications = new Mock<INotificationService>();
            var mockLogger = new Mock<ILogger<BookingService>>();
            var mockConfigService = new Mock<ISystemConfigService>();

            // Setup mock config return defaults
            mockConfigService.Setup(x => x.GetCurrentConfigsAsync())
                .ReturnsAsync(new ChargeSlot.Api.DTOs.Admin.UpdateSystemConfigsDto
                {
                    NoShow_Grace_Minutes = 30,
                    Payment_Expiry_Minutes = 30,
                    RefundPolicy100_Hrs = 2,
                    RefundPolicy50_Hrs = 1,
                    VAT_Rate = 0.08m,
                    Platform_Fee_Rate = 0.05m,
                    Loyalty_Earn_Rate = 0.05m
                });

            _bookingService = new BookingService(
                bookingRepo,
                slotRepo,
                mockNotifications.Object,
                walletRepo,
                _dbContext,
                mockLogger.Object,
                mockConfigService.Object
            );
        }

        [Fact]
        public async Task CancelBooking_LessThan1Hour_Returns0PercentRefund()
        {
            // Arrange
            var driverId = 1;
            
            var booking = new Booking
            {
                Id = 100,
                DriverUserId = driverId,
                SlotId = 1,
                StartTime = ChargeSlot.Api.Helpers.DateTimeHelper.VietnamNow().AddMinutes(30), // Start in 30 mins
                EndTime = ChargeSlot.Api.Helpers.DateTimeHelper.VietnamNow().AddHours(1.5),
                TotalAmount = 50000,
                Status = BookingStatus.Paid,
                Refund100DeadlineAt = ChargeSlot.Api.Helpers.DateTimeHelper.VietnamNow().AddHours(-1.5), // Past
                Refund50DeadlineAt = ChargeSlot.Api.Helpers.DateTimeHelper.VietnamNow().AddMinutes(-30), // Past
                CreatedAt = ChargeSlot.Api.Helpers.DateTimeHelper.VietnamNow().AddHours(-5)
            };

            // Seed DB
            var appUser = new ApplicationUser { Id = driverId, FullName = "Test Driver", Email = "test@test.com", UserName = "test", PhoneNumber = "123456789" };
            var driver = new Driver { UserId = driverId, LoyaltyPoints = 0 };
            
            _dbContext.Users.Add(appUser);
            _dbContext.Driver.Add(driver);
            _dbContext.Bookings.Add(booking);
            _dbContext.ChargingSlots.Add(new ChargingSlot { Id = 1, SlotName = "A1", ChargingStation = new ChargingStation { Name = "Station 1", Address = "Ha Noi", Latitude = 21, Longitude = 105, OwnerUserId = 2 } });
            _dbContext.Wallets.Add(new Wallet { Id = 1, SystemCode = "ESCROW", AvailableBalance = 100000 });
            _dbContext.Wallets.Add(new Wallet { Id = 2, SystemCode = "PLATFORM_REVENUE", AvailableBalance = 0 });
            _dbContext.Wallets.Add(new Wallet { Id = 3, UserId = 2, WalletType = WalletType.Owner, AvailableBalance = 0 });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _bookingService.DriverCancelBookingAsync(driverId, booking.Id, "Cancel close to start time");

            // Assert
            Assert.Equal(BookingStatus.Cancelled.ToString(), result.Status);
            
            // ESCROW should pay Owner because 0% refund to driver
            var ownerWallet = await _dbContext.Wallets.FirstAsync(w => w.UserId == 2);
            var expectedOwnerNet = 50000 - Math.Round(50000 * 0.08m, 0) - Math.Round(50000 * 0.05m, 0); // 50k - 4k - 2.5k = 43.5k
            
            Assert.Equal(expectedOwnerNet, ownerWallet.AvailableBalance);
        }

        [Fact]
        public async Task CancelBooking_Over2Hours_Returns100PercentRefund_AndPoints()
        {
            // Arrange
            var driverId = 1;
            var now = ChargeSlot.Api.Helpers.DateTimeHelper.VietnamNow();
            
            var booking = new Booking
            {
                Id = 101,
                DriverUserId = driverId,
                SlotId = 2,
                StartTime = now.AddHours(3),
                EndTime = now.AddHours(4),
                TotalAmount = 100000,
                PointsUsed = 5000,
                Status = BookingStatus.Paid,
                Refund100DeadlineAt = now.AddHours(1), // Still valid
                Refund50DeadlineAt = now.AddHours(2), // Still valid
                CreatedAt = now.AddHours(-1)
            };

            var appUser = new ApplicationUser { Id = driverId, FullName = "Driver 1", Email = "driver@email.com", UserName = "driver1", PhoneNumber = "123456789" };
            var driver = new Driver { UserId = driverId, LoyaltyPoints = 1000 };

            _dbContext.Users.Add(appUser);
            _dbContext.Driver.Add(driver);
            _dbContext.Bookings.Add(booking);
            _dbContext.ChargingSlots.Add(new ChargingSlot { Id = 2, SlotName = "A2", ChargingStation = new ChargingStation { Name = "Station 2", Address = "Da Nang", Latitude = 16, Longitude = 108, OwnerUserId = 2 } });
            _dbContext.Wallets.Add(new Wallet { Id = 4, SystemCode = "ESCROW", AvailableBalance = 100000, FrozenBalance = 0 });
            _dbContext.Wallets.Add(new Wallet { Id = 5, UserId = driverId, WalletType = WalletType.Driver, AvailableBalance = 0 });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _bookingService.DriverCancelBookingAsync(driverId, booking.Id, "Cancel early");

            // Assert
            Assert.Equal(BookingStatus.Cancelled.ToString(), result.Status);
            
            // Driver wallet receives 100k
            var driverWallet = await _dbContext.Wallets.FirstAsync(w => w.UserId == driverId);
            Assert.Equal(100000, driverWallet.AvailableBalance);
            
            // Driver points restored
            var updatedDriver = await _dbContext.Driver.FirstAsync(d => d.UserId == driverId);
            Assert.Equal(1000 + 5000, updatedDriver.LoyaltyPoints); // Restored 5000 points
        }
    }
}
