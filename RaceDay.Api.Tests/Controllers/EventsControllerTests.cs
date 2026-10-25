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
 
        // Every test in this class shares one in-memory database, so the filter tests put a
        // unique token in their event names and search for it. That isolates each test's
        // events from everyone else's, however many events other tests have created.
        private static string Token() => Guid.NewGuid().ToString("N")[..8];
 
        private static async Task<EventResponse> CreateCustomEventAsync(
            HttpClient organiser, string name, string location, decimal distanceKm, string eventType, DateTime eventDate)
        {
            var request = new CreateEventRequest(name, "A test event", eventDate, location, distanceKm, eventType);
            var response = await organiser.PostAsJsonAsync("/api/events", request);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<EventResponse>())!;
        }
 
        [Fact]
        public async Task GetAll_WithoutLogin_ReturnsOk()
        {
            var client = _factory.CreateClient();
 
            var response = await client.GetAsync("/api/events");
 
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
 
        [Fact]
        public async Task GetAll_WithUpcomingTrue_ReturnsOnlyEventsFromTodayOnwards()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var token = Token();
            await CreateCustomEventAsync(organiser, $"{token} Past", "Test City", 10m, "Run", DateTime.Today.AddDays(-5));
            var today = await CreateCustomEventAsync(organiser, $"{token} Today", "Test City", 10m, "Run", DateTime.Today);
            var future = await CreateCustomEventAsync(organiser, $"{token} Future", "Test City", 10m, "Run", DateTime.Today.AddDays(5));
            var anonymous = _factory.CreateClient();
 
            var response = await anonymous.GetAsync($"/api/events?upcoming=true&search={token}");
 
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var list = await response.Content.ReadFromJsonAsync<List<EventResponse>>();
            Assert.Equal(new[] { today.Id, future.Id }, list!.Select(e => e.Id).ToArray());
        }
 
        [Fact]
        public async Task GetAll_WithType_ReturnsOnlyThatTypeIgnoringCase()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var token = Token();
            await CreateCustomEventAsync(organiser, $"{token} Run", "Test City", 10m, "Run", DateTime.Today.AddDays(3));
            var walk = await CreateCustomEventAsync(organiser, $"{token} Walk", "Test City", 5m, "Walk", DateTime.Today.AddDays(3));
            await CreateCustomEventAsync(organiser, $"{token} Cycle", "Test City", 50m, "Cycle", DateTime.Today.AddDays(3));
            var anonymous = _factory.CreateClient();
 
            // lower-case "walk" on purpose: the filter shouldn't care about case
            var response = await anonymous.GetAsync($"/api/events?type=walk&search={token}");
 
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var list = await response.Content.ReadFromJsonAsync<List<EventResponse>>();
            var only = Assert.Single(list!);
            Assert.Equal(walk.Id, only.Id);
        }
 
        [Fact]
        public async Task GetAll_WithInvalidType_ReturnsBadRequest()
        {
            var anonymous = _factory.CreateClient();
 
            var response = await anonymous.GetAsync("/api/events?type=Swim");
 
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
 
        [Fact]
        public async Task GetAll_WithSearch_MatchesNameOrLocationIgnoringCase()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var token = Token();
            var byName = await CreateCustomEventAsync(organiser, $"{token} Marathon", "Somewhere", 42.2m, "Run", DateTime.Today.AddDays(3));
            var byLocation = await CreateCustomEventAsync(organiser, "Plain Name", $"{token} Park", 10m, "Run", DateTime.Today.AddDays(3));
            await CreateCustomEventAsync(organiser, "Unrelated", "Elsewhere", 10m, "Run", DateTime.Today.AddDays(3));
            var anonymous = _factory.CreateClient();
 
            // upper-case search for lower-case text: the match shouldn't care about case
            var response = await anonymous.GetAsync($"/api/events?search={token.ToUpper()}");
 
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var list = await response.Content.ReadFromJsonAsync<List<EventResponse>>();
            var ids = list!.Select(e => e.Id).ToHashSet();
            Assert.Equal(2, ids.Count);
            Assert.Contains(byName.Id, ids);
            Assert.Contains(byLocation.Id, ids);
        }
 
        [Fact]
        public async Task GetAll_WithDistanceRange_ReturnsOnlyEventsInRangeInclusive()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var token = Token();
            await CreateCustomEventAsync(organiser, $"{token} Short", "Test City", 5m, "Run", DateTime.Today.AddDays(3));
            var ten = await CreateCustomEventAsync(organiser, $"{token} Ten", "Test City", 10m, "Run", DateTime.Today.AddDays(3));
            var half = await CreateCustomEventAsync(organiser, $"{token} Half", "Test City", 21.1m, "Run", DateTime.Today.AddDays(3));
            await CreateCustomEventAsync(organiser, $"{token} Full", "Test City", 42.2m, "Run", DateTime.Today.AddDays(3));
            var anonymous = _factory.CreateClient();
 
            var response = await anonymous.GetAsync($"/api/events?minDistance=10&maxDistance=25&search={token}");
 
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var list = await response.Content.ReadFromJsonAsync<List<EventResponse>>();
            Assert.Equal(new[] { ten.Id, half.Id }, list!.Select(e => e.Id).ToArray());
        }
 
        [Fact]
        public async Task GetAll_WithMinDistanceAboveMaxDistance_ReturnsBadRequest()
        {
            var anonymous = _factory.CreateClient();
 
            var response = await anonymous.GetAsync("/api/events?minDistance=20&maxDistance=10");
 
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
 
        [Fact]
        public async Task GetAll_WithCombinedFilters_AppliesAllOfThem()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var token = Token();
            await CreateCustomEventAsync(organiser, $"{token} Past Run", "Test City", 10m, "Run", DateTime.Today.AddDays(-3));
            var futureRun = await CreateCustomEventAsync(organiser, $"{token} Future Run", "Test City", 10m, "Run", DateTime.Today.AddDays(3));
            await CreateCustomEventAsync(organiser, $"{token} Future Walk", "Test City", 10m, "Walk", DateTime.Today.AddDays(3));
            var anonymous = _factory.CreateClient();
 
            var response = await anonymous.GetAsync($"/api/events?upcoming=true&type=Run&search={token}");
 
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var list = await response.Content.ReadFromJsonAsync<List<EventResponse>>();
            var only = Assert.Single(list!);
            Assert.Equal(futureRun.Id, only.Id);
        }
 
        [Fact]
        public async Task GetAll_ReturnsEventsEarliestFirst()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var token = Token();
            var inTenDays = await CreateCustomEventAsync(organiser, $"{token} C", "Test City", 10m, "Run", DateTime.Today.AddDays(10));
            var inTwoDays = await CreateCustomEventAsync(organiser, $"{token} A", "Test City", 10m, "Run", DateTime.Today.AddDays(2));
            var inSixDays = await CreateCustomEventAsync(organiser, $"{token} B", "Test City", 10m, "Run", DateTime.Today.AddDays(6));
            var anonymous = _factory.CreateClient();
 
            var response = await anonymous.GetAsync($"/api/events?search={token}");
 
            var list = await response.Content.ReadFromJsonAsync<List<EventResponse>>();
            Assert.Equal(new[] { inTwoDays.Id, inSixDays.Id, inTenDays.Id }, list!.Select(e => e.Id).ToArray());
        }
 
        [Fact]
        public async Task GetMine_WithoutLogin_ReturnsUnauthorized()
        {
            var anonymous = _factory.CreateClient();
 
            var response = await anonymous.GetAsync("/api/events/mine");
 
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
 
        [Fact]
        public async Task GetMine_AsParticipant_ReturnsForbidden()
        {
            var (participant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
 
            var response = await participant.GetAsync("/api/events/mine");
 
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
 
        [Fact]
        public async Task GetMine_ReturnsOnlyTheCurrentOrganisersEventsWithActiveEnrolmentCounts()
        {
            var (organiserA, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var (organiserB, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var busyEvent = await TestHelpers.CreateEventAsync(organiserA, "Busy Event");
            var quietEvent = await TestHelpers.CreateEventAsync(organiserA, "Quiet Event");
            await TestHelpers.CreateEventAsync(organiserB, "Someone Else's Event");
            var category = await TestHelpers.CreateCategoryAsync(organiserA, busyEvent.Id);
            var (stayer, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            var (leaver, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            await TestHelpers.CreateEnrolmentAsync(stayer, busyEvent.Id, category.Id);
            var leaverEnrolment = await TestHelpers.CreateEnrolmentAsync(leaver, busyEvent.Id, category.Id);
            await leaver.DeleteAsync($"/api/enrolments/{leaverEnrolment.Id}"); // cancelled enrolments must not be counted
 
            var response = await organiserA.GetAsync("/api/events/mine");
 
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var list = await response.Content.ReadFromJsonAsync<List<OrganiserEventSummary>>();
            Assert.Equal(2, list!.Count);
            Assert.Equal(1, list.Single(e => e.Id == busyEvent.Id).EnrolmentCount);
            Assert.Equal(0, list.Single(e => e.Id == quietEvent.Id).EnrolmentCount);
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
 