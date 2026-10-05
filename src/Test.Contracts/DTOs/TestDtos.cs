namespace Test.Contracts.DTOs;

public record CreateTestSessionRequest(
    int QuestionCount = 10,
    string? CategoryId = null
);

public record TestQuestionDto(
    Guid Id,
    Guid VocabularyId,
    string QuestionType,
    string Prompt,
    IReadOnlyList<string> Options,
    int OrderIndex,
    string? SelectedAnswer = null,
    string? CorrectAnswer = null,
    bool? IsCorrect = null
);

public record TestSessionDto(
    Guid Id,
    string Title,
    string Status,
    int TotalQuestions,
    int CorrectAnswers,
    double ScorePercentage,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<TestQuestionDto> Questions
);

public record SubmitQuestionAnswerDto(
    Guid QuestionId,
    string SelectedAnswer
);

public record SubmitTestRequest(
    IReadOnlyList<SubmitQuestionAnswerDto> Answers
);

public record TestSessionSummaryDto(
    Guid Id,
    string Title,
    string Status,
    int TotalQuestions,
    int CorrectAnswers,
    double ScorePercentage,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt
);
