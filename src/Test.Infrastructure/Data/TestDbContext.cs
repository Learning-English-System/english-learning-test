using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Test.Application.Interfaces;
using Test.Domain.Entities;

namespace Test.Infrastructure.Data;

public class TestDbContext : DbContext, ITestDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    public DbSet<TestSession> TestSessions => Set<TestSession>();
    public DbSet<TestQuestion> TestQuestions => Set<TestQuestion>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TestSession
        modelBuilder.Entity<TestSession>(b =>
        {
            b.ToTable("TestSessions");
            b.HasKey(s => s.Id);
            b.Property(s => s.Title).IsRequired().HasMaxLength(200);
            b.Property(s => s.Status).IsRequired().HasMaxLength(50);
            b.HasIndex(s => s.UserId);
            b.HasIndex(s => new { s.UserId, s.StartedAt });

            b.HasMany(s => s.Questions)
             .WithOne(q => q.Session)
             .HasForeignKey(q => q.SessionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // TestQuestion
        modelBuilder.Entity<TestQuestion>(b =>
        {
            b.ToTable("TestQuestions");
            b.HasKey(q => q.Id);
            b.Property(q => q.QuestionType).IsRequired().HasMaxLength(50);
            b.Property(q => q.Prompt).IsRequired().HasMaxLength(500);
            b.Property(q => q.CorrectAnswer).IsRequired().HasMaxLength(500);
            b.Property(q => q.SelectedAnswer).HasMaxLength(500);
            b.Property(q => q.OptionsJson).IsRequired();
            b.HasIndex(q => q.SessionId);
        });

        // OutboxMessage
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages");
            b.HasKey(o => o.Id);
            b.Property(o => o.EventType).HasMaxLength(128).IsRequired();
            b.Property(o => o.PartitionKey).HasMaxLength(128).IsRequired();
            b.Property(o => o.PayloadJson).IsRequired();
            b.HasIndex(o => new { o.PublishedAtUtc, o.CreatedAtUtc });
        });

        // InboxMessage
        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable("InboxMessages");
            b.HasKey(i => new { i.EventId, i.ConsumerName });
            b.Property(i => i.ConsumerName).HasMaxLength(100);
        });
    }
}
