using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using Xunit;

namespace RaceDay.Api.Tests.Controllers
{
    public class AuthControllerTests : IClassFixture<RaceDayApiFactory>
    {
        private readonly HttpClient _client;

        public AuthControllerTests(RaceDayApiFactory factory)
        {
            _client = factory.CreateClient();
        }

        private static string UniqueEmail(string prefix) => $"{prefix}_{Guid.NewGuid():N}@example.com";

        [Fact]
        public async Task RegisterOrganiser_WithValidData_ReturnsCreated()
        {
            var request = new RegisterRequest("Test Organiser", UniqueEmail("org"), "Password123");

            var response = await _client.PostAsJsonAsync("/api/auth/register/organiser", request);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(body);
            Assert.Equal("Organiser", body!.Role);
        }

        [Fact]
        public async Task RegisterOrganiser_WithDuplicateEmail_ReturnsConflict()
        {
            var email = UniqueEmail("dupe");
            var request = new RegisterRequest("First Organiser", email, "Password123");

            var firstResponse = await _client.PostAsJsonAsync("/api/auth/register/organiser", request);
            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

            var secondResponse = await _client.PostAsJsonAsync("/api/auth/register/organiser", request);

            Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        }

        [Fact]
        public async Task RegisterParticipant_WithValidData_ReturnsCreated()
        {
            var request = new RegisterRequest("Test Participant", UniqueEmail("part"), "Password123");

            var response = await _client.PostAsJsonAsync("/api/auth/register/participant", request);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.Equal("Participant", body!.Role);
        }

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsOkWithCorrectRole()
        {
            var email = UniqueEmail("login");
            var registerRequest = new RegisterRequest("Login Test", email, "Password123");
            await _client.PostAsJsonAsync("/api/auth/register/organiser", registerRequest);

            var loginRequest = new LoginRequest(email, "Password123");
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.Equal("Organiser", body!.Role);
        }

        [Fact]
        public async Task Login_WithWrongPassword_ReturnsUnauthorized()
        {
            var email = UniqueEmail("wrongpw");
            var registerRequest = new RegisterRequest("Wrong Password Test", email, "Password123");
            await _client.PostAsJsonAsync("/api/auth/register/organiser", registerRequest);

            var loginRequest = new LoginRequest(email, "ThisIsWrong");
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_WithNonexistentEmail_ReturnsUnauthorized()
        {
            var loginRequest = new LoginRequest(UniqueEmail("ghost"), "Password123");

            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
