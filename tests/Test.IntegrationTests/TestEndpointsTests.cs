using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Test.Application.Interfaces;
using Test.Contracts.DTOs;
using Test.Infrastructure.Data;
using Xunit;

namespace Test.IntegrationTests;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "TestScheme";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? authHeader = Request.Headers.Authorization;
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string userId = authHeader["Bearer ".Length..].Trim();
        if (Guid.TryParse(userId, out var guid))
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, guid.ToString()),
                new Claim("sub", guid.ToString()),
                new Claim(ClaimTypes.Role, "Learner"),
                new Claim("scope", "openid profile email")
            };
            var identity = new ClaimsIdentity(claims, AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, AuthenticationScheme)));
        }

        return Task.FromResult(AuthenticateResult.Fail("Invalid test bearer token"));
    }
}

public class MockLearningServiceClient : ILearningServiceClient
{
    public Task<IReadOnlyList<VocabularyCandidateDto>> GetCandidatesAsync(
        Guid userId,
        int count,
        string? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var list = new List<VocabularyCandidateDto>
        {
            new(Guid.NewGuid(), "meticulous", "tỉ mỉ, cẩn thận", "Adjective", "C1", "Learning", 2),
            new(Guid.NewGuid(), "pragmatic", "thực dụng, thực tế", "Adjective", "B2", "Learning", 1),
            new(Guid.NewGuid(), "ubiquitous", "phổ biến, có mặt khắp nơi", "Adjective", "C1", "Learning", 3)
        };

        return Task.FromResult<IReadOnlyList<VocabularyCandidateDto>>(list.Take(count).ToList());
    }
}

public class MockAiDistractorClient : IAiDistractorClient
{
    public Task<IReadOnlyList<string>> GenerateDistractorsAsync(
        string word,
        string correctMeaning,
        int count = 3,
        CancellationToken cancellationToken = default)
    {
        var list = new List<string>
        {
            $"Distractor 1 for {word}",
            $"Distractor 2 for {word}",
            $"Distractor 3 for {word}"
        };
        return Task.FromResult<IReadOnlyList<string>>(list);
    }
}

public class TestServiceWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthenticationScheme, null);

            // Replace external gRPC client dependencies with mocks for integration tests
            var learningDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ILearningServiceClient));
            if (learningDescriptor != null) services.Remove(learningDescriptor);
            services.AddScoped<ILearningServiceClient, MockLearningServiceClient>();

            var aiDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAiDistractorClient));
            if (aiDescriptor != null) services.Remove(aiDescriptor);
            services.AddScoped<IAiDistractorClient, MockAiDistractorClient>();
        });
    }
}

public class TestEndpointsTests : IClassFixture<TestServiceWebApplicationFactory>
{
    private readonly TestServiceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TestEndpointsTests(TestServiceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Endpoints_Unauthenticated_ReturnUnauthorized()
    {
        var res1 = await _client.PostAsJsonAsync("/api/tests", new CreateTestSessionRequest(5));
        res1.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var res2 = await _client.GetAsync("/api/tests");
        res2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var res3 = await _client.GetAsync($"/api/tests/{Guid.NewGuid()}");
        res3.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var res4 = await _client.PostAsJsonAsync($"/api/tests/{Guid.NewGuid()}/submit", new SubmitTestRequest(Array.Empty<SubmitQuestionAnswerDto>()));
        res4.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateSession_Authenticated_Success_AndHidesAnswers()
    {
        var userId = Guid.NewGuid();
        using var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

        var res = await authedClient.PostAsJsonAsync("/api/tests", new CreateTestSessionRequest(3));
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var session = await res.Content.ReadFromJsonAsync<TestSessionDto>();
        session.Should().NotBeNull();
        session!.TotalQuestions.Should().Be(3);
        session.Status.Should().Be("InProgress");
        session.Questions.Should().HaveCount(3);

        foreach (var q in session.Questions)
        {
            q.CorrectAnswer.Should().BeNull();
            q.IsCorrect.Should().BeNull();
            q.Options.Should().HaveCount(4);
        }
    }

    [Fact]
    public async Task UserIsolation_UserACannotAccessOrSubmitUserBSession()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        using var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userA.ToString());

        using var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userB.ToString());

        var createRes = await clientA.PostAsJsonAsync("/api/tests", new CreateTestSessionRequest(2));
        var sessionA = await createRes.Content.ReadFromJsonAsync<TestSessionDto>();

