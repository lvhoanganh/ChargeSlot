using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Implementation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChargeSlot.Tests.Services
{
    public class ChargingStationServiceTests
    {
        private readonly Mock<IChargingStationRepository> _repo = new();
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly ChargeSlotDbContext _context;

        private readonly ChargingStationService _service;

        public ChargingStationServiceTests()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            _userManager = new Mock<UserManager<ApplicationUser>>(
                store.Object, null, null, null, null, null, null, null, null);

            var options = new DbContextOptionsBuilder<ChargeSlotDbContext>()
                .UseInMemoryDatabase("test_db")
                .Options;

            _context = new ChargeSlotDbContext(options);

            _service = new ChargingStationService(
                _repo.Object,
                _context,
                _userManager.Object);
        }

        // =========================
        // CREATE
        // =========================

        [Fact]
        public async Task Create_ShouldAutoCreateOwner_WhenNotExist()
        {
            var user = new ApplicationUser {
                Id = 1,
                FullName = "Trung",
                UserName = "trung",
                NormalizedUserName = "TRUNG",
                PhoneNumber = "0123456789",
                Email = "trung@gmail.com",
                NormalizedEmail = "TRUNG@GMAIL.COM"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var dto = new CreateChargingStationDto
            {
                Name = "Station A",
                Address = "HN",
                Latitude = 1,
                Longitude = 1
            };

            var result = await _service.CreateAsync(1, dto);

            Assert.NotNull(result);

            var owner = await _context.Owner.FirstOrDefaultAsync(o => o.UserId == 1);
            Assert.NotNull(owner);
        }

        // =========================
        // UPDATE
        // =========================

        [Fact]
        public async Task Update_ShouldFail_WhenNotOwner()
        {
            var station = new ChargingStation
            {
                Id = 1,
                OwnerUserId = 99,
                ApprovalStatus = ApprovalStatus.Draft
            };

            _repo.Setup(x => x.GetByIdAsync(1, true, true))
                .ReturnsAsync(station);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.UpdateAsync(1, 1, new UpdateChargingStationDto()));
        }

        [Fact]
        public async Task Update_ShouldFail_WhenWrongStatus()
        {
            var station = new ChargingStation
            {
                Id = 1,
                OwnerUserId = 1,
                ApprovalStatus = ApprovalStatus.Approved
            };

            _repo.Setup(x => x.GetByIdAsync(1, true, true))
                .ReturnsAsync(station);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UpdateAsync(1, 1, new UpdateChargingStationDto()));
        }

        // =========================
        // SUBMIT FOR APPROVAL
        // =========================

        [Fact]
        public async Task Submit_ShouldSuccess()
        {
            var station = new ChargingStation
            {
                Id = 1,
                OwnerUserId = 1,
                ApprovalStatus = ApprovalStatus.Draft,
                Name = "A",
                Address = "HN",
                Latitude = 1,
                Longitude = 1,
                ChargingSlots = new List<ChargingSlot> { new ChargingSlot() }
            };

            _repo.Setup(x => x.GetByIdAsync(1, true, true))
                .ReturnsAsync(station);

            _userManager.Setup(x => x.GetUsersInRoleAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<ApplicationUser>
                {
                new ApplicationUser { Id = 2 }
                });

            await _service.SubmitForApprovalAsync(1, 1);

            Assert.Equal(ApprovalStatus.PendingApproval, station.ApprovalStatus);
        }

        [Fact]
        public async Task Submit_ShouldFail_WhenInvalidData()
        {
            var station = new ChargingStation
            {
                Id = 1,
                OwnerUserId = 1,
                ApprovalStatus = ApprovalStatus.Draft
            };

            _repo.Setup(x => x.GetByIdAsync(1, true, true))
                .ReturnsAsync(station);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.SubmitForApprovalAsync(1, 1));
        }

        // =========================
        // REVIEW (ADMIN)
        // =========================

        [Fact]
        public async Task Review_ShouldApprove()
        {
            var station = new ChargingStation
            {
                Id = 1,
                OwnerUserId = 1,
                ApprovalStatus = ApprovalStatus.PendingApproval,
                Name = "A"
            };

            _repo.Setup(x => x.GetByIdAsync(
     It.IsAny<int>(),
     It.IsAny<bool>(),
     It.IsAny<bool>()))
     .ReturnsAsync(station);

            await _service.ReviewStationAsync(1, 99, new ReviewStationDto
            {
                IsApproved = true
            });

            Assert.Equal(ApprovalStatus.Approved, station.ApprovalStatus);
            Assert.Equal(OperationalStatus.Active, station.OperationalStatus);
        }

        [Fact]
        public async Task Review_ShouldReject_WhenNoReason()
        {
            var station = new ChargingStation
            {
                Id = 1,
                OwnerUserId = 1,
                ApprovalStatus = ApprovalStatus.PendingApproval
            };

            _repo.Setup(x => x.GetByIdAsync(
    It.IsAny<int>(),
    It.IsAny<bool>(),
    It.IsAny<bool>()))
    .ReturnsAsync(station);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.ReviewStationAsync(1, 99, new ReviewStationDto
                {
                    IsApproved = false,
                    AdminNote = ""
                }));
        }
    }
}
