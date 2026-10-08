using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using Xunit;

namespace RaceDay.Api.Tests.Controllers
{
    public class CategoriesControllerTests : IClassFixture<RaceDayApiFactory>
    {
        private readonly RaceDayApiFactory _factory;

        public CategoriesControllerTests(RaceDayApiFactory factory)
        {
            _factory = factory;
        }

        private static CreateCategoryRequest ValidCategoryRequest(string name = "10km") =>
            new(name, "A test category");

        [Fact]
        public async Task GetByEvent_WithoutLogin_ReturnsOkWithTheEventsCategories()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            await TestHelpers.CreateCategoryAsync(organiser, ev.Id, "10km");
            await TestHelpers.CreateCategoryAsync(organiser, ev.Id, "21km");
            var anonymous = _factory.CreateClient();

            var response = await anonymous.GetAsync($"/api/events/{ev.Id}/categories");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var list = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>();
            Assert.Equal(2, list!.Count);
        }

        [Fact]
        public async Task GetByEvent_WithNonexistentEvent_ReturnsNotFound()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/events/999999/categories");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Create_WithoutLogin_ReturnsUnauthorized()
        {
            var client = _factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/events/1/categories", ValidCategoryRequest());

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Create_AsParticipant_ReturnsForbidden()
        {
            var (participant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);

            var response = await participant.PostAsJsonAsync("/api/events/1/categories", ValidCategoryRequest());

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Create_AsOwningOrganiser_ReturnsCreatedAndLinkedToTheEvent()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);

            var response = await organiser.PostAsJsonAsync($"/api/events/{ev.Id}/categories", ValidCategoryRequest("Under 20"));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<CategoryResponse>();
            Assert.Equal("Under 20", body!.Name);
            Assert.Equal(ev.Id, body.EventId);
        }

        [Fact]
        public async Task Create_AsDifferentOrganiser_ReturnsForbidden()
        {
            var (owner, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var (otherOrganiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(owner);

            var response = await otherOrganiser.PostAsJsonAsync($"/api/events/{ev.Id}/categories", ValidCategoryRequest());

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Create_WithNonexistentEvent_ReturnsNotFound()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);

            var response = await organiser.PostAsJsonAsync("/api/events/999999/categories", ValidCategoryRequest());

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Create_WithDuplicateNameOnSameEvent_ReturnsConflict()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            await TestHelpers.CreateCategoryAsync(organiser, ev.Id, "10km");

            var response = await organiser.PostAsJsonAsync($"/api/events/{ev.Id}/categories", ValidCategoryRequest("10km"));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task Create_SameNameOnDifferentEvents_ReturnsCreated()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var eventOne = await TestHelpers.CreateEventAsync(organiser, "Event One");
            var eventTwo = await TestHelpers.CreateEventAsync(organiser, "Event Two");
            await TestHelpers.CreateCategoryAsync(organiser, eventOne.Id, "10km");

            // Category names only need to be unique within an event, not system-wide
            var response = await organiser.PostAsJsonAsync($"/api/events/{eventTwo.Id}/categories", ValidCategoryRequest("10km"));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task Update_AsOwningOrganiser_ReturnsOkWithUpdatedValues()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            var category = await TestHelpers.CreateCategoryAsync(organiser, ev.Id, "10km");

            var response = await organiser.PutAsJsonAsync(
                $"/api/categories/{category.Id}", new UpdateCategoryRequest("21km", "Updated description"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<CategoryResponse>();
            Assert.Equal("21km", body!.Name);
            Assert.Equal("Updated description", body.Description);
        }

        [Fact]
        public async Task Update_AsDifferentOrganiser_ReturnsForbidden()
        {
            var (owner, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var (otherOrganiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(owner);
            var category = await TestHelpers.CreateCategoryAsync(owner, ev.Id);

            var response = await otherOrganiser.PutAsJsonAsync(
                $"/api/categories/{category.Id}", new UpdateCategoryRequest("Hijacked", null));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Delete_AsOwningOrganiser_ReturnsNoContentAndCategoryIsGone()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            var category = await TestHelpers.CreateCategoryAsync(organiser, ev.Id);

            var deleteResponse = await organiser.DeleteAsync($"/api/categories/{category.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var listResponse = await organiser.GetAsync($"/api/events/{ev.Id}/categories");
            var list = await listResponse.Content.ReadFromJsonAsync<List<CategoryResponse>>();
            Assert.Empty(list!);
        }

        [Fact]
        public async Task Delete_AsDifferentOrganiser_ReturnsForbidden()
        {
            var (owner, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var (otherOrganiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(owner);
            var category = await TestHelpers.CreateCategoryAsync(owner, ev.Id);

            var response = await otherOrganiser.DeleteAsync($"/api/categories/{category.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}