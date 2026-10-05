using System.Text.Json;
using BuildingBlocks.Messaging.Contracts;
using BuildingBlocks.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Test.Application.Interfaces;
using Test.Contracts.DTOs;
using Test.Contracts.Events;
using Test.Domain.Entities;

namespace Test.Application.Services;

public class TestService : ITestService
{
    private readonly ITestDbContext _dbContext;
    private readonly ILearningServiceClient _learningClient;
    private readonly IAiDistractorClient _aiDistractorClient;

    public TestService(
        ITestDbContext dbContext,
        ILearningServiceClient learningClient,
        IAiDistractorClient aiDistractorClient)
    {
        _dbContext = dbContext;
        _learningClient = learningClient;
        _aiDistractorClient = aiDistractorClient;
    }

    public async Task<TestSessionDto> CreateSessionAsync(
        Guid userId,
        CreateTestSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        int count = Math.Clamp(request.QuestionCount, 1, 50);
        var candidates = await _learningClient.GetCandidatesAsync(userId, count, request.CategoryId, cancellationToken);

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No vocabulary items available to generate a test session. Please add vocabulary items first.");
        }

        var session = new TestSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = $"Vocabulary Test ({candidates.Count} words)",
            Status = "InProgress",
            TotalQuestions = candidates.Count,
            CorrectAnswers = 0,
            ScorePercentage = 0,
            StartedAt = DateTimeOffset.UtcNow
        };

        int order = 1;
        foreach (var candidate in candidates)
        {
            var distractors = await _aiDistractorClient.GenerateDistractorsAsync(
                candidate.Word,
                candidate.Translation,
                3,
                cancellationToken);

            var options = new List<string> { candidate.Translation };
            foreach (var d in distractors)
            {
                if (!string.IsNullOrWhiteSpace(d) && !options.Contains(d, StringComparer.OrdinalIgnoreCase))
                {
                    options.Add(d.Trim());
                }
            }

            while (options.Count < 4)
            {
                options.Add($"Nghĩa khác {options.Count}");
            }

            // Shuffle options
            var shuffledOptions = options.OrderBy(_ => Random.Shared.Next()).ToList();

            session.Questions.Add(new TestQuestion
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                VocabularyId = candidate.Id,
                QuestionType = "MultipleChoice",
                Prompt = candidate.Word,
                CorrectAnswer = candidate.Translation,
                OptionsJson = JsonSerializer.Serialize(shuffledOptions),
                OrderIndex = order++
            });
        }

        await _dbContext.TestSessions.AddAsync(session, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(session, isCompleted: false);
    }

    public async Task<TestSessionDto?> GetSessionByIdAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.TestSessions
            .Include(s => s.Questions.OrderBy(q => q.OrderIndex))
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);

        if (session == null)
        {
            return null;
        }

        bool isCompleted = session.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase);
        return MapToDto(session, isCompleted);
    }

    public async Task<TestSessionDto?> SubmitSessionAsync(
        Guid userId,
        Guid sessionId,
        SubmitTestRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.TestSessions
            .Include(s => s.Questions.OrderBy(q => q.OrderIndex))
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);

        if (session == null)
        {
            return null;
        }

        if (session.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
        {
            return MapToDto(session, isCompleted: true);
        }

        var answersMap = request.Answers.ToDictionary(a => a.QuestionId, a => a.SelectedAnswer);
        int correctCount = 0;

        foreach (var q in session.Questions)
        {
            if (answersMap.TryGetValue(q.Id, out var selectedAnswer))
            {
                q.SelectedAnswer = selectedAnswer;
                bool isCorrect = selectedAnswer.Trim().Equals(q.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase);
                q.IsCorrect = isCorrect;
                if (isCorrect) correctCount++;
            }
            else
            {
                q.SelectedAnswer = string.Empty;
                q.IsCorrect = false;
            }
        }

        var now = DateTimeOffset.UtcNow;
        session.CorrectAnswers = correctCount;
        session.ScorePercentage = session.TotalQuestions > 0
            ? Math.Round((double)correctCount / session.TotalQuestions * 100.0, 1)
            : 0.0;
        session.Status = "Completed";
        session.CompletedAt = now;

        // Emit outbox event
        var completedEvent = IntegrationEventEnvelope<TestAttemptCompletedEvent>.Create(
            eventType: "test.attempt.completed.v1",
            eventVersion: 1,
            producer: "Test.Service",
            correlationId: Guid.NewGuid().ToString(),
            payload: new TestAttemptCompletedEvent(
                session.Id,
                userId,
                session.TotalQuestions,
                correctCount,
                session.ScorePercentage,
                now)
        );

        await _dbContext.OutboxMessages.AddAsync(
            OutboxMessage.FromEnvelope(userId.ToString(), completedEvent),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(session, isCompleted: true);
    }

    public async Task<IReadOnlyList<TestSessionSummaryDto>> GetUserSessionsAsync(
        Guid userId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 50);
        var sessions = await _dbContext.TestSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartedAt)
            .Skip(skip)
            .Take(safeTake)
            .Select(s => new TestSessionSummaryDto(
                s.Id,
                s.Title,
                s.Status,
                s.TotalQuestions,
                s.CorrectAnswers,
                s.ScorePercentage,
                s.StartedAt,
                s.CompletedAt
            ))
            .ToListAsync(cancellationToken);

        return sessions;
    }

    private static TestSessionDto MapToDto(TestSession s, bool isCompleted)
    {
        var questions = s.Questions.OrderBy(q => q.OrderIndex).Select(q =>
        {
            var options = new List<string>();
            try
            {
                options = JsonSerializer.Deserialize<List<string>>(q.OptionsJson) ?? new List<string>();
            }
            catch
            {
                // Fallback
            }

            return new TestQuestionDto(
                Id: q.Id,
                VocabularyId: q.VocabularyId,
                QuestionType: q.QuestionType,
                Prompt: q.Prompt,
                Options: options,
                OrderIndex: q.OrderIndex,
                SelectedAnswer: isCompleted ? q.SelectedAnswer : null,
                CorrectAnswer: isCompleted ? q.CorrectAnswer : null,
                IsCorrect: isCompleted ? q.IsCorrect : null
            );
        }).ToList();

        return new TestSessionDto(
            Id: s.Id,
            Title: s.Title,
            Status: s.Status,
            TotalQuestions: s.TotalQuestions,
            CorrectAnswers: s.CorrectAnswers,
            ScorePercentage: s.ScorePercentage,
            StartedAt: s.StartedAt,
            CompletedAt: s.CompletedAt,
            Questions: questions
        );
    }
}
