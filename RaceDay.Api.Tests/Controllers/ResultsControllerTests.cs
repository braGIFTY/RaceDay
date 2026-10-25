using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using Xunit;

namespace RaceDay.Api.Tests.Controllers
{
    public class ResultsControllerTests : IClassFixture<RaceDayApiFactory>
    {
        private readonly RaceDayApiFactory _factory;

        public ResultsControllerTests(RaceDayApiFactory factory)
        {
            _factory = factory;
        }

        private static readonly TimeSpan SampleFinishTime = new(1, 38, 47);

        // Almost every results test starts from the same situation: an organiser with an
        // event and category, and a participant enrolled in it. Built once here instead
        // of repeated in every test. The event defaults to a past date, so results can be recorded.
        private record Scenario(
            HttpClient Organiser,
            HttpClient Participant,
            AuthResponse ParticipantUser,
            EnrolmentResponse Enrolment);

        private async Task<Scenario> SetUpEnrolledParticipantAsync()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            var category = await TestHelpers.CreateCategoryAsync(organiser, ev.Id);
            var (participant, user) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            var enrolment = await TestHelpers.CreateEnrolmentAsync(participant, ev.Id, category.Id);
            return new Scenario(organiser, participant, user, enrolment);
        }

        [Fact]
        public async Task Create_WithoutLogin_ReturnsUnauthorized()
        {
            var client = _factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/enrolments/1/results", new CreateResultRequest(SampleFinishTime, 1));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Create_AsParticipant_ReturnsForbidden()
        {
            var (participant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);

            var response = await participant.PostAsJsonAsync("/api/enrolments/1/results", new CreateResultRequest(SampleFinishTime, 1));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Create_AsOwningOrganiser_ReturnsCreatedWithRecordedValues()
        {
            var scenario = await SetUpEnrolledParticipantAsync();

            var response = await scenario.Organiser.PostAsJsonAsync(
                $"/api/enrolments/{scenario.Enrolment.Id}/results", new CreateResultRequest(SampleFinishTime, 3));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ResultResponse>();
            Assert.Equal(scenario.Enrolment.Id, body!.EnrolmentId);
            Assert.Equal(scenario.ParticipantUser.Id, body.ParticipantId);
            Assert.Equal(SampleFinishTime, body.FinishTime);
            Assert.Equal(3, body.Position);
            Assert.Equal(1, body.TotalFinishers);
        }

        [Fact]
        public async Task Create_AsDifferentOrganiser_ReturnsForbidden()
        {
            var scenario = await SetUpEnrolledParticipantAsync();
            var (otherOrganiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);

            var response = await otherOrganiser.PostAsJsonAsync(
                $"/api/enrolments/{scenario.Enrolment.Id}/results", new CreateResultRequest(SampleFinishTime, 1));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Create_SecondResultForSameEnrolment_ReturnsConflict()
        {
            var scenario = await SetUpEnrolledParticipantAsync();
            await TestHelpers.CreateResultAsync(scenario.Organiser, scenario.Enrolment.Id, SampleFinishTime, 1);

            var response = await scenario.Organiser.PostAsJsonAsync(
                $"/api/enrolments/{scenario.Enrolment.Id}/results", new CreateResultRequest(SampleFinishTime, 2));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task Create_WithNonexistentEnrolment_ReturnsNotFound()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);

            var response = await organiser.PostAsJsonAsync("/api/enrolments/999999/results", new CreateResultRequest(SampleFinishTime, 1));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Create_WithInvalidPosition_ReturnsBadRequest()
        {
            var scenario = await SetUpEnrolledParticipantAsync();

            var response = await scenario.Organiser.PostAsJsonAsync(
                $"/api/enrolments/{scenario.Enrolment.Id}/results", new CreateResultRequest(SampleFinishTime, 0));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Create_ForAFutureEvent_ReturnsConflict()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var futureEvent = await TestHelpers.CreateEventAsync(organiser, "Future Event", DateTime.Today.AddDays(30));
            var category = await TestHelpers.CreateCategoryAsync(organiser, futureEvent.Id);
            var (participant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            var enrolment = await TestHelpers.CreateEnrolmentAsync(participant, futureEvent.Id, category.Id);

            var response = await organiser.PostAsJsonAsync(
                $"/api/enrolments/{enrolment.Id}/results", new CreateResultRequest(SampleFinishTime, 1));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task Create_ForAnEventHeldToday_ReturnsCreated()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var todaysEvent = await TestHelpers.CreateEventAsync(organiser, "Today's Event", DateTime.Today);
            var category = await TestHelpers.CreateCategoryAsync(organiser, todaysEvent.Id);
            var (participant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            var enrolment = await TestHelpers.CreateEnrolmentAsync(participant, todaysEvent.Id, category.Id);

            // Races finish the same day they start, so the event date itself is allowed
            var response = await organiser.PostAsJsonAsync(
                $"/api/enrolments/{enrolment.Id}/results", new CreateResultRequest(SampleFinishTime, 1));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task Create_ForACancelledEnrolment_ReturnsConflict()
        {
            var scenario = await SetUpEnrolledParticipantAsync();
            await scenario.Participant.DeleteAsync($"/api/enrolments/{scenario.Enrolment.Id}");

            var response = await scenario.Organiser.PostAsJsonAsync(
                $"/api/enrolments/{scenario.Enrolment.Id}/results", new CreateResultRequest(SampleFinishTime, 1));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task Update_AsOwningOrganiser_ReturnsOkWithUpdatedValues()
        {
            var scenario = await SetUpEnrolledParticipantAsync();
            var created = await TestHelpers.CreateResultAsync(scenario.Organiser, scenario.Enrolment.Id, SampleFinishTime, 5);
            var correctedTime = new TimeSpan(1, 35, 0);

            var response = await scenario.Organiser.PutAsJsonAsync(
                $"/api/results/{created.Id}", new UpdateResultRequest(correctedTime, 2));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ResultResponse>();
            Assert.Equal(correctedTime, body!.FinishTime);
            Assert.Equal(2, body.Position);
        }

        [Fact]
        public async Task Update_AsDifferentOrganiser_ReturnsForbidden()
        {
            var scenario = await SetUpEnrolledParticipantAsync();
            var created = await TestHelpers.CreateResultAsync(scenario.Organiser, scenario.Enrolment.Id, SampleFinishTime, 5);
            var (otherOrganiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);

            var response = await otherOrganiser.PutAsJsonAsync(
                $"/api/results/{created.Id}", new UpdateResultRequest(new TimeSpan(0, 59, 59), 1));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetMine_ReturnsOnlyTheCurrentParticipantsResults()
        {
            var scenario = await SetUpEnrolledParticipantAsync();
            await TestHelpers.CreateResultAsync(scenario.Organiser, scenario.Enrolment.Id, SampleFinishTime, 5);
            var (otherParticipant, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);

            var mine = await scenario.Participant.GetAsync("/api/results/me");
            var others = await otherParticipant.GetAsync("/api/results/me");

            Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
            var myList = await mine.Content.ReadFromJsonAsync<List<ResultResponse>>();
            Assert.Single(myList!);
            Assert.Equal(scenario.ParticipantUser.Id, myList![0].ParticipantId);

            var otherList = await others.Content.ReadFromJsonAsync<List<ResultResponse>>();
            Assert.Empty(otherList!);
        }

        [Fact]
        public async Task GetMine_IncludesEventDateCategoryAndTotalFinishers()
        {
            var (organiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);
            var ev = await TestHelpers.CreateEventAsync(organiser);
            var category = await TestHelpers.CreateCategoryAsync(organiser, ev.Id, "21km Individual");
            var (runnerOne, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            var (runnerTwo, _) = await TestHelpers.CreateLoggedInParticipantAsync(_factory);
            var enrolmentOne = await TestHelpers.CreateEnrolmentAsync(runnerOne, ev.Id, category.Id);
            var enrolmentTwo = await TestHelpers.CreateEnrolmentAsync(runnerTwo, ev.Id, category.Id);
            await TestHelpers.CreateResultAsync(organiser, enrolmentOne.Id, new TimeSpan(1, 40, 0), 2);
            await TestHelpers.CreateResultAsync(organiser, enrolmentTwo.Id, new TimeSpan(1, 35, 0), 1);

            var response = await runnerOne.GetAsync("/api/results/me");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var list = await response.Content.ReadFromJsonAsync<List<ResultResponse>>();
            var result = Assert.Single(list!);
            Assert.Equal("21km Individual", result.CategoryName);
            Assert.Equal(ev.EventDate.Date, result.EventDate.Date);
            Assert.Equal(2, result.Position);
            Assert.Equal(2, result.TotalFinishers);
        }

        [Fact]
        public async Task GetByEvent_AsOwningOrganiser_ReturnsTheEventsResults()
        {
            var scenario = await SetUpEnrolledParticipantAsync();
            await TestHelpers.CreateResultAsync(scenario.Organiser, scenario.Enrolment.Id, SampleFinishTime, 5);

            var response = await scenario.Organiser.GetAsync($"/api/events/{scenario.Enrolment.EventId}/results");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var list = await response.Content.ReadFromJsonAsync<List<ResultResponse>>();
            Assert.Single(list!);
            Assert.Equal(scenario.ParticipantUser.Id, list![0].ParticipantId);
        }

        [Fact]
        public async Task GetByEvent_AsDifferentOrganiser_ReturnsForbidden()
        {
            var scenario = await SetUpEnrolledParticipantAsync();
            var (otherOrganiser, _) = await TestHelpers.CreateLoggedInOrganiserAsync(_factory);

            var response = await otherOrganiser.GetAsync($"/api/events/{scenario.Enrolment.EventId}/results");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}