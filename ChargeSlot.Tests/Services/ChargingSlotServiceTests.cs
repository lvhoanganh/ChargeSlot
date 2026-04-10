using Xunit;
using Moq;
using ChargeSlot.Api.DTOs.Slot;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Implementation;

namespace ChargeSlot.Tests.Services
{
    /// <summary>
    /// Unit tests cho ChargingSlotService - quản lý slot trong trạm sạc.
    /// Slot là điều kiện tiên quyết của booking: phải Active mới đặt được.
    /// </summary>
    public class ChargingSlotServiceTests
    {
        private readonly Mock<IChargingSlotRepository>    _slotRepoMock    = new();
        private readonly Mock<IChargingStationRepository> _stationRepoMock = new();

        private readonly ChargingSlotService _service;

        public ChargingSlotServiceTests()
        {
            _service = new ChargingSlotService(
                _slotRepoMock.Object,
                _stationRepoMock.Object);
        }

        // ─────────────────────────────────────────────
        // HELPER: tạo station hợp lệ
        // ─────────────────────────────────────────────

        private static ChargingStation MakeStation(int ownerId, ApprovalStatus status) =>
            new ChargingStation { Id = 1, OwnerUserId = ownerId, ApprovalStatus = status };

        // ─────────────────────────────────────────────
        // CREATE SLOT
        // ─────────────────────────────────────────────

        /// <summary>
        /// ✅ Station Draft + đúng owner → tạo slot thành công với Status = Inactive.
        /// Slot mới không bao giờ Active ngay (phải chờ Admin duyệt station).
        /// </summary>
        [Fact]
        public async Task CreateSlot_ShouldSuccess_WhenStationIsDraft()
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(MakeStation(ownerId: 10, ApprovalStatus.Draft));

            ChargingSlot? captured = null;
            _slotRepoMock
                .Setup(x => x.AddAsync(It.IsAny<ChargingSlot>()))
                .Callback<ChargingSlot>(s => captured = s)
                .Returns(Task.CompletedTask);

            var dto = new CreateChargingSlotDto
            {
                SlotName         = "Slot A",
                ConnectorType    = "Type2",
                BasePricePerHour = 150,
                PowerKw          = 22
            };

            var result = await _service.CreateAsync(stationId: 1, ownerUserId: 10, dto);

            Assert.NotNull(result);
            Assert.Equal("Slot A", result.SlotName);
            Assert.Equal(SlotStatus.Inactive.ToString(), result.Status); // mới tạo = Inactive

            _slotRepoMock.Verify(x => x.AddAsync(It.IsAny<ChargingSlot>()), Times.Once);
            _slotRepoMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        /// <summary>
        /// ✅ Station Rejected + đúng owner → cũng được tạo slot (cần sửa để resubmit).
        /// </summary>
        [Fact]
        public async Task CreateSlot_ShouldSuccess_WhenStationIsRejected()
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(MakeStation(ownerId: 10, ApprovalStatus.Rejected));

            _slotRepoMock
                .Setup(x => x.AddAsync(It.IsAny<ChargingSlot>()))
                .Returns(Task.CompletedTask);

            var dto = new CreateChargingSlotDto { SlotName = "Slot B", ConnectorType = "CCS" };

            var result = await _service.CreateAsync(1, 10, dto);

            Assert.NotNull(result);
            _slotRepoMock.Verify(x => x.AddAsync(It.IsAny<ChargingSlot>()), Times.Once);
        }

        /// <summary>
        /// ❌ Station đã Approved → không được thêm/sửa/xóa slot.
        /// Tránh thay đổi cấu hình slot khi đang hoạt động.
        /// </summary>
        [Theory]
        [InlineData(ApprovalStatus.Approved)]
        [InlineData(ApprovalStatus.PendingApproval)]
        public async Task CreateSlot_ShouldFail_WhenStationNotEditable(ApprovalStatus status)
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(MakeStation(ownerId: 10, status));

