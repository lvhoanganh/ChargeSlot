using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Kyc;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ChargeSlot.Tests.Services
{
    public class KycServiceTests : System.IDisposable
    {
        private readonly ChargeSlotDbContext _db;
        private readonly IKycService _kycService;
        private readonly Mock<IFileStorageService> _mockFileService;
        private readonly Mock<INotificationService> _mockNotificationService;

        public KycServiceTests()
        {
            _db = TestDbHelper.CreateInMemoryDb();

            _mockFileService = new Mock<IFileStorageService>();
            _mockFileService.Setup(f => f.UploadAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
                .ReturnsAsync("https://mock-url.com/file.jpg");

            _mockNotificationService = new Mock<INotificationService>();

            var loggerMock = new Mock<ILogger<KycService>>();

            _kycService = new KycService(
                _db,
                _mockFileService.Object,
                _mockNotificationService.Object,
                loggerMock.Object
            );
        }

        public void Dispose()
        {
            _db.Database.EnsureDeleted();
            _db.Dispose();
        }

        private IFormFile CreateMockFile(string fileName)
        {
            var content = "Fake image content";
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);
            writer.Write(content);
            writer.Flush();
            ms.Position = 0;

            return new FormFile(ms, 0, ms.Length, "Data", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };
        }

        [Fact]
        public async Task SubmitKycAsync_ValidData_ChangesStatusToPending()
        {
            // Arrange
            var (_, owner) = await TestDbHelper.SeedOwnerAsync(_db, 2);
            owner.KycStatus = KycStatus.Unverified; // Explicitly set it
            await _db.SaveChangesAsync();

            var dto = new SubmitKycDto
            {
                IdCardNumber = "012345678912",
                IdCardDate = "01/01/2020",
                BusinessName = "Test Business",
                BusinessLicenseNumber = "12345",
                TaxCode = "MST123",
                Address = "Hanoi",
                FrontIdCardImage = CreateMockFile("front.jpg"),
                BackIdCardImage = CreateMockFile("back.png"),
                BusinessLicenseImage = CreateMockFile("license.webp")
            };

            // Act
            var result = await _kycService.SubmitKycAsync(2, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Pending", result.KycStatus);
            
            var dbOwner = await _db.Owner.FindAsync(2);
            Assert.Equal(KycStatus.Pending, dbOwner.KycStatus);
            Assert.Equal("012345678912", dbOwner.IdCardNumber);
            Assert.Equal("https://mock-url.com/file.jpg", dbOwner.FrontIdCardUrl);
        }

        [Fact]
        public async Task ReviewKycAsync_Approve_ChangesStatusToApproved()
        {
            // Arrange
            var (_, owner) = await TestDbHelper.SeedOwnerAsync(_db, 2);
            owner.KycStatus = KycStatus.Pending;
            await _db.SaveChangesAsync();

            var dto = new ReviewKycDto
            {
                IsApproved = true
            };

            // Act
            var result = await _kycService.ReviewKycAsync(1, 2, dto);

            // Assert
            Assert.Equal("Approved", result.KycStatus);
            
            var dbOwner = await _db.Owner.FindAsync(2);
            Assert.Equal(KycStatus.Approved, dbOwner.KycStatus);
            Assert.Equal(1, dbOwner.KycReviewedByUserId);
            
            // Verify notification sent
            _mockNotificationService.Verify(n => n.SendAsync(2, It.IsAny<string>(), It.IsAny<string>(), NotificationType.System), Times.Once);
        }

        [Fact]
        public async Task ReviewKycAsync_Reject_ChangesStatusToRejected()
        {
            // Arrange
            var (_, owner) = await TestDbHelper.SeedOwnerAsync(_db, 2);
            owner.KycStatus = KycStatus.Pending;
            owner.IdCardNumber = "test"; // Provide required seed if any
            await _db.SaveChangesAsync();

            var dto = new ReviewKycDto
            {
                IsApproved = false,
                RejectReason = "Mờ quá"
            };

            // Act
            var result = await _kycService.ReviewKycAsync(1, 2, dto);

            // Assert
            Assert.Equal("Rejected", result.KycStatus);
            Assert.Equal("Mờ quá", result.KycRejectReason);
            
            var dbOwner = await _db.Owner.FindAsync(2);
            Assert.Equal(KycStatus.Rejected, dbOwner.KycStatus);
        }

        [Fact]
        public async Task ReviewKycAsync_NotPending_ThrowsException()
        {
            // Arrange
            var (_, owner) = await TestDbHelper.SeedOwnerAsync(_db, 2);
            owner.KycStatus = KycStatus.Unverified; // Need to be pending to evaluate
            await _db.SaveChangesAsync();

            var dto = new ReviewKycDto { IsApproved = true };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<System.InvalidOperationException>(() => _kycService.ReviewKycAsync(1, 2, dto));
            Assert.Contains("không ở trạng thái chờ duyệt", ex.Message);
        }
    }
}
