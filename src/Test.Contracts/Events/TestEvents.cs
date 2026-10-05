namespace Test.Contracts.Events;

public record TestAttemptCompletedEvent(
    Guid SessionId,
    Guid UserId,
    int TotalQuestions,
    int CorrectAnswers,
    double ScorePercentage,
    DateTimeOffset CompletedAt
);
