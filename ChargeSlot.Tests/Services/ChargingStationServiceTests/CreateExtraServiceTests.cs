using Moq;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.DTOs.Station;

namespace ChargeSlot.Tests.Services.ChargingStationServiceTests
{
    public class CreateExtraServiceTests : ChargingStationServiceTestBase
    {
        private CreateExtraServiceDto CreateValidDto(
            string serviceName = "Wifi",
            decimal price = 5000,
            bool isRental = false,
            string? description = "Free wifi") => new CreateExtraServiceDto
            {
                ServiceName = serviceName,
                Description = description,
                Price = price,
                TotalStock = 10,
                IsRental = isRental
            };

        // TC01: Station không tồn tại → throw KeyNotFoundException
        [Fact]
        public async Task CreateExtraService_StationNotFound_Throws()
        {
            _stationRepoMock.Setup(x => x.GetByIdAsync(99, false, true)).ReturnsAsync((ChargingStation?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                CreateService().CreateExtraServiceAsync(99, 1, CreateValidDto()));
        }

        // TC02: Không phải owner → throw UnauthorizedAccessException
        [Fact]
        public async Task CreateExtraService_NotOwner_Throws()
        {
            var station = CreateStation(id: 1, ownerUserId: 99);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().CreateExtraServiceAsync(1, 1, CreateValidDto()));
        }

        // TC03: ServiceName null/empty → throw InvalidOperationException
        [Fact]
        public async Task CreateExtraService_EmptyServiceName_Throws()
        {
            var station = CreateStation(id: 1, ownerUserId: 1);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CreateExtraServiceAsync(1, 1, CreateValidDto(serviceName: "")));

            Assert.Contains("dịch vụ", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // TC04: ServiceName chỉ có khoảng trắng → throw
        [Fact]
        public async Task TC04_WhitespaceServiceName_Throws()
        {
            var station = CreateStation(id: 1, ownerUserId: 1);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CreateExtraServiceAsync(1, 1, CreateValidDto(serviceName: "   ")));
        }

        // TC05: Price âm → throw InvalidOperationException
        [Fact]
        public async Task TC05_NegativePrice_Throws()
        {
            var station = CreateStation(id: 1, ownerUserId: 1);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CreateExtraServiceAsync(1, 1, CreateValidDto(price: -1000)));

            Assert.Contains("âm", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // TC06: Happy path — IsRental = false → tạo thành công
        [Fact]
        public async Task TC06_ValidData_NonRental_CreatesSuccessfully()
        {
            var station = CreateStation(id: 1, ownerUserId: 1);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            var result = await CreateService().CreateExtraServiceAsync(1, 1, CreateValidDto());

            Assert.NotNull(result);
            Assert.Equal("Wifi", result.ServiceName);
            Assert.True(result.IsActive);
            Assert.False(result.IsRental);
            _extraServiceRepoMock.Verify(x => x.Add(It.IsAny<ExtraService>()), Times.Once);
            _uowMock.Verify(x => x.CompleteAsync(), Times.Once);
        }

        // TC07: IsRental = true + TotalStock → tạo thành công với đúng fields
        [Fact]
        public async Task TC07_RentalService_CreatesSuccessfully()
        {
            var station = CreateStation(id: 1, ownerUserId: 1);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            var result = await CreateService().CreateExtraServiceAsync(1, 1,
                CreateValidDto(serviceName: "Xe đẩy", price: 10000, isRental: true));

            Assert.NotNull(result);
            Assert.True(result.IsRental);
            Assert.Equal(10, result.TotalStock);
        }

        // TC08: Description = null → không lỗi, Description trong kết quả là null
        [Fact]
        public async Task TC08_NullDescription_DoesNotThrow()
        {
            var station = CreateStation(id: 1, ownerUserId: 1);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            var result = await CreateService().CreateExtraServiceAsync(1, 1,
                CreateValidDto(description: null));

            Assert.NotNull(result);
            Assert.Null(result.Description);
        }

        // TC09: Price = 0 → hợp lệ (miễn phí)
        [Fact]
        public async Task TC09_ZeroPrice_IsValid()
        {
            var station = CreateStation(id: 1, ownerUserId: 1);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            var result = await CreateService().CreateExtraServiceAsync(1, 1,
                CreateValidDto(price: 0));

            Assert.NotNull(result);
            Assert.Equal(0, result.Price);
        }
    }
}
