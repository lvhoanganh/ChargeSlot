using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChargeSlot.Tests.Services
{
    public class WalletServiceTests
    {
        private readonly Mock<IWalletRepository> _walletRepo = new();
        private readonly Mock<IBookingRepository> _bookingRepo = new();
        private readonly Mock<IPaymentRepository> _paymentRepo = new();
        private readonly Mock<IChargingSlotRepository> _slotRepo = new();
        private readonly Mock<IVnPayService> _vnPay = new();
        private readonly Mock<INotificationService> _noti = new();

        private readonly WalletService _service;

        public WalletServiceTests()
        {
            _service = new WalletService(
                _walletRepo.Object,
                _bookingRepo.Object,
                _paymentRepo.Object,
                _slotRepo.Object,
                _vnPay.Object,
                _noti.Object);
        }

        // =========================
        // PAY BOOKING
        // =========================

        [Fact]
        public async Task PayBooking_ShouldSuccess()
        {
            var wallet = new Wallet
            {
                Id = 1,
                UserId = 10,
                AvailableBalance = 500
            };

            var booking = new Booking
            {
                Id = 1,
                DriverUserId = 10,
                Status = BookingStatus.PendingPayment,
                TotalAmount = 200,
                SlotId = 5
            };

            var slot = new ChargingSlot { Id = 5 };

            _walletRepo.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(wallet);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _paymentRepo.Setup(x => x.GetByBookingIdAsync(1)).ReturnsAsync((Payment?)null);
            _slotRepo.Setup(x => x.GetByIdAsync(5, true)).ReturnsAsync(slot);

            var result = await _service.PayBookingByWalletAsync(10, 1);

            Assert.Equal(300, wallet.AvailableBalance);
            Assert.Equal(BookingStatus.Paid, booking.Status);

            _walletRepo.Verify(x => x.UpdateAsync(wallet), Times.Once);
            _bookingRepo.Verify(x => x.UpdateAsync(booking), Times.Once);
            _noti.Verify(x => x.SendAsync(
                10,
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationType.Payment), Times.Once);
        }

        [Fact]
        public async Task PayBooking_ShouldFail_WhenNotEnoughBalance()
        {
            var wallet = new Wallet { UserId = 10, AvailableBalance = 100 };
            var booking = new Booking
            {
                DriverUserId = 10,
                Status = BookingStatus.PendingPayment,
                TotalAmount = 200
            };

            _walletRepo.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(wallet);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.PayBookingByWalletAsync(10, 1));
        }

        // =========================
        // WITHDRAW
        // =========================

        [Fact]
        public async Task Withdraw_ShouldSuccess()
        {
            var wallet = new Wallet
            {
                Id = 1,
                UserId = 10,
                AvailableBalance = 500,
                FrozenBalance = 0
            };

            _walletRepo.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(wallet);

            var result = await _service.WithdrawAsync(10, 200);

            Assert.Equal(300, wallet.AvailableBalance);
            Assert.Equal(200, wallet.FrozenBalance);

            _walletRepo.Verify(x => x.UpdateAsync(wallet), Times.Once);
            _noti.Verify(x => x.SendAsync(
                10,
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationType.System), Times.Once);
        }

        [Fact]
        public async Task Withdraw_ShouldFail_WhenNotEnoughMoney()
        {
            var wallet = new Wallet
            {
                UserId = 10,
                AvailableBalance = 100
            };

            _walletRepo.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(wallet);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.WithdrawAsync(10, 200));
        }

        // =========================
        // TOPUP CALLBACK
        // =========================

        [Fact]
        public async Task TopUpCallback_ShouldSuccess()
        {
            var wallet = new Wallet
            {
                Id = 1,
                UserId = 10,
                AvailableBalance = 100
            };

            var query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "vnp_Amount", "10000" } // 100.00 VND
        });

            _vnPay.Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((true, "00", "-1_123"));

            _walletRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(wallet);

            await _service.ProcessTopUpCallbackAsync(query);

            Assert.Equal(200, wallet.AvailableBalance);

            _walletRepo.Verify(x => x.UpdateAsync(wallet), Times.Once);
            _noti.Verify(x => x.SendAsync(
                10,
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationType.Payment), Times.Once);
        }
    }
}
