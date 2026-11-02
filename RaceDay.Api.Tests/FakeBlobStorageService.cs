using RaceDay.Api.Services;
 
namespace RaceDay.Api.Tests
{
    public record RecordedUpload(string Url, string Folder, string ContentType, string Extension, long Length);
 
    // Stands in for Azure Blob Storage during tests, so they never need an Azure account,
    // a connection string, or internet access. It remembers what was "uploaded" and
    // "deleted" so tests can check the API did the right thing.
    public class FakeBlobStorageService : IBlobStorageService
    {
        public const string BaseUrl = "https://fake-storage.test/raceday-images";
 
        private readonly List<RecordedUpload> _uploads = new();
        private readonly List<string> _deleted = new();
        private readonly object _lock = new();
 
        public IReadOnlyList<RecordedUpload> Uploads
        {
            get { lock (_lock) { return _uploads.ToList(); } }
        }
 
        public IReadOnlyList<string> Deleted
        {
            get { lock (_lock) { return _deleted.ToList(); } }
        }
 
        // One-shot: the next upload throws this, and then it clears itself, so a failure set up
        // by one test can never leak into another.
        public Exception? NextUploadFailure { get; set; }
 
        public async Task<string> UploadImageAsync(
            Stream content, string contentType, string folder, string extension,
            CancellationToken cancellationToken = default)
        {
            var failure = NextUploadFailure;
            if (failure != null)
            {
                NextUploadFailure = null;
                throw failure;
            }
 
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
 
            var url = $"{BaseUrl}/{folder}/{Guid.NewGuid():N}{extension}";
            lock (_lock)
            {
                _uploads.Add(new RecordedUpload(url, folder, contentType, extension, buffer.Length));
            }
 
            return url;
        }
 
        public Task DeleteImageAsync(string imageUrl, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _deleted.Add(imageUrl);
            }
 
            return Task.CompletedTask;
        }
    }
}
 