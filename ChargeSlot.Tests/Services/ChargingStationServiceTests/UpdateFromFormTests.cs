using Moq;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.DTOs.Station;
using Microsoft.AspNetCore.Http;

namespace ChargeSlot.Tests.Services.ChargingStationServiceTests
{
    public class UpdateFromFormTests : ChargingStationServiceTestBase
    {
        private UpdateStationFormDto CreateValidUpdateDto(bool withHours = false) => new UpdateStationFormDto
        {
            Name = "Updated Station",
            Address = "456 Updated St",
            Description = "Updated desc",
            Latitude = 10.5m,
            Longitude = 106.5m,
            LayoutWidth = 900,
            LayoutHeight = 700,
            OperatingHours = withHours ? new List<OperatingHoursFormItem>
            {
                new OperatingHoursFormItem { DayOfWeek = 2, IsClosed = false, OpenTime = "09:00", CloseTime = "21:00" }
            } : null,
            ExistingImageUrls = new List<string>()
        };

        // TC01: Station không tồn tại → throw KeyNotFoundException
        [Fact]
        public async Task UpdateFromForm_StationNotFound_Throws()
        {
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, true, true)).ReturnsAsync((ChargingStation?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                CreateService().UpdateFromFormAsync(1, 1, CreateValidUpdateDto()));
        }

        // TC02: Không phải owner (station.OwnerUserId=99, request userId=1) → throw UnauthorizedAccessException
        [Fact]
        public async Task UpdateFromForm_NotOwner_Throws()
        {
            var station = CreateStation(id: 1, ownerUserId: 99);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, true, true)).ReturnsAsync(station);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().UpdateFromFormAsync(1, 1, CreateValidUpdateDto()));
        }

        // TC03: Update thành công — không ảnh mới, không đổi hours
        [Fact]
        public async Task UpdateFromForm_ValidData_NoImageNoHours_UpdatesSuccessfully()
        {
            var station = CreateStation(id: 1, ownerUserId: 1);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, true, true)).ReturnsAsync(station);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            var result = await CreateService().UpdateFromFormAsync(1, 1, CreateValidUpdateDto());

            Assert.NotNull(result);
            Assert.Equal("Updated Station", result.Name);
        }

        // TC04: Có OperatingHours mới [{DayOfWeek=2, Open="09:00", Close="21:00"}] → xóa cũ, thêm mới
        [Fact]
        public async Task UpdateFromForm_WithNewOperatingHours_ReplacesOldHours()
        {
            var station = CreateStation(id: 1, ownerUserId: 1);
            station.OperatingHours = new List<StationOperatingHours>
            {
                new StationOperatingHours { DayOfWeek = 1, IsClosed = false }
            };
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, true, true)).ReturnsAsync(station);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            await CreateService().UpdateFromFormAsync(1, 1, CreateValidUpdateDto(withHours: true));

            _stationRepoMock.Verify(x => x.RemoveOperatingHours(It.IsAny<IEnumerable<StationOperatingHours>>()), Times.Once);
        }

        // TC05: ExistingImageUrls = [] → xóa tất cả 2 ảnh cũ trên Firebase
        [Fact]
        public async Task UpdateFromForm_NoExistingUrls_DeletesAllOldImages()
        {
            var station = CreateStation(id: 1, ownerUserId: 1);
            station.Images = new List<StationImage>
            {
                new StationImage { Id = 1, ImageUrl = "https://storage/old1.jpg" },
                new StationImage { Id = 2, ImageUrl = "https://storage/old2.jpg" }
            };
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, true, true)).ReturnsAsync(station);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            var dto = CreateValidUpdateDto();
            dto.ExistingImageUrls = new List<string>(); // giữ lại 0 ảnh

            await CreateService().UpdateFromFormAsync(1, 1, dto);

            _fileStorageMock.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Exactly(2));
        }

        // TC06: ExistingImageUrls = [keepUrl] → chỉ xóa removeUrl, giữ keepUrl
        [Fact]
        public async Task UpdateFromForm_PartialKeepImages_OnlyDeletesRemovedImages()
        {
            const string keepUrl = "https://storage/keep.jpg";
            const string removeUrl = "https://storage/remove.jpg";

            var station = CreateStation(id: 1, ownerUserId: 1);
            station.Images = new List<StationImage>
            {
                new StationImage { Id = 1, ImageUrl = keepUrl },
                new StationImage { Id = 2, ImageUrl = removeUrl }
            };
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, true, true)).ReturnsAsync(station);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            var dto = CreateValidUpdateDto();
            dto.ExistingImageUrls = new List<string> { keepUrl };

            await CreateService().UpdateFromFormAsync(1, 1, dto);

            _fileStorageMock.Verify(x => x.DeleteAsync(removeUrl), Times.Once);
            _fileStorageMock.Verify(x => x.DeleteAsync(keepUrl), Times.Never);
        }

        // TC07: Có 1 ảnh mới upload (new.jpg, Length=1024) → UploadAsync được gọi 1 lần
        [Fact]
        public async Task UpdateFromForm_WithNewImages_CallsUpload()
        {
            var station = CreateStation(id: 1, ownerUserId: 1);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, true, true)).ReturnsAsync(station);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            var dto = CreateValidUpdateDto();
            dto.Images = new IFormFile[] { CreateMockFormFile("new.jpg") };

            await CreateService().UpdateFromFormAsync(1, 1, dto);

            _fileStorageMock.Verify(x => x.UploadAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Once);
        }

        // TC08: OperatingHours = null → giữ nguyên giờ cũ, RemoveOperatingHours NOT called
        [Fact]
        public async Task UpdateFromForm_NullOperatingHours_DoesNotReplaceHours()
        {
            var station = CreateStation(id: 1, ownerUserId: 1);
            station.OperatingHours = new List<StationOperatingHours>
            {
                new StationOperatingHours { DayOfWeek = 1, IsClosed = false }
            };
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, true, true)).ReturnsAsync(station);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, false, true)).ReturnsAsync(station);

            var dto = CreateValidUpdateDto(withHours: false); // null hours

            await CreateService().UpdateFromFormAsync(1, 1, dto);

            _stationRepoMock.Verify(x => x.RemoveOperatingHours(It.IsAny<IEnumerable<StationOperatingHours>>()), Times.Never);
        }
    }
}
