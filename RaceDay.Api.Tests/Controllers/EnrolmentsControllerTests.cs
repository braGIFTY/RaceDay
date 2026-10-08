using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using Xunit;

namespace RaceDay.Api.Tests.Controllers
{
    public class EnrolmentsControllerTests : IClassFixture<RaceDayApiFactory>
    {
        private readonly RaceDayApiFactory _factory;

        public EnrolmentsControllerTests(RaceDayApiFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Create_WithoutLogin_ReturnsUnauthorized()
        {
            var client = _factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/enrolments", new CreateEnrolmentRequest(1, 1));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Create_AsOrganiser_ReturnsForbidden()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);

            var response = await organiser.PostAsJsonAsync("/api/enrolments", new CreateEnrolmentRequest(1, 1));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Create_AsParticipant_ReturnsCreatedAndRecordsParticipantEventAndCategory()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            var category = await TestHelpers.CreateCategoryAsync(organiser, ev.Id);
            var (participant, user) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);

            var response = await participant.PostAsJsonAsync("/api/enrolments", new CreateEnrolmentRequest(ev.Id, category.Id));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<EnrolmentResponse>();
            Assert.Equal(user.Id, body!.ParticipantId);
            Assert.Equal(ev.Id, body.EventId);
            Assert.Equal(category.Id, body.CategoryId);
            Assert.Equal("Confirmed", body.Status);
        }

        [Fact]
        public async Task Create_SecondEnrolmentInSameEvent_ReturnsConflict()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            var categoryA = await TestHelpers.CreateCategoryAsync(organiser, ev.Id, "10km");
            var categoryB = await TestHelpers.CreateCategoryAsync(organiser, ev.Id, "21km");
            var (participant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            await TestHelpers.CreateEnrolmentAsync(participant, ev.Id, categoryA.Id);

            // Different category, same event: still a conflict (one event, one entry)
            var response = await participant.PostAsJsonAsync("/api/enrolments", new CreateEnrolmentRequest(ev.Id, categoryB.Id));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task Create_WithNonexistentEvent_ReturnsNotFound()
        {
            var (participant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);

            var response = await participant.PostAsJsonAsync("/api/enrolments", new CreateEnrolmentRequest(999999, 1));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Create_WithCategoryFromDifferentEvent_ReturnsNotFound()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var eventOne = await TestHelpers.CreateEventAsync(organiser, "Event One");
            var eventTwo = await TestHelpers.CreateEventAsync(organiser, "Event Two");
            var categoryOfEventTwo = await TestHelpers.CreateCategoryAsync(organiser, eventTwo.Id);
            var (participant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);

            // A real event and a real category, but the category belongs to the OTHER event
            var response = await participant.PostAsJsonAsync("/api/enrolments", new CreateEnrolmentRequest(eventOne.Id, categoryOfEventTwo.Id));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetMine_ReturnsOnlyTheCurrentParticipantsEnrolments()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            var category = await TestHelpers.CreateCategoryAsync(organiser, ev.Id);
            var (participantA, userA) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            var (participantB, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            await TestHelpers.CreateEnrolmentAsync(participantA, ev.Id, category.Id);

            var responseA = await participantA.GetAsync("/api/enrolments/me");
            var responseB = await participantB.GetAsync("/api/enrolments/me");

            Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
            var listA = await responseA.Content.ReadFromJsonAsync<List<EnrolmentResponse>>();
            Assert.Single(listA!);
            Assert.Equal(userA.Id, listA![0].ParticipantId);

            var listB = await responseB.Content.ReadFromJsonAsync<List<EnrolmentResponse>>();
            Assert.Empty(listB!);
        }

        [Fact]
        public async Task GetByEvent_AsOwningOrganiser_ReturnsTheEventsEnrolments()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            var category = await TestHelpers.CreateCategoryAsync(organiser, ev.Id);
            var (participant, user) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            await TestHelpers.CreateEnrolmentAsync(participant, ev.Id, category.Id);

            var response = await organiser.GetAsync($"/api/events/{ev.Id}/enrolments");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var list = await response.Content.ReadFromJsonAsync<List<EnrolmentResponse>>();
            Assert.Single(list!);
            Assert.Equal(user.Id, list![0].ParticipantId);
        }

        [Fact]
        public async Task GetByEvent_AsDifferentOrganiser_ReturnsForbidden()
        {
            var (owner, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var (otherOrganiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(owner);

            var response = await otherOrganiser.GetAsync($"/api/events/{ev.Id}/enrolments");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Cancel_AsOwningParticipant_ReturnsNoContentAndEnrolmentIsGone()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            var category = await TestHelpers.CreateCategoryAsync(organiser, ev.Id);
            var (participant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            var enrolment = await TestHelpers.CreateEnrolmentAsync(participant, ev.Id, category.Id);

            var deleteResponse = await participant.DeleteAsync($"/api/enrolments/{enrolment.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var listResponse = await participant.GetAsync("/api/enrolments/me");
            var list = await listResponse.Content.ReadFromJsonAsync<List<EnrolmentResponse>>();
            Assert.Empty(list!);
        }

        [Fact]
        public async Task Cancel_AsDifferentParticipant_ReturnsForbidden()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            var category = await TestHelpers.CreateCategoryAsync(organiser, ev.Id);
            var (owner, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            var (otherParticipant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            var enrolment = await TestHelpers.CreateEnrolmentAsync(owner, ev.Id, category.Id);

            var response = await otherParticipant.DeleteAsync($"/api/enrolments/{enrolment.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}