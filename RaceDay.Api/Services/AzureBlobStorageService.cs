using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
 
namespace RaceDay.Api.Services
{
    public class AzureBlobStorageService : IBlobStorageService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureBlobStorageService> _logger;
        private BlobContainerClient? _container;
 
        public AzureBlobStorageService(IConfiguration configuration, ILogger<AzureBlobStorageService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }
 
        // The connection to Azure is only created the first time it is needed. That way the
        // rest of the API keeps working on a machine with no Azure settings (for example the
        // CI build, or someone running Part 2 for the first time); only uploads need them.
        private BlobContainerClient Container
        {
            get
            {
                if (_container != null)
                    return _container;
 
                var connectionString = _configuration["AzureBlob:ConnectionString"];
                var containerName = _configuration["AzureBlob:ContainerName"];
 
                if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(containerName))
                    throw new BlobStorageNotConfiguredException(
                        "Azure Blob Storage is not configured. Set AzureBlob:ConnectionString and AzureBlob:ContainerName.");
 
                _container = new BlobContainerClient(connectionString, containerName);
                return _container;
            }
        }
 
        public async Task<string> UploadImageAsync(
            Stream content, string contentType, string folder, string extension,
            CancellationToken cancellationToken = default)
        {
            // A random name means two uploads can never overwrite each other
            var blobName = $"{folder}/{Guid.NewGuid():N}{extension}";
            var blob = Container.GetBlobClient(blobName);
 
            var options = new BlobUploadOptions
            {
                // Without the content type the browser would download the file instead of showing it
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            };
 
            await blob.UploadAsync(content, options, cancellationToken);
 
            return blob.Uri.ToString();
        }
 
        public async Task DeleteImageAsync(string imageUrl, CancellationToken cancellationToken = default)
        {
            try
            {
                var containerPrefix = Container.Uri.ToString().TrimEnd('/') + "/";
 
                // Only ever delete things inside our own container
                if (!imageUrl.StartsWith(containerPrefix, StringComparison.OrdinalIgnoreCase))
                    return;
 
                var blobName = Uri.UnescapeDataString(imageUrl.Substring(containerPrefix.Length));
                await Container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                // Cleaning up an old image is a courtesy, so it must never fail the request
                _logger.LogWarning(ex, "Could not delete the old image {ImageUrl}", imageUrl);
            }
        }
    }
}