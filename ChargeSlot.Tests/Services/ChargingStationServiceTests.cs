using Xunit;
using Moq;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Implementation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Tests.Services
{
    /// <summary>
    /// Unit tests cho ChargingStationService - quản lý trạm sạc và luồng duyệt Admin.
    /// Dùng InMemory DB để test logic có EF Core (Owner auto-create, slot activation).
    /// </summary>
    public class ChargingStationServiceTests
    {
        private readonly Mock<IChargingStationRepository>   _repoMock;
        private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        private readonly ChargeSlotDbContext                _context;
        private readonly ChargingStationService             _service;

        public ChargingStationServiceTests()
        {
            _repoMock = new Mock<IChargingStationRepository>();

            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            // Mỗi test dùng DB riêng biệt tránh ô nhiễm dữ liệu
            var options = new DbContextOptionsBuilder<ChargeSlotDbContext>()
                .UseInMemoryDatabase($"TestStation_{Guid.NewGuid()}")
                .Options;
            _context = new ChargeSlotDbContext(options);

            _service = new ChargingStationService(
                _repoMock.Object,
                _context,
                _userManagerMock.Object);

            // Quan trọng: repo mock phải delegate SaveChangesAsync sang _context
            // vì service dùng cả _stationRepo.SaveChangesAsync() lẫn _context.Notifications.Add()
            _repoMock
                .Setup(x => x.SaveChangesAsync())
                .Returns(() => _context.SaveChangesAsync().ContinueWith(_ => 0));
        }

        // ─────────────────────────────────────────────
        // CREATE STATION
        // ─────────────────────────────────────────────

        /// <summary>
        /// ✅ Owner chưa có record → tự động tạo Owner profile, tạo station thành công.
        /// </summary>
        [Fact]
        public async Task Create_ShouldAutoCreateOwnerProfile_WhenNotExist()
        {
            var user = new ApplicationUser
            {
                Id = 1, FullName = "Nguyen Van A",
                UserName = "0912345678", NormalizedUserName = "0912345678",
                PhoneNumber = "0912345678",
                Email = "a@test.com", NormalizedEmail = "A@TEST.COM"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var dto = new CreateChargingStationDto
            {
                Name      = "Station Alpha",
                Address   = "Hà Nội",
                Latitude  = 21.0m,
                Longitude = 105.8m
            };

            var result = await _service.CreateAsync(ownerUserId: 1, dto);

            Assert.NotNull(result);
            Assert.Equal("Station Alpha", result.Name);
            Assert.Equal(ApprovalStatus.Draft.ToString(), result.ApprovalStatus);

            // Owner profile tự tạo
            var owner = await _context.Owner.FirstOrDefaultAsync(o => o.UserId == 1);
            Assert.NotNull(owner);
        }

        /// <summary>
        /// ✅ Owner đã có profile → không tạo lại, station vẫn tạo được.
        /// </summary>
        [Fact]
        public async Task Create_ShouldNotDuplicateOwner_WhenAlreadyExists()
        {
            var user = new ApplicationUser
            {
                Id = 2, FullName = "Tran Thi B",
                UserName = "0911111111", NormalizedUserName = "0911111111",
                PhoneNumber = "0911111111",
                Email = "b@test.com", NormalizedEmail = "B@TEST.COM"
            };
            _context.Users.Add(user);
            _context.Owner.Add(new Owner { UserId = 2, BusinessName = "B Corp", TaxCode = "123" });
            await _context.SaveChangesAsync();

            var dto = new CreateChargingStationDto
            {
                Name = "Station Beta", Address = "HCM", Latitude = 10m, Longitude = 106m
            };

            var result = await _service.CreateAsync(2, dto);

            Assert.NotNull(result);
            // Phải vẫn chỉ có 1 Owner record
            Assert.Single(_context.Owner.Where(o => o.UserId == 2));
        }

        /// <summary>
        /// Station mới phải có ApprovalStatus = Draft, OperationalStatus = Inactive.
        /// </summary>
        [Fact]
        public async Task Create_ShouldStartWithDraftStatus()
        {
            var user = new ApplicationUser
            {
                Id = 3, FullName = "User C",
                UserName = "0900000003", NormalizedUserName = "0900000003",
                PhoneNumber = "0900000003",
                Email = "c@test.com", NormalizedEmail = "C@TEST.COM"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var dto = new CreateChargingStationDto { Name = "C Station", Address = "DN", Latitude = 16m, Longitude = 108m };

            var result = await _service.CreateAsync(3, dto);

            Assert.Equal(ApprovalStatus.Draft.ToString(), result.ApprovalStatus);
            Assert.Equal(OperationalStatus.Inactive.ToString(), result.OperationalStatus);
        }

        // ─────────────────────────────────────────────
        // UPDATE STATION
        // ─────────────────────────────────────────────

        /// <summary>
        /// ❌ Không phải Owner → throw UnauthorizedAccessException.
        /// </summary>
        [Fact]
        public async Task Update_ShouldFail_WhenNotOwner()
        {
            var station = new ChargingStation
            {
                Id = 1, OwnerUserId = 99, ApprovalStatus = ApprovalStatus.Draft
            };

            _repoMock
                .Setup(x => x.GetByIdAsync(1, true, true))
                .ReturnsAsync(station);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.UpdateAsync(id: 1, ownerUserId: 10, new UpdateChargingStationDto()));
        }

        /// <summary>
        /// ❌ Station Approved → không được sửa.
        /// </summary>
        [Theory]
        [InlineData(ApprovalStatus.Approved)]
        [InlineData(ApprovalStatus.PendingApproval)]
        public async Task Update_ShouldFail_WhenStationNotEditable(ApprovalStatus status)
        {
            var station = new ChargingStation
            {
                Id = 1, OwnerUserId = 10, ApprovalStatus = status
            };

            _repoMock
                .Setup(x => x.GetByIdAsync(1, true, true))
                .ReturnsAsync(station);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UpdateAsync(1, 10, new UpdateChargingStationDto()));
        }

        /// <summary>
        /// ❌ Station không tồn tại → throw KeyNotFoundException.
        /// </summary>
        [Fact]
        public async Task Update_ShouldFail_WhenStationNotFound()
        {
            _repoMock
                .Setup(x => x.GetByIdAsync(999, true, true))
                .ReturnsAsync((ChargingStation?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _service.UpdateAsync(999, 10, new UpdateChargingStationDto()));
        }

        // ─────────────────────────────────────────────
        // DELETE STATION
        // ─────────────────────────────────────────────

        /// <summary>
        /// ❌ Không phải Owner → throw UnauthorizedAccessException.
        /// </summary>
        [Fact]
        public async Task Delete_ShouldFail_WhenNotOwner()
        {
            var station = new ChargingStation
            {
                Id = 1, OwnerUserId = 99, ApprovalStatus = ApprovalStatus.Draft
            };

            _repoMock
                .Setup(x => x.GetByIdAsync(1, true, true))
                .ReturnsAsync(station);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.DeleteAsync(1, ownerUserId: 10));
        }

        /// <summary>
        /// ❌ Station Approved → không được xóa.
        /// </summary>
        [Theory]
        [InlineData(ApprovalStatus.Approved)]
        [InlineData(ApprovalStatus.PendingApproval)]
        public async Task Delete_ShouldFail_WhenStationNotDeletable(ApprovalStatus status)
        {
            var station = new ChargingStation
            {
                Id = 1, OwnerUserId = 10, ApprovalStatus = status
            };

            _repoMock
                .Setup(x => x.GetByIdAsync(1, true, true))
                .ReturnsAsync(station);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.DeleteAsync(1, 10));
        }

        // ─────────────────────────────────────────────
        // SUBMIT FOR APPROVAL
        // ─────────────────────────────────────────────

        /// <summary>
        /// ✅ Submit hợp lệ (Draft, đầy đủ thông tin) → PendingApproval + notify Admin.
        /// </summary>
        [Fact]
        public async Task Submit_ShouldSuccess_AndNotifyAdmins()
        {
            var station = new ChargingStation
            {
                Id             = 1,
                OwnerUserId    = 1,
                ApprovalStatus = ApprovalStatus.Draft,
                Name           = "Station X",
                Address        = "Hà Nội",
                Latitude       = 21.0m,
                Longitude      = 105.8m,
                ChargingSlots  = new List<ChargingSlot> { new ChargingSlot() }
            };

            _repoMock
                .Setup(x => x.GetByIdAsync(1, true, true))
                .ReturnsAsync(station);

            _userManagerMock
                .Setup(x => x.GetUsersInRoleAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<ApplicationUser>
                {
                    new ApplicationUser { Id = 100 },
                    new ApplicationUser { Id = 101 }
                });

            await _service.SubmitForApprovalAsync(id: 1, ownerUserId: 1);

            Assert.Equal(ApprovalStatus.PendingApproval, station.ApprovalStatus);
            Assert.NotNull(station.SubmittedAt);

            // Notification được tạo cho 2 admin
            var notifications = _context.Notifications.ToList();
            Assert.Equal(2, notifications.Count);
            Assert.All(notifications, n => Assert.Equal(NotificationType.StationApproval, n.Type));
        }

        /// <summary>
        /// ✅ Resubmit sau khi bị Rejected cũng được.
        /// </summary>
        [Fact]
        public async Task Submit_ShouldSuccess_WhenStationRejected()
        {
            var station = new ChargingStation
            {
                Id             = 1,
                OwnerUserId    = 1,
                ApprovalStatus = ApprovalStatus.Rejected,
                AdminNote      = "Thiếu slot",
                Name           = "S",
                Address        = "A",
                Latitude       = 1m,
                Longitude      = 1m,
                ChargingSlots  = new List<ChargingSlot> { new ChargingSlot() }
            };

            _repoMock
                .Setup(x => x.GetByIdAsync(1, true, true))
                .ReturnsAsync(station);

            _userManagerMock
                .Setup(x => x.GetUsersInRoleAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<ApplicationUser>());

            await _service.SubmitForApprovalAsync(1, 1);

            Assert.Equal(ApprovalStatus.PendingApproval, station.ApprovalStatus);
            Assert.Null(station.AdminNote); // clear admin note cũ
        }

        /// <summary>
        /// ❌ Station đã PendingApproval/Approved → không submit lại.
        /// </summary>
        [Theory]
        [InlineData(ApprovalStatus.PendingApproval)]
        [InlineData(ApprovalStatus.Approved)]
        public async Task Submit_ShouldFail_WhenAlreadySubmittedOrApproved(ApprovalStatus status)
        {
            var station = new ChargingStation
            {
                Id = 1, OwnerUserId = 1, ApprovalStatus = status
            };

            _repoMock
                .Setup(x => x.GetByIdAsync(1, true, true))
                .ReturnsAsync(station);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.SubmitForApprovalAsync(1, 1));
        }

        /// <summary>
        /// ❌ Thiếu Name → throw với thông báo lỗi validation.
        /// </summary>
        [Fact]
        public async Task Submit_ShouldFail_WhenNameMissing()
        {
            var station = new ChargingStation
            {
                Id             = 1,
                OwnerUserId    = 1,
                ApprovalStatus = ApprovalStatus.Draft,
                Name           = "",        // thiếu
                Address        = "Hà Nội",
                Latitude       = 21m,
                Longitude      = 105m,
                ChargingSlots  = new List<ChargingSlot> { new ChargingSlot() }
            };

            _repoMock
                .Setup(x => x.GetByIdAsync(1, true, true))
                .ReturnsAsync(station);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.SubmitForApprovalAsync(1, 1));

            Assert.Contains("Tên trạm", ex.Message);
        }

        /// <summary>
        /// ❌ Thiếu GPS (Latitude/Longitude null) → throw với thông báo lỗi.
        /// </summary>
        [Fact]
        public async Task Submit_ShouldFail_WhenGpsMissing()
        {
            var station = new ChargingStation
            {
                Id             = 1,
                OwnerUserId    = 1,
                ApprovalStatus = ApprovalStatus.Draft,
                Name           = "Station",
                Address        = "Hà Nội",
                Latitude       = null,  // thiếu GPS
                Longitude      = null,
                ChargingSlots  = new List<ChargingSlot> { new ChargingSlot() }
            };

            _repoMock
                .Setup(x => x.GetByIdAsync(1, true, true))
                .ReturnsAsync(station);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.SubmitForApprovalAsync(1, 1));

            Assert.Contains("GPS", ex.Message);
        }

        /// <summary>
        /// ❌ Không có slot nào → throw với thông báo yêu cầu ít nhất 1 slot.
        /// </summary>
        [Fact]
        public async Task Submit_ShouldFail_WhenNoSlots()
        {
            var station = new ChargingStation
            {
                Id             = 1,
                OwnerUserId    = 1,
                ApprovalStatus = ApprovalStatus.Draft,
                Name           = "Station",
                Address        = "Hà Nội",
                Latitude       = 21m,
                Longitude      = 105m,
                ChargingSlots  = new List<ChargingSlot>() // rỗng
            };

            _repoMock
                .Setup(x => x.GetByIdAsync(1, true, true))
                .ReturnsAsync(station);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.SubmitForApprovalAsync(1, 1));

            Assert.Contains("slot", ex.Message.ToLower());
        }

        // ─────────────────────────────────────────────
        // REVIEW STATION (ADMIN)
        // ─────────────────────────────────────────────

        /// <summary>
        /// ✅ Admin Approve station →
        ///   - ApprovalStatus = Approved
        ///   - OperationalStatus = Active
        ///   - Tất cả slot trong station → Active
        ///   - Gửi notification cho Owner
        /// </summary>
        [Fact]
        public async Task Review_ShouldApprove_AndActivateAllSlots()
        {
            // Dữ liệu trong InMemory DB
            var slot1 = new ChargingSlot { Id = 1, StationId = 10, SlotName = "A", ConnectorType = "T2", Status = SlotStatus.Inactive };
            var slot2 = new ChargingSlot { Id = 2, StationId = 10, SlotName = "B", ConnectorType = "CCS", Status = SlotStatus.Inactive };
            _context.ChargingSlots.AddRange(slot1, slot2);
            await _context.SaveChangesAsync();

            var station = new ChargingStation
            {
                Id             = 10,
                OwnerUserId    = 1,
                ApprovalStatus = ApprovalStatus.PendingApproval,
                Name           = "Station Y"
            };

            _repoMock
                .Setup(x => x.GetByIdAsync(10, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(station);

            await _service.ReviewStationAsync(id: 10, adminUserId: 99, new ReviewStationDto
            {
                IsApproved = true
            });

            Assert.Equal(ApprovalStatus.Approved, station.ApprovalStatus);
            Assert.Equal(OperationalStatus.Active, station.OperationalStatus);
            Assert.Equal(99, station.ReviewedByUserId);

            // Tất cả slot phải Active
            var slots = _context.ChargingSlots.Where(s => s.StationId == 10).ToList();
            Assert.All(slots, s => Assert.Equal(SlotStatus.Active, s.Status));

            // Notification cho Owner
            var notification = _context.Notifications.FirstOrDefault(n => n.UserId == 1);
            Assert.NotNull(notification);
            Assert.Equal(NotificationType.StationApproval, notification!.Type);
        }

        /// <summary>
        /// ✅ Admin Reject station với AdminNote →
        ///   - ApprovalStatus = Rejected
        ///   - Gửi notification cho Owner kèm lý do
        /// </summary>
        [Fact]
        public async Task Review_ShouldReject_WithAdminNote()
        {
            var station = new ChargingStation
            {
                Id             = 1,
                OwnerUserId    = 1,
                ApprovalStatus = ApprovalStatus.PendingApproval,
                Name           = "Station Z"
            };

            _repoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(station);

            await _service.ReviewStationAsync(1, adminUserId: 99, new ReviewStationDto
            {
                IsApproved = false,
                AdminNote  = "Thiếu thông tin địa chỉ chính xác"
            });

            Assert.Equal(ApprovalStatus.Rejected, station.ApprovalStatus);
            Assert.Equal("Thiếu thông tin địa chỉ chính xác", station.AdminNote);

            // Notification chứa lý do
            var noti = _context.Notifications.FirstOrDefault(n => n.UserId == 1);
            Assert.NotNull(noti);
            Assert.Contains("Thiếu thông tin", noti!.Content);
        }

        /// <summary>
        /// ❌ Reject mà không có AdminNote → throw InvalidOperationException.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task Review_ShouldFail_WhenRejectWithoutAdminNote(string? adminNote)
        {
            var station = new ChargingStation
            {
                Id = 1, OwnerUserId = 1, ApprovalStatus = ApprovalStatus.PendingApproval
            };

            _repoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(station);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.ReviewStationAsync(1, 99, new ReviewStationDto
                {
                    IsApproved = false,
                    AdminNote  = adminNote
                }));
        }

        /// <summary>
        /// ❌ Station không ở PendingApproval → không cho review.
        /// </summary>
        [Theory]
        [InlineData(ApprovalStatus.Draft)]
        [InlineData(ApprovalStatus.Approved)]
        [InlineData(ApprovalStatus.Rejected)]
        public async Task Review_ShouldFail_WhenStationNotPendingApproval(ApprovalStatus status)
        {
            var station = new ChargingStation
            {
                Id = 1, OwnerUserId = 1, ApprovalStatus = status
            };

            _repoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(station);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.ReviewStationAsync(1, 99, new ReviewStationDto { IsApproved = true }));
        }

        /// <summary>
        /// ❌ Station không tồn tại → throw KeyNotFoundException.
        /// </summary>
        [Fact]
        public async Task Review_ShouldFail_WhenStationNotFound()
        {
            _repoMock
                .Setup(x => x.GetByIdAsync(999, It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync((ChargingStation?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _service.ReviewStationAsync(999, 99, new ReviewStationDto { IsApproved = true }));
        }
    }
}
