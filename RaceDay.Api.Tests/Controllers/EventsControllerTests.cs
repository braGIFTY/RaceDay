using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using Xunit;

namespace RaceDay.Api.Tests.Controllers
{
    public class EventsControllerTests : IClassFixture<RaceDayApiFactory>
    {
        private readonly RaceDayApiFactory _factory;

        public EventsControllerTests(RaceDayApiFactory factory)
        {
            _factory = factory;
        }

        private static CreateEventRequest ValidEventRequest(string name = "Test Event") =>
            new(name, "A test event", new DateTime(2026, 12, 1), "Test City", 10m, "Run");

        [Fact]
        public async Task GetAll_WithoutLogin_ReturnsOk()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/events");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Create_WithoutLogin_ReturnsUnauthorized()
        {
            var client = _factory.CreateClient();

            // Deliberately a VALID body: ASP.NET's automatic validation runs before our
            // auth filter, so an invalid body here would give 400 and hide what we're testing.
            var response = await client.PostAsJsonAsync("/api/events", ValidEventRequest());

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Create_AsParticipant_ReturnsForbidden()
        {
            var (participant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);

            var response = await participant.PostAsJsonAsync("/api/events", ValidEventRequest());

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Create_AsOrganiser_ReturnsCreatedAndOwnedByThatOrganiser()
        {
            var (organiser, user) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);

            var response = await organiser.PostAsJsonAsync("/api/events", ValidEventRequest("Owned Event"));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<EventResponse>();
            Assert.Equal("Owned Event", body!.Name);
            Assert.Equal(user.Id, body.OrganiserId);
        }

        [Fact]
        public async Task Create_WithInvalidEventType_ReturnsBadRequest()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var request = new CreateEventRequest("Bad Event", null, new DateTime(2026, 12, 1), "Test City", 10m, "Swim");

            var response = await organiser.PostAsJsonAsync("/api/events", request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Update_AsOwningOrganiser_ReturnsOkWithUpdatedValues()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var created = await TestHelpers.CreateEventAsync(organiser);
            var update = new UpdateEventRequest("Renamed Event", "Updated", new DateTime(2027, 1, 15), "New City", 21.1m, "Walk");

            var response = await organiser.PutAsJsonAsync($"/api/events/{created.Id}", update);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<EventResponse>();
            Assert.Equal("Renamed Event", body!.Name);
            Assert.Equal("Walk", body.EventType);
        }

        [Fact]
        public async Task Update_AsDifferentOrganiser_ReturnsForbidden()
        {
            var (owner, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var (otherOrganiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var created = await TestHelpers.CreateEventAsync(owner);
            var update = new UpdateEventRequest("Hijacked", null, new DateTime(2027, 1, 15), "Nowhere", 5m, "Run");

            var response = await otherOrganiser.PutAsJsonAsync($"/api/events/{created.Id}", update);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Delete_AsOwningOrganiser_ReturnsNoContentAndEventIsGone()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var created = await TestHelpers.CreateEventAsync(organiser);

            var deleteResponse = await organiser.DeleteAsync($"/api/events/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var getResponse = await organiser.GetAsync($"/api/events/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
        public async Task GetById_WithNonexistentId_ReturnsNotFound()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/events/999999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}