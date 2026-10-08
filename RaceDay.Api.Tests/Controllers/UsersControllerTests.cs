using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using Xunit;

namespace RaceDay.Api.Tests.Controllers
{
    public class UsersControllerTests : IClassFixture<RaceDayApiFactory>
    {
        private readonly RaceDayApiFactory _factory;

        public UsersControllerTests(RaceDayApiFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetMe_WithoutLogin_ReturnsUnauthorized()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetMe_AsOrganiser_ReturnsTheOrganisersOwnProfile()
        {
            var (organiser, user) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);

            var response = await organiser.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ProfileResponse>();
            Assert.Equal("Organiser", body!.Role);
            Assert.Equal(user.Id, body.Id);
            Assert.Equal(user.Email, body.Email);
        }

        [Fact]
        public async Task GetMe_AsParticipant_ReturnsTheParticipantsOwnProfile()
        {
            var (participant, user) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);

            var response = await participant.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ProfileResponse>();
            Assert.Equal("Participant", body!.Role);
            Assert.Equal(user.Id, body.Id);
            Assert.Equal(user.Email, body.Email);
        }

        [Fact]
        public async Task UpdateMe_WithoutLogin_ReturnsUnauthorized()
        {
            var client = _factory.CreateClient();

            var response = await client.PutAsJsonAsync("/api/users/me",
                new UpdateProfileRequest("Nobody", null, TestHelpers.UniqueEmail("nobody")));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task UpdateMe_WithNewDetails_ReturnsOkWithUpdatedValues()
        {
            var (participant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            var newEmail = TestHelpers.UniqueEmail("renamed");

            var response = await participant.PutAsJsonAsync("/api/users/me",
                new UpdateProfileRequest("Renamed Participant", "0821234567", newEmail));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ProfileResponse>();
            Assert.Equal("Renamed Participant", body!.FullName);
            Assert.Equal("0821234567", body.Phone);
            Assert.Equal(newEmail, body.Email);
        }

        [Fact]
        public async Task UpdateMe_KeepingOwnEmail_ReturnsOk()
        {
            var (organiser, user) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);

            // Saving a profile without changing the email must not conflict with itself
            var response = await organiser.PutAsJsonAsync("/api/users/me",
                new UpdateProfileRequest("Same Email Different Name", null, user.Email));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task UpdateMe_AsOrganiser_WithAParticipantsEmail_ReturnsConflict()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var (_, participantUser) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);

            var response = await organiser.PutAsJsonAsync("/api/users/me",
                new UpdateProfileRequest("Organiser", null, participantUser.Email));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task UpdateMe_AsParticipant_WithAnOrganisersEmail_ReturnsConflict()
        {
            var (participant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            var (_, organiserUser) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);

            var response = await participant.PutAsJsonAsync("/api/users/me",
                new UpdateProfileRequest("Participant", null, organiserUser.Email));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task UpdateMe_AsOrganiser_WithAnotherOrganisersEmail_ReturnsConflict()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var (_, otherOrganiserUser) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);

            var response = await organiser.PutAsJsonAsync("/api/users/me",
                new UpdateProfileRequest("Organiser", null, otherOrganiserUser.Email));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
    }
}