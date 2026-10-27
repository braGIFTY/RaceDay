namespace RaceDay.Api.Services
{
    public record ImageValidationResult(bool IsValid, string? Error, string? ContentType, string? Extension);
 
    public static class ImageUploadValidator
    {
        public const long MaxBytes = 5 * 1024 * 1024;
 
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] JpegSignature = { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] RiffTag = { 0x52, 0x49, 0x46, 0x46 };   // "RIFF"
        private static readonly byte[] WebpTag = { 0x57, 0x45, 0x42, 0x50 };   // "WEBP"
 
        // The file's own first bytes decide what it is, not its name or the content type
        // the client claimed, so a renamed document or program can't be uploaded as a picture.
        public static ImageValidationResult Validate(long length, byte[] header)
        {
            if (length <= 0)
                return Fail("No image was uploaded.");
 
            if (length > MaxBytes)
                return Fail("The image must be 5 MB or smaller.");
 
            if (StartsWith(header, PngSignature))
                return new ImageValidationResult(true, null, "image/png", ".png");
 
            if (StartsWith(header, JpegSignature))
                return new ImageValidationResult(true, null, "image/jpeg", ".jpg");
 
            if (IsWebP(header))
                return new ImageValidationResult(true, null, "image/webp", ".webp");
 
            return Fail("Only JPEG, PNG or WebP images are allowed.");
        }
 
        private static bool StartsWith(byte[] data, byte[] signature) =>
            data.Length >= signature.Length && data.AsSpan(0, signature.Length).SequenceEqual(signature);
 
        // WebP files start with "RIFF", then a 4-byte size, then "WEBP"
        private static bool IsWebP(byte[] data) =>
            data.Length >= 12
            && data.AsSpan(0, 4).SequenceEqual(RiffTag)
            && data.AsSpan(8, 4).SequenceEqual(WebpTag);
 
        private static ImageValidationResult Fail(string error) => new(false, error, null, null);
    }
}