        // User B attempts GET
        var getB = await clientB.GetAsync($"/api/tests/{sessionA!.Id}");
        getB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // User B attempts Submit
        var submitB = await clientB.PostAsJsonAsync($"/api/tests/{sessionA.Id}/submit", new SubmitTestRequest(Array.Empty<SubmitQuestionAnswerDto>()));
        submitB.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmitSession_GradesQuestions_RevealsAnswers_AndEmitsOutboxEvent()
    {
        var userId = Guid.NewGuid();
        using var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

        var createRes = await authedClient.PostAsJsonAsync("/api/tests", new CreateTestSessionRequest(2));
        var session = await createRes.Content.ReadFromJsonAsync<TestSessionDto>();

        var q1 = session!.Questions[0];
        var q2 = session.Questions[1];

        // Retrieve actual correct answers directly from database to test accurate grading
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var dbQ1 = await db.TestQuestions.FirstAsync(q => q.Id == q1.Id);
            var dbQ2 = await db.TestQuestions.FirstAsync(q => q.Id == q2.Id);

            var submitReq = new SubmitTestRequest(new[]
            {
                new SubmitQuestionAnswerDto(q1.Id, dbQ1.CorrectAnswer), // 1 correct
                new SubmitQuestionAnswerDto(q2.Id, "Definitely Wrong Meaning") // 1 incorrect
            });

            var submitRes = await authedClient.PostAsJsonAsync($"/api/tests/{session.Id}/submit", submitReq);
            submitRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var gradedSession = await submitRes.Content.ReadFromJsonAsync<TestSessionDto>();
            gradedSession.Should().NotBeNull();
            gradedSession!.Status.Should().Be("Completed");
            gradedSession.TotalQuestions.Should().Be(2);
            gradedSession.CorrectAnswers.Should().Be(1);
            gradedSession.ScorePercentage.Should().Be(50.0);
            gradedSession.CompletedAt.Should().NotBeNull();

            var gradedQ1 = gradedSession.Questions.First(q => q.Id == q1.Id);
            gradedQ1.IsCorrect.Should().BeTrue();
            gradedQ1.CorrectAnswer.Should().Be(dbQ1.CorrectAnswer);

            var gradedQ2 = gradedSession.Questions.First(q => q.Id == q2.Id);
            gradedQ2.IsCorrect.Should().BeFalse();
            gradedQ2.CorrectAnswer.Should().Be(dbQ2.CorrectAnswer);

            // Verify Outbox Event created
            var outboxMsg = await db.OutboxMessages.FirstOrDefaultAsync(
                m => m.PartitionKey == userId.ToString() && m.EventType == "test.attempt.completed.v1");
            outboxMsg.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ListSessions_ReturnsUserSummaries()
    {
        var userId = Guid.NewGuid();
        using var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

        await authedClient.PostAsJsonAsync("/api/tests", new CreateTestSessionRequest(1));
        await authedClient.PostAsJsonAsync("/api/tests", new CreateTestSessionRequest(2));

        var listRes = await authedClient.GetAsync("/api/tests");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await listRes.Content.ReadFromJsonAsync<List<TestSessionSummaryDto>>();
        list.Should().NotBeNull();
        list!.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task SubmitSession_DuplicateSubmission_IsIdempotentAndDoesNotDuplicateOutbox()
    {
        var userId = Guid.NewGuid();
        using var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

        var createRes = await authedClient.PostAsJsonAsync("/api/tests", new CreateTestSessionRequest(2));
        var session = await createRes.Content.ReadFromJsonAsync<TestSessionDto>();

        var submitReq = new SubmitTestRequest(new[]
        {
            new SubmitQuestionAnswerDto(session!.Questions[0].Id, "Sample Answer")
        });

        // First submit
        var submitRes1 = await authedClient.PostAsJsonAsync($"/api/tests/{session.Id}/submit", submitReq);
        submitRes1.StatusCode.Should().Be(HttpStatusCode.OK);
        var result1 = await submitRes1.Content.ReadFromJsonAsync<TestSessionDto>();

        // Second submit (same session)
        var submitRes2 = await authedClient.PostAsJsonAsync($"/api/tests/{session.Id}/submit", submitReq);
        submitRes2.StatusCode.Should().Be(HttpStatusCode.OK);
        var result2 = await submitRes2.Content.ReadFromJsonAsync<TestSessionDto>();

        // Assert identical completed state
        result2!.Id.Should().Be(result1!.Id);
        result2.Status.Should().Be("Completed");
        result2.ScorePercentage.Should().Be(result1.ScorePercentage);

        // Assert outbox count is exactly 1 (not duplicated)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var outboxMsgs = await db.OutboxMessages.Where(
                m => m.PartitionKey == userId.ToString() && m.EventType == "test.attempt.completed.v1").ToListAsync();
            outboxMsgs.Should().HaveCount(1);
        }
    }

    [Fact]
    public async Task SubmitSession_PartialAnswers_GradesUnansweredAsIncorrect()
    {
        var userId = Guid.NewGuid();
        using var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

        var createRes = await authedClient.PostAsJsonAsync("/api/tests", new CreateTestSessionRequest(3));
        var session = await createRes.Content.ReadFromJsonAsync<TestSessionDto>();

        // Only answer question 0 with dummy answer, leaving question 1 and 2 unprovided
        var submitReq = new SubmitTestRequest(new[]
        {
            new SubmitQuestionAnswerDto(session!.Questions[0].Id, "Some Answer")
        });

        var submitRes = await authedClient.PostAsJsonAsync($"/api/tests/{session.Id}/submit", submitReq);
        submitRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await submitRes.Content.ReadFromJsonAsync<TestSessionDto>();

        result.Should().NotBeNull();
        result!.TotalQuestions.Should().Be(3);
        result.Questions.Should().HaveCount(3);
        result.Questions.Where(q => q.IsCorrect == false).Should().HaveCountGreaterOrEqualTo(2);
    }
}

