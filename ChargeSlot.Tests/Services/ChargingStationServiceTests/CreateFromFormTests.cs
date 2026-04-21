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
            // Must use It.IsAny<bool>() because GetByUserIdAsync has optional tracking param
            _ownerRepoMock
                .Setup(x => x.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(CreateApprovedOwner(userId, KycStatus.Approved));
            _contractRepoMock
                .Setup(x => x.GetByOwnerAsync(It.IsAny<int>()))
                .ReturnsAsync(CreateSignedContract(userId));
            // Must use It.IsAny for all params because GetByIdAsync has optional params
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
                CreateService().CreateFromFormAsync(1, CreateValidFormDto(), CreateFakeHttpRequest()));

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
                CreateService().CreateFromFormAsync(1, CreateValidFormDto(), CreateFakeHttpRequest()));

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
                CreateService().CreateFromFormAsync(1, CreateValidFormDto(), CreateFakeHttpRequest()));

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
                CreateService().CreateFromFormAsync(1, CreateValidFormDto(), CreateFakeHttpRequest()));

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
                CreateService().CreateFromFormAsync(1, CreateValidFormDto(), CreateFakeHttpRequest()));
        }

        // TC06: Happy path tối giản — không ảnh, không slot, không pricing
        [Fact]
        public async Task CreateFromForm_ValidOwner_NoExtras_CreatesStation()
        {
            SetupApprovedOwnerWithContract();

            var result = await CreateService().CreateFromFormAsync(1, CreateValidFormDto(withSlots: false), CreateFakeHttpRequest());

            _stationRepoMock.Verify(x => x.AddAsync(It.IsAny<ChargingStation>()), Times.Once);
            Assert.NotNull(result);
        }

        // TC07: Có slots + operating hours → tạo trạm thành công
        [Fact]
        public async Task CreateFromForm_WithSlotsAndHours_CreatesSuccessfully()
        {
            SetupApprovedOwnerWithContract();

            var result = await CreateService().CreateFromFormAsync(1,
                CreateValidFormDto(withSlots: true, withHours: true), CreateFakeHttpRequest());

            Assert.NotNull(result);
            _stationRepoMock.Verify(x => x.AddAsync(It.IsAny<ChargingStation>()), Times.Once);
        }

        // TC08: Có ảnh upload → UploadAsync được gọi đúng số lần
        [Fact]
        public async Task CreateFromForm_WithImages_CallsUploadForEachFile()
        {
            SetupApprovedOwnerWithContract();
            var dto = CreateValidFormDto();
            dto.Images = new IFormFile[]
            {
                CreateMockFormFile("img1.jpg"),
                CreateMockFormFile("img2.jpg")
            };

            await CreateService().CreateFromFormAsync(1, dto, CreateFakeHttpRequest());

            _fileStorageMock.Verify(x => x.UploadAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Exactly(2));
        }

        // TC09: Có StationPricing hợp lệ → pricing được add vào repo
        [Fact]
        public async Task CreateFromForm_WithValidPricing_AddsPricingRecords()
        {
            SetupApprovedOwnerWithContract();

            await CreateService().CreateFromFormAsync(1, CreateValidFormDto(withPricing: true), CreateFakeHttpRequest());

            _pricingRepoMock.Verify(x => x.Add(It.IsAny<StationPricing>()), Times.Once);
        }

        // TC10: StationPricing format sai → bị skip, không throw
        [Fact]
        public async Task CreateFromForm_InvalidPricingFormat_SkipsAndDoesNotThrow()
        {
            SetupApprovedOwnerWithContract();
            var dto = CreateValidFormDto();
            dto.StationPricing = new List<StationPricingFormItem>
            {
                new StationPricingFormItem { StartTime = "invalid", EndTime = "also-bad", PricePerHour = 10000 }
            };

            await CreateService().CreateFromFormAsync(1, dto, CreateFakeHttpRequest());

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
                CreateValidFormDto(withSlots: false), CreateFakeHttpRequest());

            Assert.NotNull(result);
        }
    }
}
