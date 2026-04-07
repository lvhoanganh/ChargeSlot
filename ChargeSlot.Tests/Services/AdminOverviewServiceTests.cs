using System;
using System.Linq;
using System.Threading.Tasks;
using ChargeSlot.Api.DTOs.Admin.Overview;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Tests.Helpers;
using Xunit;
using Moq;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace ChargeSlot.Tests.Services
{
    public class AdminOverviewServiceTests
    {
        [Fact]
        public async Task BookingService_GetAdminAllBookingsAsync_ReturnsFilteredResult()
        {
            var db = TestDbHelper.CreateInMemoryDb();
            var (user1, driver) = await TestDbHelper.SeedDriverAsync(db);
            var (user2, owner) = await TestDbHelper.SeedOwnerAsync(db);
            var (station, slots) = await TestDbHelper.SeedStationWithSlotsAsync(db, owner.UserId, 1);

            var booking = new ChargeSlot.Api.Models.Booking
            {
                DriverUserId = driver.UserId,
                SlotId = slots[0].Id,
                Status = BookingStatus.Paid,
                StartTime = DateTime.UtcNow.AddHours(1),
                EndTime = DateTime.UtcNow.AddHours(2),
                CreatedAt = DateTime.UtcNow
            };
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();

            var service = new BookingService(
                new Mock<IBookingRepository>().Object,
                new Mock<IChargingSlotRepository>().Object,
                new Mock<INotificationService>().Object,
                new Mock<IWalletRepository>().Object,
                db,
                new Mock<ILogger<BookingService>>().Object,
                new Mock<ISystemConfigService>().Object
            );

            var filter = new BookingFilterDto { Status = "Paid", DriverUserId = driver.UserId };
            var result = await service.GetAdminAllBookingsAsync(filter);

            Assert.NotNull(result);
            Assert.Equal(1, result.TotalCount);
            Assert.Single(result.Items);
            Assert.Equal(BookingStatus.Paid.ToString(), result.Items[0].Status);
        }

        [Fact]
        public async Task ChargingSessionService_GetAdminAllSessionsAsync_ReturnsFilteredResult()
        {
            var db = TestDbHelper.CreateInMemoryDb();
            var service = new ChargingSessionService(
                new Mock<IChargingSessionRepository>().Object,
                new Mock<IInvoiceRepository>().Object,
                new Mock<IBookingRepository>().Object,
                new Mock<IChargingSlotRepository>().Object,
                new Mock<IWalletRepository>().Object,
                new Mock<INotificationService>().Object,
                db,
                new Mock<ISystemConfigService>().Object
            );

            var slot = new ChargeSlot.Api.Models.ChargingSlot { Id = 1, StationId = 1, SlotName = "S1", Status = SlotStatus.Active, CreatedAt = DateTime.UtcNow };
            var station = new ChargeSlot.Api.Models.ChargingStation { Id = 1, OwnerUserId = 99, Name = "Test", Address = "Addr", Latitude = 10m, Longitude = 106m, ApprovalStatus = ApprovalStatus.Approved, CreatedAt = DateTime.UtcNow };
            db.ChargingStations.Add(station);
            db.ChargingSlots.Add(slot);

            var booking = new ChargeSlot.Api.Models.Booking
            {
                DriverUserId = 1,
                SlotId = slot.Id,
                Status = BookingStatus.Completed,
                StartTime = DateTime.UtcNow.AddHours(-2),
                EndTime = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow
            };
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();

            var session = new ChargeSlot.Api.Models.ChargingSession
            {
                BookingId = booking.Id,
                ActualStartTime = DateTime.UtcNow.AddHours(-2),
                ActualEndTime = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow
            };
            db.ChargingSessions.Add(session);
            await db.SaveChangesAsync();

            var filter = new SessionFilterDto();
            var result = await service.GetAdminAllSessionsAsync(filter);

            Assert.NotNull(result);
            Assert.True(result.TotalCount >= 1);
        }

        [Fact]
        public async Task WalletService_GetAdminAllWalletsAsync_And_Transactions_ReturnsFilteredResult()
        {
            var db = TestDbHelper.CreateInMemoryDb();
            var service = new WalletService(
                new Mock<IWalletRepository>().Object,
                new Mock<IBookingRepository>().Object,
                new Mock<IPaymentRepository>().Object,
                new Mock<IChargingSlotRepository>().Object,
                new Mock<INotificationService>().Object,
                new Mock<IFileStorageService>().Object,
                db,
                new Mock<Microsoft.AspNetCore.Identity.UserManager<ChargeSlot.Api.Models.Identity.ApplicationUser>>(
                    new Mock<Microsoft.AspNetCore.Identity.IUserStore<ChargeSlot.Api.Models.Identity.ApplicationUser>>().Object, null, null, null, null, null, null, null, null
                ).Object,
                new Mock<Microsoft.Extensions.Configuration.IConfiguration>().Object,
                new Mock<ISystemConfigService>().Object
            );

            var wallet = new ChargeSlot.Api.Models.Wallet
            {
                UserId = 999,
                WalletType = WalletType.Driver,
                AvailableBalance = 100000,
                CreatedAt = DateTime.UtcNow
            };
            db.Wallets.Add(wallet);
            await db.SaveChangesAsync();

            var ledgerTx = new ChargeSlot.Api.Models.LedgerTransaction
            {
                ReferenceType = "TestDebit",
                ReferenceId = 1,
                CreatedAt = DateTime.UtcNow
            };
            db.LedgerTransactions.Add(ledgerTx);
            await db.SaveChangesAsync();

            var entry = new ChargeSlot.Api.Models.LedgerEntry
            {
                LedgerTransactionId = ledgerTx.Id,
                WalletId = wallet.Id,
                Amount = 50000,
                Direction = LedgerDirection.Debit,
                CreatedAt = DateTime.UtcNow
            };
            db.LedgerEntries.Add(entry);
            await db.SaveChangesAsync();

            var filterWallet = new WalletFilterDto { WalletType = "Driver", UserId = 999 };
            var walletsResult = await service.GetAdminAllWalletsAsync(filterWallet);

            Assert.NotNull(walletsResult);
            Assert.Equal(1, walletsResult.TotalCount);
            Assert.Equal(100000, walletsResult.Items[0].AvailableBalance);

            var filterTx = new TransactionFilterDto { TransactionType = "Debit" };
            var txResult = await service.GetAdminWalletTransactionsAsync(wallet.Id, filterTx);

            Assert.NotNull(txResult);
            Assert.Equal(1, txResult.TotalCount);
            Assert.Equal(50000, txResult.Items[0].Amount);
            Assert.Equal("Debit", txResult.Items[0].Direction);
        }
    }
}
