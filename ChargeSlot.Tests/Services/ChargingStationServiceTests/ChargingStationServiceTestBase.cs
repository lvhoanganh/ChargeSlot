using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.DTOs.Station;
using Microsoft.Extensions.DependencyInjection;

namespace ChargeSlot.Tests.Services.ChargingStationServiceTests
{
    /// <summary>
    /// Base class chứa toàn bộ mock chung cho ChargingStationService tests.
    /// </summary>
    public abstract class ChargingStationServiceTestBase
    {
        protected readonly Mock<IChargingStationRepository> _stationRepoMock = new();
        protected readonly Mock<IOwnerRepository> _ownerRepoMock = new();
        protected readonly Mock<IContractRepository> _contractRepoMock = new();
        protected readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        protected readonly Mock<IFileStorageService> _fileStorageMock = new();
        protected readonly Mock<IStationPricingRepository> _pricingRepoMock = new();
        protected readonly Mock<IExtraServiceRepository> _extraServiceRepoMock = new();
        protected readonly Mock<IStationUnavailableDateRepository> _unavailableDateRepoMock = new();
        protected readonly Mock<IBookingRepository> _bookingRepoMock = new();
        protected readonly Mock<INotificationService> _notificationMock = new();
        protected readonly Mock<IUnitOfWork> _uowMock = new();
        protected readonly Mock<IServiceProvider> _serviceProviderMock = new();

        protected ChargingStationServiceTestBase()
        {
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            // Default setups
            _uowMock.Setup(x => x.CompleteAsync()).ReturnsAsync(1);
            _notificationMock.Setup(x => x.SendAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Api.Enums.NotificationType>()))
                .Returns(Task.CompletedTask);
            _stationRepoMock.Setup(x => x.AddAsync(It.IsAny<ChargingStation>()))
                .Returns(Task.CompletedTask);
            _ownerRepoMock.Setup(x => x.AddAsync(It.IsAny<Owner>()))
                .Returns(Task.CompletedTask);
            _fileStorageMock.Setup(x => x.UploadAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
                .ReturnsAsync("https://storage.example.com/image.jpg");
            _fileStorageMock.Setup(x => x.DeleteAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _userManagerMock.Setup(x => x.GetUsersInRoleAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<ApplicationUser>());

            // NOTE: GetByUserIdAsync has optional 'tracking' param → must use It.IsAny<bool>() in Moq
            // Default: return null (each test overrides as needed)
            _ownerRepoMock
                .Setup(x => x.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync((Owner?)null);

            // NOTE: GetByIdAsync has optional params → must use It.IsAny for all
            _stationRepoMock
                .Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync((ChargingStation?)null);
        }

        protected ChargingStationService CreateService() => new ChargingStationService(
            _stationRepoMock.Object,
            _ownerRepoMock.Object,
            _contractRepoMock.Object,
            _userManagerMock.Object,
            _fileStorageMock.Object,
            _pricingRepoMock.Object,
            _extraServiceRepoMock.Object,
            _unavailableDateRepoMock.Object,
            _bookingRepoMock.Object,
            _notificationMock.Object,
            _uowMock.Object,
            _serviceProviderMock.Object);

        // ─── HELPERS ───

        protected static Owner CreateApprovedOwner(int userId = 1, KycStatus kyc = KycStatus.Approved)
            => new Owner { UserId = userId, KycStatus = kyc, BusinessName = "Test Biz", TaxCode = "123" };

        protected static Contract CreateSignedContract(int ownerUserId = 1)
            => new Contract { OwnerUserId = ownerUserId, Status = ContractStatus.Signed };

        protected static ChargingStation CreateStation(
            int id = 1,
            int ownerUserId = 1,
            ApprovalStatus approval = ApprovalStatus.Draft,
            OperationalStatus operational = OperationalStatus.Inactive)
        {
            return new ChargingStation
            {
                Id = id,
                OwnerUserId = ownerUserId,
                Name = "Test Station",
                Address = "123 Test St",
                Latitude = 10.0m,
                Longitude = 106.0m,
                ApprovalStatus = approval,
                OperationalStatus = operational,
                ChargingSlots = new List<ChargingSlot>
                {
                    new ChargingSlot { Id = 1, SlotName = "Slot A", Status = SlotStatus.Inactive }
                },
                Images = new List<StationImage>(),
                OperatingHours = new List<StationOperatingHours>()
            };
        }

        protected static CreateStationFormDto CreateValidFormDto(
            bool withSlots = true,
            bool withHours = false,
            bool withPricing = false)
        {
            var dto = new CreateStationFormDto
            {
                Name = "Test Station",
                Address = "123 Test St",
                Latitude = 10.0m,
                Longitude = 106.0m,
                LayoutWidth = 800,
                LayoutHeight = 600
            };

            if (withSlots)
                dto.Slots = new List<SlotFormItem>
                {
                    new SlotFormItem { SlotName = "Slot A", PositionX = 100, PositionY = 200 }
                };

            if (withHours)
                dto.OperatingHours = new List<OperatingHoursFormItem>
                {
                    new OperatingHoursFormItem { DayOfWeek = 1, IsClosed = false, OpenTime = "08:00", CloseTime = "22:00" }
                };

            if (withPricing)
                dto.StationPricing = new List<StationPricingFormItem>
                {
                    new StationPricingFormItem { StartTime = "08:00", EndTime = "22:00", PricePerHour = 15000 }
                };

            return dto;
        }

        protected static IFormFile CreateMockFormFile(string fileName = "test.jpg", long length = 1024)
        {
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns(fileName);
            fileMock.Setup(f => f.Length).Returns(length);
            fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
            fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[length]));
            return fileMock.Object;
        }

        protected static HttpRequest CreateFakeHttpRequest()
        {
            var httpContextMock = new Mock<Microsoft.AspNetCore.Http.HttpContext>();
            var requestMock = new Mock<HttpRequest>();
            requestMock.Setup(r => r.HttpContext).Returns(httpContextMock.Object);
            return requestMock.Object;
        }
    }
}
