namespace RaceDay.Api.Services
{
    public record ImageUploadResult(bool Succeeded, string? Url, int StatusCode, string? Error)
    {
        public static ImageUploadResult Success(string url) => new(true, url, StatusCodes.Status200OK, null);
        public static ImageUploadResult Failure(int statusCode, string error) => new(false, null, statusCode, error);
    }
 
    // Both upload endpoints (event banner, profile picture) do exactly the same job: check the
    // file, store it, report what went wrong in plain words. That lives here once instead of
    // being copied into each controller.
    public class ImageUploader
    {
        private readonly IBlobStorageService _storage;
        private readonly ILogger<ImageUploader> _logger;
 
        public ImageUploader(IBlobStorageService storage, ILogger<ImageUploader> logger)
        {
            _storage = storage;
            _logger = logger;
        }
 
        public async Task<ImageUploadResult> UploadAsync(IFormFile? file, string folder, CancellationToken cancellationToken = default)
        {
            if (file == null)
                return ImageUploadResult.Failure(StatusCodes.Status400BadRequest, "No image was uploaded.");
 
            // Read just the first few bytes: enough to tell what the file really is
            byte[] header;
            await using (var headerStream = file.OpenReadStream())
            {
                var buffer = new byte[12];
                var read = await headerStream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
                header = buffer[..read];
            }
 
            var validation = ImageUploadValidator.Validate(file.Length, header);
            if (!validation.IsValid)
                return ImageUploadResult.Failure(StatusCodes.Status400BadRequest, validation.Error!);
 
            try
            {
                await using var uploadStream = file.OpenReadStream();
                var url = await _storage.UploadImageAsync(
                    uploadStream, validation.ContentType!, folder, validation.Extension!, cancellationToken);
                return ImageUploadResult.Success(url);
            }
            catch (BlobStorageNotConfiguredException)
            {
                return ImageUploadResult.Failure(StatusCodes.Status503ServiceUnavailable,
                    "Image storage is not set up on this server.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The detail goes in the server log; the user just gets a plain message
                _logger.LogError(ex, "Uploading an image to blob storage failed");
                return ImageUploadResult.Failure(StatusCodes.Status503ServiceUnavailable,
                    "The image could not be stored right now. Please try again.");
            }
        }
 
        // Used to tidy up the old picture after it has been replaced
        public Task DeleteAsync(string? imageUrl) =>
            string.IsNullOrEmpty(imageUrl) ? Task.CompletedTask : _storage.DeleteImageAsync(imageUrl);
    }
}