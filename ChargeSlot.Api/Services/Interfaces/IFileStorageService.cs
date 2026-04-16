namespace ChargeSlot.Api.Services.Interfaces
{
    /// <summary>
    /// Service upload file lên cloud storage (Firebase Storage).
    /// </summary>
    public interface IFileStorageService
    {
        /// <summary>
        /// Upload file, trả về public URL.
        /// </summary>
        /// <param name="file">File cần upload</param>
        /// <param name="folder">Thư mục trên storage (VD: "avatars/123", "stations/5")</param>
        /// <returns>Public URL của file đã upload</returns>
        Task<string> UploadAsync(IFormFile file, string folder);

        /// <summary>
        Task DeleteAsync(string fileUrl);

        /// <summary>
        /// Upload raw bytes.
        /// </summary>
        Task<string> UploadBytesAsync(byte[] bytes, string folder, string fileName, string contentType);
    }
}
