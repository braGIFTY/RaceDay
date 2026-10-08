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
        // of repeated in every test.
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