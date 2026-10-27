namespace RaceDay.Api.Services
{
    public interface IBlobStorageService
    {
        // Uploads an image and returns its public URL.
        Task<string> UploadImageAsync(
            Stream content, string contentType, string folder, string extension,
            CancellationToken cancellationToken = default);
 
        // Deletes an image that was uploaded earlier. Best effort: does nothing if the
        // URL isn't in our container or the image is already gone.
        Task DeleteImageAsync(string imageUrl, CancellationToken cancellationToken = default);
    }
 
    // Thrown when an upload is attempted but no storage connection has been set up.
    public class BlobStorageNotConfiguredException : InvalidOperationException
    {
        public BlobStorageNotConfiguredException(string message) : base(message) { }
    }
}