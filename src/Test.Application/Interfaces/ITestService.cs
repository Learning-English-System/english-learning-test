using Test.Contracts.DTOs;

namespace Test.Application.Interfaces;

public interface ITestService
{
    Task<TestSessionDto> CreateSessionAsync(
        Guid userId,
        CreateTestSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<TestSessionDto?> GetSessionByIdAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<TestSessionDto?> SubmitSessionAsync(
        Guid userId,
        Guid sessionId,
        SubmitTestRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TestSessionSummaryDto>> GetUserSessionsAsync(
        Guid userId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);
}
