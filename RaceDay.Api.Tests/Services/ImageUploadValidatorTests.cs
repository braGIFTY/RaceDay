using RaceDay.Api.Services;
using Xunit;
 
namespace RaceDay.Api.Tests.Services
{
    public class ImageUploadValidatorTests
    {
        private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        private static readonly byte[] WebP = { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
        private static readonly byte[] Wav = { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 };
        private static readonly byte[] Text = System.Text.Encoding.UTF8.GetBytes("this is not an image at all");
 
        [Fact]
        public void Validate_WithPngHeader_ReturnsValidPng()
        {
            var result = ImageUploadValidator.Validate(Png.Length, Png);
 
            Assert.True(result.IsValid);
            Assert.Equal("image/png", result.ContentType);
            Assert.Equal(".png", result.Extension);
        }
 
        [Fact]
        public void Validate_WithJpegHeader_ReturnsValidJpeg()
        {
            var result = ImageUploadValidator.Validate(Jpeg.Length, Jpeg);
 
            Assert.True(result.IsValid);
            Assert.Equal("image/jpeg", result.ContentType);
            Assert.Equal(".jpg", result.Extension);
        }
 
        [Fact]
        public void Validate_WithWebPHeader_ReturnsValidWebP()
        {
            var result = ImageUploadValidator.Validate(WebP.Length, WebP);
 
            Assert.True(result.IsValid);
            Assert.Equal("image/webp", result.ContentType);
            Assert.Equal(".webp", result.Extension);
        }
 
        [Fact]
        public void Validate_WithTextContent_IsRejected()
        {
            var result = ImageUploadValidator.Validate(Text.Length, Text);
 
            Assert.False(result.IsValid);
            Assert.Contains("JPEG, PNG or WebP", result.Error);
        }
 
        [Fact]
        public void Validate_WithEmptyFile_IsRejected()
        {
            var result = ImageUploadValidator.Validate(0, Png);
 
            Assert.False(result.IsValid);
            Assert.NotNull(result.Error);
        }
 
        [Fact]
        public void Validate_AboveTheSizeLimit_IsRejected()
        {
            var result = ImageUploadValidator.Validate(ImageUploadValidator.MaxBytes + 1, Png);
 
            Assert.False(result.IsValid);
            Assert.Contains("5 MB", result.Error);
        }
 
        [Fact]
        public void Validate_ExactlyAtTheSizeLimit_IsAccepted()
        {
            var result = ImageUploadValidator.Validate(ImageUploadValidator.MaxBytes, Png);
 
            Assert.True(result.IsValid);
        }
 
        [Fact]
        public void Validate_WithHeaderShorterThanASignature_IsRejected()
        {
            var tooShort = new byte[] { 0x89, 0x50 };
 
            var result = ImageUploadValidator.Validate(tooShort.Length, tooShort);
 
            Assert.False(result.IsValid);
        }
 
        [Fact]
        public void Validate_WithRiffContainerThatIsNotWebP_IsRejected()
        {
            // A WAV audio file also starts with "RIFF", so the "WEBP" tag has to be checked too
            var result = ImageUploadValidator.Validate(Wav.Length, Wav);
 
            Assert.False(result.IsValid);
        }
    }
}