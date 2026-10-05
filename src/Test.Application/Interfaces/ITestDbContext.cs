using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Test.Domain.Entities;

namespace Test.Application.Interfaces;

public interface ITestDbContext
{
    DbSet<TestSession> TestSessions { get; }
    DbSet<TestQuestion> TestQuestions { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<InboxMessage> InboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
