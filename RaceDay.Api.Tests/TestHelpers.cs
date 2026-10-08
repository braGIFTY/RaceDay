using System.Net.Http.Json;
using RaceDay.Api.DTOs;

namespace RaceDay.Api.Tests
{
    public static class TestHelpers
    {
        public static string UniqueEmail(string prefix) => $"{prefix}_{Guid.NewGuid():N}@example.com";

        // Every call to factory.CreateClient() returns a client with its OWN cookie jar,
        // so each client has its own session. That's how one test can have an organiser,
        // a second organiser, and a participant all "logged in" at the same time.
        public static Task<(HttpClient Client, AuthResponse User)> CreateLoggedInOrganiserAsync(RaceDayApiFactory factory)
            => CreateLoggedInAsync(factory, "organiser");

        public static Task<(HttpClient Client, AuthResponse User)> CreateLoggedInParticipantAsync(RaceDayApiFactory factory)
            => CreateLoggedInAsync(factory, "participant");

        private static async Task<(HttpClient Client, AuthResponse User)> CreateLoggedInAsync(RaceDayApiFactory factory, string role)
        {
            var client = factory.CreateClient();
            var email = UniqueEmail(role);
            const string password = "Password123";

            var registerResponse = await client.PostAsJsonAsync(
                $"/api/auth/register/{role}", new RegisterRequest($"Test {role}", email, password));
            registerResponse.EnsureSuccessStatusCode();
            var user = (await registerResponse.Content.ReadFromJsonAsync<AuthResponse>())!;

            var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
            loginResponse.EnsureSuccessStatusCode();

            return (client, user);
        }

        public static async Task<EventResponse> CreateEventAsync(HttpClient organiserClient, string name = "Test Event")
        {
            var request = new CreateEventRequest(name, "A test event", new DateTime(2026, 12, 1), "Test City", 10m, "Run");
            var response = await organiserClient.PostAsJsonAsync("/api/events", request);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<EventResponse>())!;
        }

        public static async Task<CategoryResponse> CreateCategoryAsync(HttpClient organiserClient, int eventId, string name = "10km")
        {
            var request = new CreateCategoryRequest(name, "A test category");
            var response = await organiserClient.PostAsJsonAsync($"/api/events/{eventId}/categories", request);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<CategoryResponse>())!;
        }
        public static async Task<EnrolmentResponse> CreateEnrolmentAsync(HttpClient participantClient, int eventId, int categoryId)
        {
            var response = await participantClient.PostAsJsonAsync("/api/enrolments", new CreateEnrolmentRequest(eventId, categoryId));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<EnrolmentResponse>())!;
        }

        public static async Task<ResultResponse> CreateResultAsync(HttpClient organiserClient, int enrolmentId, TimeSpan finishTime, int position)
        {
            var response = await organiserClient.PostAsJsonAsync(
                $"/api/enrolments/{enrolmentId}/results", new CreateResultRequest(finishTime, position));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ResultResponse>())!;
        }
    }
}