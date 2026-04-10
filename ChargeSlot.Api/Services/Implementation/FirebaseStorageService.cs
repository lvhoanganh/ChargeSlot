using ChargeSlot.Api.Services.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;

namespace ChargeSlot.Api.Services.Implementation
{
    /// <summary>
    /// Upload/delete file trên Firebase Storage (Google Cloud Storage).
    /// Dùng chung Service Account Key đã có sẵn cho Firebase Auth.
    /// </summary>
    public class FirebaseStorageService : IFileStorageService
    {
        private readonly StorageClient _client;
        private readonly string _bucketName;
        private readonly ILogger<FirebaseStorageService> _logger;

        public FirebaseStorageService(IConfiguration config, ILogger<FirebaseStorageService> logger)
        {
            _logger = logger;

            var keyPath = config["Firebase:ServiceAccountKeyPath"] ?? "firebase-service-account.json";
#pragma warning disable CS0618 // GoogleCredential.FromFile deprecated but CredentialFactory not yet stable in all envs
            var credential = GoogleCredential.FromFile(keyPath);
#pragma warning restore CS0618
            _client = StorageClient.Create(credential);

            _bucketName = config["Firebase:StorageBucket"]
                ?? throw new InvalidOperationException(
                    "Chưa cấu hình Firebase:StorageBucket trong appsettings.json. " +
                    "Giá trị mẫu: chargeslot-42b86.firebasestorage.app");
        }

        public async Task<string> UploadAsync(IFormFile file, string folder)
        {
            // Tạo tên file unique tránh trùng
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var objectName = $"{folder}/{Guid.NewGuid():N}{ext}";

            using var stream = file.OpenReadStream();
            var obj = await _client.UploadObjectAsync(
                _bucketName,
                objectName,
                file.ContentType,
                stream);

            // Firebase Storage public URL format
            // Dùng firebasestorage.googleapis.com — tuân theo Firebase Security Rules (allow read: if true)
            var encodedPath = Uri.EscapeDataString(objectName);
            var publicUrl = $"https://firebasestorage.googleapis.com/v0/b/{_bucketName}/o/{encodedPath}?alt=media";

            _logger.LogInformation("Uploaded {ObjectName} to Firebase Storage ({Size} bytes)", objectName, file.Length);

            return publicUrl;
        }

        public async Task DeleteAsync(string fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl)) return;

            // Parse object name từ Firebase Storage URL
            // Format: https://firebasestorage.googleapis.com/v0/b/{bucket}/o/{encodedPath}?alt=media
            string? objectName = null;

            var firebasePrefix = $"https://firebasestorage.googleapis.com/v0/b/{_bucketName}/o/";
            var storagePrefix = $"https://storage.googleapis.com/{_bucketName}/";

            if (fileUrl.StartsWith(firebasePrefix))
            {
                var encoded = fileUrl[firebasePrefix.Length..];
                // Remove query string (?alt=media)
                var queryIdx = encoded.IndexOf('?');
                if (queryIdx >= 0) encoded = encoded[..queryIdx];
                objectName = Uri.UnescapeDataString(encoded);
            }
            else if (fileUrl.StartsWith(storagePrefix))
            {
                // Backward compat: old format (nếu có URL cũ từ storage.googleapis.com)
                objectName = fileUrl[storagePrefix.Length..];
            }
            else
            {
                // URL cũ từ wwwroot hoặc external → skip
                _logger.LogDebug("Skipping delete for non-Firebase URL: {Url}", fileUrl);
                return;
            }

            try
            {
                await _client.DeleteObjectAsync(_bucketName, objectName);
                _logger.LogInformation("Deleted {ObjectName} from Firebase Storage", objectName);
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("File not found on Firebase Storage (already deleted?): {ObjectName}", objectName);
            }
        }
    }
}
