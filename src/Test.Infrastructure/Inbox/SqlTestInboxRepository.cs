using BuildingBlocks.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;
using Test.Application.Interfaces;

namespace Test.Infrastructure.Inbox;

public class SqlTestInboxRepository : IInboxRepository
{
    private readonly ITestDbContext _dbContext;

    public SqlTestInboxRepository(ITestDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HasBeenProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
    {
        return await _dbContext.InboxMessages
            .AnyAsync(i => i.EventId == eventId && i.ConsumerName == consumerName, cancellationToken);
    }

    public async Task MarkProcessedAsync(Guid eventId, string consumerName, DateTimeOffset processedAtUtc, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.InboxMessages
            .FirstOrDefaultAsync(i => i.EventId == eventId && i.ConsumerName == consumerName, cancellationToken);

        if (existing == null)
        {
            await _dbContext.InboxMessages.AddAsync(new InboxMessage
            {
                EventId = eventId,
                ConsumerName = consumerName,
                ProcessedAtUtc = processedAtUtc
            }, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