            var dto = new CreateChargingSlotDto { SlotName = "X", ConnectorType = "Y" };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateAsync(1, 10, dto));

            _slotRepoMock.Verify(x => x.AddAsync(It.IsAny<ChargingSlot>()), Times.Never);
        }

        /// <summary>
        /// ❌ Không phải Owner của station → throw UnauthorizedAccessException.
        /// </summary>
        [Fact]
        public async Task CreateSlot_ShouldFail_WhenNotOwner()
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(MakeStation(ownerId: 99, ApprovalStatus.Draft)); // owner = 99

            var dto = new CreateChargingSlotDto { SlotName = "X", ConnectorType = "Y" };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.CreateAsync(1, ownerUserId: 10, dto)); // user = 10 ≠ 99
        }

        /// <summary>
        /// ❌ Station không tồn tại → throw KeyNotFoundException.
        /// </summary>
        [Fact]
        public async Task CreateSlot_ShouldFail_WhenStationNotFound()
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(999, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync((ChargingStation?)null);

            var dto = new CreateChargingSlotDto { SlotName = "X", ConnectorType = "Y" };

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _service.CreateAsync(999, 10, dto));
        }

        // ─────────────────────────────────────────────
        // UPDATE SLOT
        // ─────────────────────────────────────────────

        /// <summary>
        /// ✅ Update slot thành công: cập nhật đúng field, gọi SaveChanges.
        /// </summary>
        [Fact]
        public async Task UpdateSlot_ShouldSuccess_AndUpdateFields()
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(MakeStation(ownerId: 10, ApprovalStatus.Draft));

            var slot = new ChargingSlot { Id = 1, StationId = 1, SlotName = "Old", BasePricePerHour = 100 };
            _slotRepoMock
                .Setup(x => x.GetByIdAsync(1, true))
                .ReturnsAsync(slot);

            var dto = new UpdateChargingSlotDto
            {
                SlotName         = "New Name",
                ConnectorType    = "CCS2",
                BasePricePerHour = 200,
                PowerKw          = 50
            };

            await _service.UpdateAsync(stationId: 1, slotId: 1, ownerUserId: 10, dto);

            Assert.Equal("New Name", slot.SlotName);
            Assert.Equal(200, slot.BasePricePerHour);
            Assert.Equal(50, slot.PowerKw);
            Assert.NotNull(slot.UpdatedAt);

            _slotRepoMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        /// <summary>
        /// ❌ Slot không thuộc station → throw KeyNotFoundException.
        /// </summary>
        [Fact]
        public async Task UpdateSlot_ShouldFail_WhenSlotNotInStation()
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(MakeStation(ownerId: 10, ApprovalStatus.Draft));

            var slot = new ChargingSlot { Id = 99, StationId = 999 }; // khác station
            _slotRepoMock
                .Setup(x => x.GetByIdAsync(99, true))
                .ReturnsAsync(slot);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _service.UpdateAsync(stationId: 1, slotId: 99, ownerUserId: 10, new UpdateChargingSlotDto()));
        }

        // ─────────────────────────────────────────────
        // DELETE SLOT
        // ─────────────────────────────────────────────

        /// <summary>
        /// ✅ Xóa slot thành công.
        /// </summary>
        [Fact]
        public async Task DeleteSlot_ShouldSuccess()
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(MakeStation(ownerId: 10, ApprovalStatus.Draft));

            var slot = new ChargingSlot { Id = 1, StationId = 1 };
            _slotRepoMock
                .Setup(x => x.GetByIdAsync(1, true))
                .ReturnsAsync(slot);

            await _service.DeleteAsync(stationId: 1, slotId: 1, ownerUserId: 10);

            _slotRepoMock.Verify(x => x.Remove(slot), Times.Once);
            _slotRepoMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        /// <summary>
        /// ❌ Không phải owner → throw UnauthorizedAccessException.
        /// </summary>
        [Fact]
        public async Task DeleteSlot_ShouldFail_WhenNotOwner()
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(MakeStation(ownerId: 99, ApprovalStatus.Draft));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.DeleteAsync(1, 1, ownerUserId: 10));
        }

        // ─────────────────────────────────────────────
        // UPDATE SLOT STATUS
        // ─────────────────────────────────────────────

        /// <summary>
        /// ✅ Owner đổi slot Inactive → Active (station đã Approved).
        /// </summary>
        [Fact]
        public async Task UpdateSlotStatus_ShouldSuccess_InactiveToActive()
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(MakeStation(ownerId: 10, ApprovalStatus.Approved));

            var slot = new ChargingSlot { Id = 1, StationId = 1, Status = SlotStatus.Inactive };
            _slotRepoMock
                .Setup(x => x.GetByIdAsync(1, true))
                .ReturnsAsync(slot);

            await _service.UpdateStatusAsync(1, 1, 10, new UpdateSlotStatusDto { Status = SlotStatus.Active });

            Assert.Equal(SlotStatus.Active, slot.Status);
            _slotRepoMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        /// <summary>
        /// ❌ Không thể set Booked thủ công — Booked do hệ thống quản lý.
        /// </summary>
        [Fact]
        public async Task UpdateSlotStatus_ShouldFail_WhenSetBooked()
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(MakeStation(ownerId: 10, ApprovalStatus.Approved));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UpdateStatusAsync(1, 1, 10, new UpdateSlotStatusDto { Status = SlotStatus.Booked }));
        }

        /// <summary>
        /// ❌ Station chưa Approved → không cho đổi status slot.
        /// </summary>
        [Theory]
        [InlineData(ApprovalStatus.Draft)]
        [InlineData(ApprovalStatus.PendingApproval)]
        [InlineData(ApprovalStatus.Rejected)]
        public async Task UpdateSlotStatus_ShouldFail_WhenStationNotApproved(ApprovalStatus status)
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(MakeStation(ownerId: 10, status));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UpdateStatusAsync(1, 1, 10, new UpdateSlotStatusDto { Status = SlotStatus.Active }));
        }

        /// <summary>
        /// ❌ Slot đang Booked → không cho thay đổi status.
        /// Chờ booking hoàn tất hoặc expire.
        /// </summary>
        [Fact]
        public async Task UpdateSlotStatus_ShouldFail_WhenSlotCurrentlyBooked()
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(MakeStation(ownerId: 10, ApprovalStatus.Approved));

            var slot = new ChargingSlot { Id = 1, StationId = 1, Status = SlotStatus.Booked };
            _slotRepoMock
                .Setup(x => x.GetByIdAsync(1, true))
                .ReturnsAsync(slot);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UpdateStatusAsync(1, 1, 10, new UpdateSlotStatusDto { Status = SlotStatus.Inactive }));
        }

        /// <summary>
        /// ❌ Không phải Owner → throw UnauthorizedAccessException.
        /// </summary>
        [Fact]
        public async Task UpdateSlotStatus_ShouldFail_WhenNotOwner()
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(MakeStation(ownerId: 99, ApprovalStatus.Approved));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.UpdateStatusAsync(1, 1, ownerUserId: 10, new UpdateSlotStatusDto()));
        }

        /// <summary>
        /// ❌ Station không tồn tại → throw KeyNotFoundException.
        /// </summary>
        [Fact]
        public async Task UpdateSlotStatus_ShouldFail_WhenStationNotFound()
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(999, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync((ChargingStation?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _service.UpdateStatusAsync(999, 1, 10, new UpdateSlotStatusDto()));
        }

        // ─────────────────────────────────────────────
        // GET SLOT (READ)
        // ─────────────────────────────────────────────

        /// <summary>
        /// GetByIdAsync trả về null khi slot không thuộc station.
        /// </summary>
        [Fact]
        public async Task GetById_ShouldReturnNull_WhenSlotNotInStation()
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(MakeStation(ownerId: 10, ApprovalStatus.Approved));

            var slot = new ChargingSlot { Id = 5, StationId = 999 }; // thuộc station khác
            _slotRepoMock
                .Setup(x => x.GetByIdAsync(5, false))
                .ReturnsAsync(slot);

            var result = await _service.GetByIdAsync(stationId: 1, slotId: 5, ownerUserId: 10);

            Assert.Null(result);
        }

        /// <summary>
        /// GetAllByStationAsync trả về danh sách đúng.
        /// </summary>
        [Fact]
        public async Task GetAllByStation_ShouldReturnMappedList()
        {
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(MakeStation(ownerId: 10, ApprovalStatus.Approved));

            var slots = new List<ChargingSlot>
            {
                new ChargingSlot { Id = 1, StationId = 1, SlotName = "A", ConnectorType = "T2", Status = SlotStatus.Active },
                new ChargingSlot { Id = 2, StationId = 1, SlotName = "B", ConnectorType = "CCS", Status = SlotStatus.Inactive }
            };

            _slotRepoMock
                .Setup(x => x.GetAllByStationAsync(1))
                .ReturnsAsync(slots);

            var result = await _service.GetAllByStationAsync(stationId: 1, ownerUserId: 10);

            Assert.Equal(2, result.Count);
            Assert.Equal("A", result[0].SlotName);
            Assert.Equal("B", result[1].SlotName);
        }
    }
}
