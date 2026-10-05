using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.Application.Interfaces;
using Test.Application.Services;
using Test.Contracts.DTOs;
using Test.Infrastructure.Data;
using Xunit;

namespace Test.UnitTests;

public class TestServiceUnitTests
{
    private static TestDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new TestDbContext(options);
    }

    [Fact]
    public async Task CreateSession_NoCandidates_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDbContext();
        var emptyLearningClient = new FakeLearningServiceClient(Array.Empty<VocabularyCandidateDto>());
        var fakeAiClient = new FakeAiDistractorClient();
        var service = new TestService(db, emptyLearningClient, fakeAiClient);

        var act = async () => await service.CreateSessionAsync(Guid.NewGuid(), new CreateTestSessionRequest(5));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No vocabulary items available*");
    }

    [Fact]
    public async Task CreateSession_ValidCandidates_CreatesSessionWithShuffledOptionsAndHidesAnswers()
    {
        using var db = CreateInMemoryDbContext();
        var candidates = new[]
        {
            new VocabularyCandidateDto(Guid.NewGuid(), "ubiquitous", "có mặt ở khắp nơi", "Adjective", "C1", "Learning", 2),
            new VocabularyCandidateDto(Guid.NewGuid(), "ephemeral", "chóng tàn, phù du", "Adjective", "C1", "Learning", 1)
        };
        var learningClient = new FakeLearningServiceClient(candidates);
        var fakeAiClient = new FakeAiDistractorClient();
        var service = new TestService(db, learningClient, fakeAiClient);

        var userId = Guid.NewGuid();
        var session = await service.CreateSessionAsync(userId, new CreateTestSessionRequest(2));

        session.Should().NotBeNull();
        session.TotalQuestions.Should().Be(2);
        session.Status.Should().Be("InProgress");
        session.Questions.Should().HaveCount(2);

        foreach (var q in session.Questions)
        {
            q.CorrectAnswer.Should().BeNull("CorrectAnswer should remain hidden during an in-progress session");
            q.IsCorrect.Should().BeNull();
            q.Options.Should().HaveCount(4);
        }
    }

    [Fact]
    public async Task SubmitSession_GradesQuestionsAccurately_AndEmitsOutboxEvent()
    {
        using var db = CreateInMemoryDbContext();
        var candidates = new[]
        {
            new VocabularyCandidateDto(Guid.NewGuid(), "word1", "answer1", "Noun", "A1", "Learning", 1),
            new VocabularyCandidateDto(Guid.NewGuid(), "word2", "answer2", "Noun", "A1", "Learning", 1)
        };
        var learningClient = new FakeLearningServiceClient(candidates);
        var fakeAiClient = new FakeAiDistractorClient();
        var service = new TestService(db, learningClient, fakeAiClient);

        var userId = Guid.NewGuid();
        var created = await service.CreateSessionAsync(userId, new CreateTestSessionRequest(2));

        var q1 = created.Questions[0];
        var q2 = created.Questions[1];

        // Submit 1 correct, 1 incorrect
        var submitReq = new SubmitTestRequest(new[]
        {
            new SubmitQuestionAnswerDto(q1.Id, "answer1"), // Correct
            new SubmitQuestionAnswerDto(q2.Id, "wrong_answer") // Incorrect
        });

        var completed = await service.SubmitSessionAsync(userId, created.Id, submitReq);

        completed.Should().NotBeNull();
        completed!.Status.Should().Be("Completed");
        completed.TotalQuestions.Should().Be(2);
        completed.CorrectAnswers.Should().Be(1);
        completed.ScorePercentage.Should().Be(50.0);
        completed.CompletedAt.Should().NotBeNull();

        // Check questions now show answers
        var gradedQ1 = completed.Questions.First(q => q.Id == q1.Id);
        gradedQ1.IsCorrect.Should().BeTrue();
        gradedQ1.CorrectAnswer.Should().Be("answer1");

        var gradedQ2 = completed.Questions.First(q => q.Id == q2.Id);
        gradedQ2.IsCorrect.Should().BeFalse();
        gradedQ2.CorrectAnswer.Should().Be("answer2");

        // Verify outbox message
        var outboxMsg = await db.OutboxMessages.FirstOrDefaultAsync(m => m.PartitionKey == userId.ToString() && m.EventType == "test.attempt.completed.v1");
        outboxMsg.Should().NotBeNull();
    }
}

public class FakeLearningServiceClient : ILearningServiceClient
{
    private readonly IReadOnlyList<VocabularyCandidateDto> _candidates;

    public FakeLearningServiceClient(IReadOnlyList<VocabularyCandidateDto> candidates)
    {
        _candidates = candidates;
    }

    public Task<IReadOnlyList<VocabularyCandidateDto>> GetCandidatesAsync(Guid userId, int count, string? categoryId = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<VocabularyCandidateDto>>(_candidates.Take(count).ToList());
    }
}

public class FakeAiDistractorClient : IAiDistractorClient
{
    public Task<IReadOnlyList<string>> GenerateDistractorsAsync(string word, string correctMeaning, int count = 3, CancellationToken cancellationToken = default)
    {
        var list = new List<string>();
        for (int i = 1; i <= count; i++)
        {
            list.Add($"Distractor {i} for {word}");
        }
        return Task.FromResult<IReadOnlyList<string>>(list);
    }
}
