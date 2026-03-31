using ChargeSlot.Api.DTOs.Slot;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Implementation;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChargeSlot.Tests.Services
{
    public class ChargingSlotServiceTests
    {
        private readonly Mock<IChargingSlotRepository> _slotRepoMock;
        private readonly Mock<IChargingStationRepository> _stationRepoMock;
        private readonly ChargingSlotService _service;

        public ChargingSlotServiceTests()
        {
            _slotRepoMock = new Mock<IChargingSlotRepository>();
            _stationRepoMock = new Mock<IChargingStationRepository>();

            _service = new ChargingSlotService(
                _slotRepoMock.Object,
                _stationRepoMock.Object);
        }

        // =========================
        // CREATE
        // =========================
        [Fact]
        public async Task Create_ShouldSuccess_WhenValid()
        {
            var station = new ChargingStation
            {
                Id = 1,
                OwnerUserId = 10,
                ApprovalStatus = ApprovalStatus.Draft
            };

            _stationRepoMock
    .Setup(x => x.GetByIdAsync(
        It.IsAny<int>(),
        It.IsAny<bool>(),
        It.IsAny<bool>()))
    .ReturnsAsync(station);

            var dto = new CreateChargingSlotDto
            {
                SlotName = "Slot A",
                BasePricePerHour = 100
            };

            var result = await _service.CreateAsync(1, 10, dto);

            Assert.Equal("Slot A", result.SlotName);
            _slotRepoMock.Verify(x => x.AddAsync(It.IsAny<ChargingSlot>()), Times.Once);
        }

        // =========================
        // UPDATE
        // =========================
        [Fact]
        public async Task Update_ShouldSuccess_WhenValid()
        {
            var station = new ChargingStation
            {
                Id = 1,
                OwnerUserId = 10,
                ApprovalStatus = ApprovalStatus.Draft
            };

            var slot = new ChargingSlot
            {
                Id = 1,
                StationId = 1
            };

            _stationRepoMock
    .Setup(x => x.GetByIdAsync(
        It.IsAny<int>(),
        It.IsAny<bool>(),
        It.IsAny<bool>()))
    .ReturnsAsync(station);

            _slotRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), true))
                .ReturnsAsync(slot);

            var dto = new UpdateChargingSlotDto
            {
                SlotName = "Updated"
            };

            await _service.UpdateAsync(1, 1, 10, dto);

            Assert.Equal("Updated", slot.SlotName);
            _slotRepoMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        // =========================
        // DELETE
        // =========================
        [Fact]
        public async Task Delete_ShouldSuccess_WhenValid()
        {
            var station = new ChargingStation
            {
                Id = 1,
                OwnerUserId = 10,
                ApprovalStatus = ApprovalStatus.Draft
            };

            var slot = new ChargingSlot
            {
                Id = 1,
                StationId = 1
            };

            _stationRepoMock
     .Setup(x => x.GetByIdAsync(
         It.IsAny<int>(),
         It.IsAny<bool>(),
         It.IsAny<bool>()))
     .ReturnsAsync(station);

            _slotRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), true))
                .ReturnsAsync(slot);

            await _service.DeleteAsync(1, 1, 10);

            _slotRepoMock.Verify(x => x.Remove(It.IsAny<ChargingSlot>()), Times.Once);
        }

        // =========================
        // UPDATE STATUS
        // =========================
        [Fact]
        public async Task UpdateStatus_ShouldSuccess()
        {
            var station = new ChargingStation
            {
                Id = 1,
                OwnerUserId = 10,
                ApprovalStatus = ApprovalStatus.Approved
            };

            var slot = new ChargingSlot
            {
                Id = 1,
                StationId = 1,
                Status = SlotStatus.Inactive
            };

            _stationRepoMock
    .Setup(x => x.GetByIdAsync(
        It.IsAny<int>(),
        It.IsAny<bool>(),
        It.IsAny<bool>()))
    .ReturnsAsync(station);

            _slotRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), true))
                .ReturnsAsync(slot);

            var dto = new UpdateSlotStatusDto
            {
                Status = SlotStatus.Active
            };

            await _service.UpdateStatusAsync(1, 1, 10, dto);

            Assert.Equal(SlotStatus.Active, slot.Status);
        }

        // =========================
        // FAIL CASES
        // =========================

        [Fact]
        public async Task UpdateStatus_ShouldFail_WhenNotOwner()
        {
            var station = new ChargingStation
            {
                OwnerUserId = 99
            };

            _stationRepoMock
     .Setup(x => x.GetByIdAsync(
         It.IsAny<int>(),
         It.IsAny<bool>(),
         It.IsAny<bool>()))
     .ReturnsAsync(station);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.UpdateStatusAsync(1, 1, 10, new UpdateSlotStatusDto()));
        }

        [Fact]
        public async Task UpdateStatus_ShouldFail_WhenStationNotApproved()
        {
            var station = new ChargingStation
            {
                OwnerUserId = 10,
                ApprovalStatus = ApprovalStatus.Draft
            };

            _stationRepoMock
        .Setup(x => x.GetByIdAsync(
            It.IsAny<int>(),
            It.IsAny<bool>(),
            It.IsAny<bool>()))
        .ReturnsAsync(station);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UpdateStatusAsync(1, 1, 10, new UpdateSlotStatusDto()));
        }

        [Fact]
        public async Task UpdateStatus_ShouldFail_WhenSetBooked()
        {
            var station = new ChargingStation
            {
                OwnerUserId = 10,
                ApprovalStatus = ApprovalStatus.Approved
            };

            _stationRepoMock
    .Setup(x => x.GetByIdAsync(
        It.IsAny<int>(),
        It.IsAny<bool>(),
        It.IsAny<bool>()))
    .ReturnsAsync(station);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UpdateStatusAsync(1, 1, 10,
                    new UpdateSlotStatusDto { Status = SlotStatus.Booked }));
        }
    }
}
