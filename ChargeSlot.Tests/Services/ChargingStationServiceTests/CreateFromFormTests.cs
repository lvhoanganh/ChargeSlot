using Moq;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.DTOs.Station;
using Microsoft.AspNetCore.Http;
using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Tests.Services.ChargingStationServiceTests
{
    public class CreateFromFormTests : ChargingStationServiceTestBase
    {
        // Helper: setup owner (Approved) + signed contract + station reload
        private void SetupApprovedOwnerWithContract(int userId = 1)
        {
            _ownerRepoMock
                .Setup(x => x.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(CreateApprovedOwner(userId, KycStatus.Approved));
            _contractRepoMock
                .Setup(x => x.GetByOwnerAsync(It.IsAny<int>()))
                .ReturnsAsync(CreateSignedContract(userId));
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(CreateStation());
        }

        // TC01: Owner chưa có profile → user tồn tại → tự tạo Owner profile rồi tiếp tục (rồi fail KYC)
        [Fact]
        public async Task CreateFromForm_OwnerProfileNotFound_UserExists_AutoCreatesOwnerProfile()
        {
            // ownerRepo trả null để kích hoạt nhánh auto-create
            _ownerRepoMock
                .Setup(x => x.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync((Owner?)null);
            _userManagerMock.Setup(x => x.FindByIdAsync("1"))
                .ReturnsAsync(new ApplicationUser { Id = 1, FullName = "Nguyen Van A" });

            // KycStatus mặc định None → ném lỗi KYC sau khi tạo owner
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CreateFromFormAsync(1, CreateValidFormDto(), null!));

            _ownerRepoMock.Verify(x => x.AddAsync(It.IsAny<Owner>()), Times.Once);
        }

        // TC02: Owner profile null + User không tồn tại → throw "User not found"
        [Fact]
        public async Task CreateFromForm_OwnerProfileNotFound_UserNotFound_Throws()
        {
            _ownerRepoMock
                .Setup(x => x.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync((Owner?)null);
            _userManagerMock.Setup(x => x.FindByIdAsync("1")).ReturnsAsync((ApplicationUser?)null);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CreateFromFormAsync(1, CreateValidFormDto(), null!));

            Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // TC03: Owner KycStatus = Pending → throw lỗi KYC
        [Fact]
        public async Task CreateFromForm_KycNotApproved_Throws()
        {
            _ownerRepoMock
                .Setup(x => x.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(CreateApprovedOwner(1, KycStatus.Pending));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CreateFromFormAsync(1, CreateValidFormDto(), null!));

            Assert.Contains("KYC", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // TC04: KYC Approved nhưng Contract null → throw
        [Fact]
        public async Task CreateFromForm_ContractNotSigned_Null_Throws()
        {
            _ownerRepoMock
                .Setup(x => x.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(CreateApprovedOwner());
            _contractRepoMock.Setup(x => x.GetByOwnerAsync(It.IsAny<int>())).ReturnsAsync((Contract?)null);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CreateFromFormAsync(1, CreateValidFormDto(), null!));

            Assert.Contains("hợp đồng", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // TC05: Contract tồn tại nhưng Status != Signed → throw
        [Fact]
        public async Task CreateFromForm_ContractNotSigned_PendingStatus_Throws()
        {
            _ownerRepoMock
                .Setup(x => x.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(CreateApprovedOwner());
            _contractRepoMock.Setup(x => x.GetByOwnerAsync(It.IsAny<int>()))
                .ReturnsAsync(new Contract { OwnerUserId = 1, Status = ContractStatus.Pending });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CreateFromFormAsync(1, CreateValidFormDto(), null!));
        }

        // TC06: Happy path tối giản — không ảnh, không slot, không pricing
        [Fact]
        public async Task CreateFromForm_ValidOwner_NoExtras_CreatesStation()
        {
            SetupApprovedOwnerWithContract();

            var result = await CreateService().CreateFromFormAsync(1, CreateValidFormDto(withSlots: false), null!);

            _stationRepoMock.Verify(x => x.AddAsync(It.IsAny<ChargingStation>()), Times.Once);
            Assert.NotNull(result);
        }

        // TC07: Có slots + operating hours (OpenTime="08:00", CloseTime="22:00") → tạo trạm thành công
        [Fact]
        public async Task CreateFromForm_WithSlotsAndHours_CreatesSuccessfully()
        {
            SetupApprovedOwnerWithContract();

            var result = await CreateService().CreateFromFormAsync(1,
                CreateValidFormDto(withSlots: true, withHours: true), null!);

            Assert.NotNull(result);
            _stationRepoMock.Verify(x => x.AddAsync(It.IsAny<ChargingStation>()), Times.Once);
        }

        // TC08: Có 2 ảnh upload (Length > 0) → UploadAsync được gọi đúng 2 lần
        [Fact]
        public async Task CreateFromForm_WithImages_CallsUploadForEachFile()
        {
            SetupApprovedOwnerWithContract();
            var dto = CreateValidFormDto();
            dto.Images = new IFormFile[]
            {
                CreateMockFormFile("img1.jpg"),  // Length = 1024 > 0
                CreateMockFormFile("img2.jpg")   // Length = 1024 > 0
            };

            await CreateService().CreateFromFormAsync(1, dto, null!);

            _fileStorageMock.Verify(x => x.UploadAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Exactly(2));
        }

        // TC09: Có StationPricing hợp lệ (StartTime="08:00", EndTime="22:00") → pricing được add vào repo
        [Fact]
        public async Task CreateFromForm_WithValidPricing_AddsPricingRecords()
        {
            SetupApprovedOwnerWithContract();

            await CreateService().CreateFromFormAsync(1, CreateValidFormDto(withPricing: true), null!);

            _pricingRepoMock.Verify(x => x.Add(It.IsAny<StationPricing>()), Times.Once);
        }

        // TC10: StationPricing format sai (StartTime="invalid", EndTime="also-bad") → bị skip, không throw
        [Fact]
        public async Task CreateFromForm_InvalidPricingFormat_SkipsAndDoesNotThrow()
        {
            SetupApprovedOwnerWithContract();
            var dto = CreateValidFormDto();
            dto.StationPricing = new List<StationPricingFormItem>
            {
                new StationPricingFormItem { StartTime = "invalid", EndTime = "also-bad", PricePerHour = 10000 }
            };

            await CreateService().CreateFromFormAsync(1, dto, null!);

            _pricingRepoMock.Verify(x => x.Add(It.IsAny<StationPricing>()), Times.Never);
        }

        // TC11: KycStatus = PendingUpdate → được phép tạo trạm
        [Fact]
        public async Task CreateFromForm_KycPendingUpdate_IsAllowed()
        {
            _ownerRepoMock
                .Setup(x => x.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(CreateApprovedOwner(1, KycStatus.PendingUpdate));
            _contractRepoMock.Setup(x => x.GetByOwnerAsync(It.IsAny<int>())).ReturnsAsync(CreateSignedContract());
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(CreateStation());

            var result = await CreateService().CreateFromFormAsync(1,
                CreateValidFormDto(withSlots: false), null!);

            Assert.NotNull(result);
        }

        // TC12 (Boundary): Images = [file với Length=0] → file bị skip, UploadAsync NOT called
        [Fact]
        public async Task CreateFromForm_WithZeroLengthImage_SkipsUpload()
        {
            SetupApprovedOwnerWithContract();
            var dto = CreateValidFormDto();
            // File rỗng (Length = 0) → nhánh if (file.Length > 0) trả false → không upload
            dto.Images = new IFormFile[] { CreateMockFormFile("empty.jpg", length: 0) };

            await CreateService().CreateFromFormAsync(1, dto, null!);

            _fileStorageMock.Verify(x => x.UploadAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Never);
        }

        // TC13 (Boundary): OperatingHours với OpenTime="" và CloseTime="" → không throw, OpenTime/CloseTime = null
        [Fact]
        public async Task CreateFromForm_OperatingHoursWithEmptyTimes_ParsesAsNull()
        {
            SetupApprovedOwnerWithContract();
            var dto = CreateValidFormDto(withSlots: false);
            dto.OperatingHours = new List<OperatingHoursFormItem>
            {
                // OpenTime/CloseTime rỗng → IsNullOrEmpty check → parse là null, không throw
                new OperatingHoursFormItem { DayOfWeek = 1, IsClosed = false, OpenTime = "", CloseTime = "" }
            };

            // Không throw exception
            var result = await CreateService().CreateFromFormAsync(1, dto, null!);

            Assert.NotNull(result);
            _stationRepoMock.Verify(x => x.AddAsync(It.IsAny<ChargingStation>()), Times.Once);
        }
    }
}
