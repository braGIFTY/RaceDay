using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using RaceDay.Api.DTOs;
using RaceDay.Api.Services;
using Xunit;
 
namespace RaceDay.Api.Tests.Controllers
{
    public class ImageUploadTests : IClassFixture<RaceDayApiFactory>
    {
        private readonly RaceDayApiFactory _factory;
 
        public ImageUploadTests(RaceDayApiFactory factory)
        {
            _factory = factory;
        }
 
        // Just enough of each file type's first bytes for the API to recognise it
        private static readonly byte[] PngBytes =
            { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };
 
        private static readonly byte[] JpegBytes =
            { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
 
        private static readonly byte[] WebpBytes =
            { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20 };
 
        private static MultipartFormDataContent FileForm(
            byte[] bytes, string fileName = "picture.png", string contentType = "image/png")
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(file, "file", fileName);
            return form;
        }
 
        private static async Task<EventResponse> UploadBannerAsync(HttpClient organiser, int eventId, byte[]? bytes = null)
        {
            var response = await organiser.PostAsync($"/api/events/{eventId}/banner", FileForm(bytes ?? PngBytes));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<EventResponse>())!;
        }
 
        private static async Task<ProfileResponse> UploadProfileImageAsync(HttpClient user, byte[] bytes, string fileName, string contentType)
        {
            var response = await user.PostAsync("/api/users/me/profile-image", FileForm(bytes, fileName, contentType));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ProfileResponse>())!;
        }
 
        // ---------- Event banners ----------
 
        [Fact]
        public async Task UploadBanner_WithoutLogin_ReturnsUnauthorized()
        {
            var anonymous = _factory.CreateClient();
 
            var response = await anonymous.PostAsync("/api/events/1/banner", FileForm(PngBytes));
 
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
 
        [Fact]
        public async Task UploadBanner_AsParticipant_ReturnsForbidden()
        {
            var (participant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
 
            var response = await participant.PostAsync("/api/events/1/banner", FileForm(PngBytes));
 
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
 
        [Fact]
        public async Task UploadBanner_AsDifferentOrganiser_ReturnsForbidden()
        {
            var (owner, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var (otherOrganiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(owner);
 
            var response = await otherOrganiser.PostAsync($"/api/events/{ev.Id}/banner", FileForm(PngBytes));
 
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
 
        [Fact]
        public async Task UploadBanner_ForNonexistentEvent_ReturnsNotFound()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
 
            var response = await organiser.PostAsync("/api/events/999999/banner", FileForm(PngBytes));
 
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
 
        [Fact]
        public async Task UploadBanner_AsOwningOrganiser_ReturnsOkAndSavesTheImageUrl()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
 
            var response = await organiser.PostAsync($"/api/events/{ev.Id}/banner", FileForm(PngBytes));
 
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<EventResponse>();
            Assert.StartsWith(FakeBlobStorageService.BaseUrl + "/events/", body!.BannerImageUrl);
            Assert.EndsWith(".png", body.BannerImageUrl);
 
            var upload = Assert.Single(_factory.BlobStorage.Uploads, u => u.Url == body.BannerImageUrl);
            Assert.Equal("image/png", upload.ContentType);
            Assert.Equal((long)PngBytes.Length, upload.Length);
 
            // The event itself now remembers the banner
            var fetched = await organiser.GetFromJsonAsync<EventResponse>($"/api/events/{ev.Id}");
            Assert.Equal(body.BannerImageUrl, fetched!.BannerImageUrl);
        }
 
        [Fact]
        public async Task UploadBanner_WithoutAFile_ReturnsBadRequest()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            var formWithNoFile = new MultipartFormDataContent { { new StringContent("hello"), "note" } };
 
            var response = await organiser.PostAsync($"/api/events/{ev.Id}/banner", formWithNoFile);
 
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
 
        [Fact]
        public async Task UploadBanner_WithATextFilePretendingToBeAnImage_ReturnsBadRequest()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            var uploadsBefore = _factory.BlobStorage.Uploads.Count;
            var fakeImage = Encoding.UTF8.GetBytes("this is definitely not a picture");
 
            // Named .png and labelled image/png, but the contents say otherwise
            var response = await organiser.PostAsync(
                $"/api/events/{ev.Id}/banner", FileForm(fakeImage, "banner.png", "image/png"));
 
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(uploadsBefore, _factory.BlobStorage.Uploads.Count);
        }
 
        [Fact]
        public async Task UploadBanner_OverTheSizeLimit_ReturnsBadRequest()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            var uploadsBefore = _factory.BlobStorage.Uploads.Count;
            var tooBig = new byte[ImageUploadValidator.MaxBytes + 1];
            PngBytes.CopyTo(tooBig, 0);
 
            var response = await organiser.PostAsync($"/api/events/{ev.Id}/banner", FileForm(tooBig));
 
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(uploadsBefore, _factory.BlobStorage.Uploads.Count);
        }
 
        [Fact]
        public async Task UploadBanner_Replacing_DeletesTheOldBanner()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            var first = await UploadBannerAsync(organiser, ev.Id);
 
            var second = await UploadBannerAsync(organiser, ev.Id, JpegBytes);
 
            Assert.NotEqual(first.BannerImageUrl, second.BannerImageUrl);
            Assert.Contains(first.BannerImageUrl!, _factory.BlobStorage.Deleted);
            Assert.DoesNotContain(second.BannerImageUrl!, _factory.BlobStorage.Deleted);
        }
 
        [Fact]
        public async Task UploadBanner_WhenStorageFails_ReturnsServiceUnavailableAndLeavesTheEventUnchanged()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            _factory.BlobStorage.NextUploadFailure = new InvalidOperationException("storage exploded");
 
            var response = await organiser.PostAsync($"/api/events/{ev.Id}/banner", FileForm(PngBytes));
 
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var fetched = await organiser.GetFromJsonAsync<EventResponse>($"/api/events/{ev.Id}");
            Assert.Null(fetched!.BannerImageUrl);
        }
 
        [Fact]
        public async Task UploadBanner_WhenStorageIsNotConfigured_ReturnsServiceUnavailable()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            _factory.BlobStorage.NextUploadFailure = new BlobStorageNotConfiguredException("not configured");
 
            var response = await organiser.PostAsync($"/api/events/{ev.Id}/banner", FileForm(PngBytes));
 
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
 
        [Fact]
        public async Task DeleteEvent_WithABanner_RemovesTheBannerFromStorage()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            var withBanner = await UploadBannerAsync(organiser, ev.Id);
 
            var response = await organiser.DeleteAsync($"/api/events/{ev.Id}");
 
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Contains(withBanner.BannerImageUrl!, _factory.BlobStorage.Deleted);
        }
 
        // ---------- Profile pictures ----------
 
        [Fact]
        public async Task UploadProfileImage_WithoutLogin_ReturnsUnauthorized()
        {
            var anonymous = _factory.CreateClient();
 
            var response = await anonymous.PostAsync("/api/users/me/profile-image", FileForm(PngBytes));
 
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
 
        [Fact]
        public async Task UploadProfileImage_AsParticipant_ReturnsOkAndShowsOnTheProfile()
        {
            var (participant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
 
            var uploaded = await UploadProfileImageAsync(participant, JpegBytes, "me.jpg", "image/jpeg");
 
            Assert.StartsWith(FakeBlobStorageService.BaseUrl + "/profiles/", uploaded.ProfileImageUrl);
            Assert.EndsWith(".jpg", uploaded.ProfileImageUrl);
            var profile = await participant.GetFromJsonAsync<ProfileResponse>("/api/users/me");
            Assert.Equal(uploaded.ProfileImageUrl, profile!.ProfileImageUrl);
        }
 
        [Fact]
        public async Task UploadProfileImage_AsOrganiser_ReturnsOk()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
 
            var uploaded = await UploadProfileImageAsync(organiser, WebpBytes, "me.webp", "image/webp");
 
            Assert.Equal("Organiser", uploaded.Role);
            Assert.EndsWith(".webp", uploaded.ProfileImageUrl);
        }
 
        [Fact]
        public async Task UploadProfileImage_WithANonImage_ReturnsBadRequest()
        {
            var (participant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            var notAnImage = Encoding.UTF8.GetBytes("just some text pretending to be a photo");
 
            var response = await participant.PostAsync(
                "/api/users/me/profile-image", FileForm(notAnImage, "me.jpg", "image/jpeg"));
 
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
 
        [Fact]
        public async Task UploadProfileImage_Replacing_DeletesTheOldPicture()
        {
            var (participant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            var first = await UploadProfileImageAsync(participant, PngBytes, "one.png", "image/png");
 
            var second = await UploadProfileImageAsync(participant, JpegBytes, "two.jpg", "image/jpeg");
 
            Assert.NotEqual(first.ProfileImageUrl, second.ProfileImageUrl);
            Assert.Contains(first.ProfileImageUrl!, _factory.BlobStorage.Deleted);
            Assert.DoesNotContain(second.ProfileImageUrl!, _factory.BlobStorage.Deleted);
        }
    }
}