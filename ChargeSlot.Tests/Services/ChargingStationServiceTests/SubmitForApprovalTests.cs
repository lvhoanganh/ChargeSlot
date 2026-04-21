using Moq;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Tests.Services.ChargingStationServiceTests
{
    public class SubmitForApprovalTests : ChargingStationServiceTestBase
    {
        // TC01: Station không tồn tại → throw KeyNotFoundException
        [Fact]
        public async Task Submit_StationNotFound_Throws()
        {
            _stationRepoMock.Setup(x => x.GetByIdAsync(99, true, true)).ReturnsAsync((ChargingStation?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                CreateService().SubmitForApprovalAsync(99, 1));
        }

        // TC02: Không phải owner → throw UnauthorizedAccessException
        [Fact]
        public async Task Submit_NotOwner_Throws()
        {
            var station = CreateStation(id: 1, ownerUserId: 99);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, true, true)).ReturnsAsync(station);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().SubmitForApprovalAsync(1, 1)); // userId = 1, ownerUserId = 99
        }

        // TC03: Station đang ở PendingApproval → không thể submit lại
        [Fact]
        public async Task Submit_AlreadyPending_Throws()
        {
            var station = CreateStation(id: 1, ownerUserId: 1, approval: ApprovalStatus.PendingApproval);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, true, true)).ReturnsAsync(station);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().SubmitForApprovalAsync(1, 1));

            Assert.Contains("PendingApproval", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // TC04: Approved status → cũng không thể submit
        [Fact]
        public async Task Submit_Approved_Throws()
        {
            var station = CreateStation(id: 1, ownerUserId: 1, approval: ApprovalStatus.Approved);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, true, true)).ReturnsAsync(station);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().SubmitForApprovalAsync(1, 1));
        }

        // TC05: Station ở Draft nhưng thiếu data hợp lệ (không có slot) → throw validation error
        [Fact]
        public async Task Submit_DraftNoSlots_ValidationFails_Throws()
        {
            var station = CreateStation(id: 1, ownerUserId: 1, approval: ApprovalStatus.Draft);
            station.ChargingSlots = new List<ChargingSlot>(); // rỗng

            _stationRepoMock.Setup(x => x.GetByIdAsync(1, true, true)).ReturnsAsync(station);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().SubmitForApprovalAsync(1, 1));

            Assert.Contains("slot", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // TC06: Station ở Draft nhưng thiếu Address → validation fail
        [Fact]
        public async Task Submit_DraftNoAddress_ValidationFails_Throws()
        {
            var station = CreateStation(id: 1, ownerUserId: 1, approval: ApprovalStatus.Draft);
            station.Address = "";

            _stationRepoMock.Setup(x => x.GetByIdAsync(1, true, true)).ReturnsAsync(station);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().SubmitForApprovalAsync(1, 1));

            Assert.Contains("validation", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // TC07: Happy path từ Draft → chuyển sang PendingApproval + notify admin
        [Fact]
        public async Task Submit_ValidDraft_TransitionsToPendingApproval()
        {
            var station = CreateStation(id: 1, ownerUserId: 1, approval: ApprovalStatus.Draft);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, true, true)).ReturnsAsync(station);
            _userManagerMock.Setup(x => x.GetUsersInRoleAsync("Admin"))
                .ReturnsAsync(new List<ApplicationUser> { new ApplicationUser { Id = 99 } });

            await CreateService().SubmitForApprovalAsync(1, 1);

            Assert.Equal(ApprovalStatus.PendingApproval, station.ApprovalStatus);
            Assert.NotNull(station.SubmittedAt);
            _uowMock.Verify(x => x.CompleteAsync(), Times.Once);
        }

        // TC08: Station ở Rejected → có thể submit lại, AdminNote được clear
        [Fact]
        public async Task Submit_Rejected_CanResubmit_ClearsAdminNote()
        {
            var station = CreateStation(id: 1, ownerUserId: 1, approval: ApprovalStatus.Rejected);
            station.AdminNote = "Thiếu ảnh";

            _stationRepoMock.Setup(x => x.GetByIdAsync(1, true, true)).ReturnsAsync(station);

            await CreateService().SubmitForApprovalAsync(1, 1);

            Assert.Equal(ApprovalStatus.PendingApproval, station.ApprovalStatus);
            Assert.Null(station.AdminNote); // phải được clear
        }
    }
}
