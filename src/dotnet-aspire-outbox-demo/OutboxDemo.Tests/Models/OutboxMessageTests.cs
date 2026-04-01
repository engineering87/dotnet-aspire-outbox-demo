// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using SenderService.Data.Entities;

namespace OutboxDemo.Tests.Models;

public class OutboxMessageTests
{
    [Fact]
    public void OutboxMessage_NewInstance_HasDefaultValues()
    {
        // Arrange & Act
        var message = new OutboxMessage();

        // Assert
        message.Id.Should().Be(Guid.Empty);
        message.ProcessedAt.Should().BeNull();
        message.FailedAt.Should().BeNull();
        message.RetryCount.Should().Be(0);
        message.LastError.Should().BeNull();
    }

    [Fact]
    public void OutboxMessage_SetProperties_PropertiesAreSet()
    {
        // Arrange
        var id = Guid.NewGuid();
        var aggregateId = "aggregate-123";
        var type = "TestEvent";
        var payload = "{\"data\":\"test\"}";
        var correlationId = "correlation-456";
        var createdAt = DateTime.UtcNow;

        // Act
        var message = new OutboxMessage
        {
            Id = id,
            AggregateId = aggregateId,
            Type = type,
            Payload = payload,
            CorrelationId = correlationId,
            CreatedAt = createdAt
        };

        // Assert
        message.Id.Should().Be(id);
        message.AggregateId.Should().Be(aggregateId);
        message.Type.Should().Be(type);
        message.Payload.Should().Be(payload);
        message.CorrelationId.Should().Be(correlationId);
        message.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void OutboxMessage_MarkAsProcessed_SetsProcessedAt()
    {
        // Arrange
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = "test",
            Type = "TestEvent",
            Payload = "{}",
            CorrelationId = "test-corr",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        message.ProcessedAt = DateTime.UtcNow;

        // Assert
        message.ProcessedAt.Should().NotBeNull();
        message.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void OutboxMessage_MarkAsFailed_SetsFailedAtAndLastError()
    {
        // Arrange
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = "test",
            Type = "TestEvent",
            Payload = "{}",
            CorrelationId = "test-corr",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        message.FailedAt = DateTime.UtcNow;
        message.LastError = "Connection refused";
        message.RetryCount = 5;

        // Assert
        message.FailedAt.Should().NotBeNull();
        message.LastError.Should().Be("Connection refused");
        message.RetryCount.Should().Be(5);
    }

    [Fact]
    public void OutboxMessage_IncrementRetryCount_IncrementsCorrectly()
    {
        // Arrange
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = "test",
            Type = "TestEvent",
            Payload = "{}",
            CorrelationId = "test-corr",
            CreatedAt = DateTime.UtcNow,
            RetryCount = 0
        };

        // Act
        message.RetryCount++;
        message.RetryCount++;
        message.RetryCount++;

        // Assert
        message.RetryCount.Should().Be(3);
    }
}
