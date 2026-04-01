// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OutboxDemo.Tests.TestHelpers;
using SenderService.Data;
using SenderService.Data.Entities;

namespace OutboxDemo.Tests.Data;

public class SenderDbContextTests : IDisposable
{
    private readonly SenderDbContext _dbContext;

    public SenderDbContextTests()
    {
        _dbContext = InMemoryDbContextFactory.CreateSenderDbContext();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CanAddEntityItem()
    {
        // Arrange
        var entity = new EntityItem
        {
            Id = Guid.NewGuid(),
            Name = "Test Item",
            Value = 100.50m,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _dbContext.EntityItems.Add(entity);
        await _dbContext.SaveChangesAsync();

        // Assert
        var saved = await _dbContext.EntityItems.FindAsync(entity.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Test Item");
    }

    [Fact]
    public async Task CanAddOutboxMessage()
    {
        // Arrange
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = "test-aggregate",
            Type = "TestEvent",
            Payload = "{\"test\":\"data\"}",
            CorrelationId = "test-correlation",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        // Assert
        var saved = await _dbContext.OutboxMessages.FindAsync(message.Id);
        saved.Should().NotBeNull();
        saved!.Type.Should().Be("TestEvent");
    }

    [Fact]
    public async Task CanQueryPendingOutboxMessages()
    {
        // Arrange
        var pending = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = "pending",
            Type = "PendingEvent",
            Payload = "{}",
            CorrelationId = "corr-1",
            CreatedAt = DateTime.UtcNow
        };

        var processed = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = "processed",
            Type = "ProcessedEvent",
            Payload = "{}",
            CorrelationId = "corr-2",
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        };

        var failed = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = "failed",
            Type = "FailedEvent",
            Payload = "{}",
            CorrelationId = "corr-3",
            CreatedAt = DateTime.UtcNow,
            FailedAt = DateTime.UtcNow,
            RetryCount = 5
        };

        _dbContext.OutboxMessages.AddRange(pending, processed, failed);
        await _dbContext.SaveChangesAsync();

        // Act
        var pendingMessages = await _dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.FailedAt == null)
            .ToListAsync();

        // Assert
        pendingMessages.Should().HaveCount(1);
        pendingMessages[0].Id.Should().Be(pending.Id);
    }

    [Fact]
    public async Task CanSaveEntityAndOutboxTogether()
    {
        // Arrange - Simulates atomic save (without explicit transaction for InMemory)
        var entity = new EntityItem
        {
            Id = Guid.NewGuid(),
            Name = "Atomic Item",
            Value = 200m,
            CreatedAt = DateTime.UtcNow
        };

        var outbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = entity.Id.ToString(),
            Type = "TestEvent",
            Payload = "{}",
            CorrelationId = "test",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _dbContext.EntityItems.Add(entity);
        _dbContext.OutboxMessages.Add(outbox);
        await _dbContext.SaveChangesAsync();

        // Assert
        var foundEntity = await _dbContext.EntityItems.FindAsync(entity.Id);
        var foundOutbox = await _dbContext.OutboxMessages.FindAsync(outbox.Id);

        foundEntity.Should().NotBeNull();
        foundOutbox.Should().NotBeNull();
        foundOutbox!.AggregateId.Should().Be(entity.Id.ToString());
    }

    [Fact]
    public async Task QueryOutboxMessages_OrdersByCreatedAt()
    {
        // Arrange
        var older = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = "older",
            Type = "Event",
            Payload = "{}",
            CorrelationId = "corr",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var newer = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = "newer",
            Type = "Event",
            Payload = "{}",
            CorrelationId = "corr",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.OutboxMessages.AddRange(newer, older);
        await _dbContext.SaveChangesAsync();

        // Act
        var ordered = await _dbContext.OutboxMessages
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        // Assert
        ordered[0].Id.Should().Be(older.Id);
        ordered[1].Id.Should().Be(newer.Id);
    }
}
