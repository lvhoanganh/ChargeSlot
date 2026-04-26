using Moq;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.DTOs.Station;

namespace ChargeSlot.Tests.Services.ChargingStationServiceTests
{
    public class CreatePricingTests : ChargingStationServiceTestBase
    {
        private CreateStationPricingDto CreateValidPricingDto(
            string startTime = "08:00",
            string endTime = "22:00") => new CreateStationPricingDto
            {
                StartTime = startTime,
                EndTime = endTime,
                PricePerHour = 15000,
                Priority = 1
            };

        // TC01: Station không tồn tại → throw KeyNotFoundException
        [Fact]
        public async Task CreatePricing_StationNotFound_Throws()
        {
            _stationRepoMock.Setup(x => x.GetByIdAsync(99, false, true)).ReturnsAsync((ChargingStation?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                CreateService().CreatePricingAsync(99, 1, CreateValidPricingDto()));
        }

        // TC02: Không phải owner → throw UnauthorizedAccessException
        [Fact]
        public async Task CreatePricing_NotOwner_Throws()
        {
            var station = CreateStation(id: 1, ownerUserId: 99);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().CreatePricingAsync(1, 1, CreateValidPricingDto()));
        }

        // TC03: StartTime sai định dạng → throw InvalidOperationException
        [Fact]
        public async Task CreatePricing_InvalidStartTime_Throws()
        {
            var station = CreateStation(id: 1, ownerUserId: 1);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CreatePricingAsync(1, 1, CreateValidPricingDto(startTime: "invalid")));

            Assert.Contains("HH:mm", ex.Message);
        }

        // TC04: EndTime sai định dạng → throw InvalidOperationException
        [Fact]
        public async Task CreatePricing_InvalidEndTime_Throws()
        {
            var station = CreateStation(id: 1, ownerUserId: 1);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CreatePricingAsync(1, 1, CreateValidPricingDto(endTime: "bad-time")));

            Assert.Contains("HH:mm", ex.Message);
        }

        // TC05: Happy path — tạo pricing thành công, return DTO đúng
        [Fact]
        public async Task CreatePricing_ValidData_CreatesPricingAndReturnsDto()
        {
            var station = CreateStation(id: 1, ownerUserId: 1);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            var result = await CreateService().CreatePricingAsync(1, 1, CreateValidPricingDto());

            Assert.NotNull(result);
            Assert.Equal(15000, result.PricePerHour);
            Assert.True(result.IsActive);
            _pricingRepoMock.Verify(x => x.Add(It.IsAny<StationPricing>()), Times.Once);
            _uowMock.Verify(x => x.CompleteAsync(), Times.Once);
        }

        // TC06: Nullable fields (DayOfWeek = null, EffectiveTo = null) → tạo thành công
        [Fact]
        public async Task CreatePricing_NullableFields_CreatesSuccessfully()
        {
            var station = CreateStation(id: 1, ownerUserId: 1);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            var dto = CreateValidPricingDto();
            dto.DayOfWeek = null;
            dto.EffectiveTo = null;

            var result = await CreateService().CreatePricingAsync(1, 1, dto);

            Assert.NotNull(result);
            Assert.Null(result.DayOfWeek);
            Assert.Null(result.EffectiveTo);
            Assert.True(result.IsActive);
        }
    }
}
