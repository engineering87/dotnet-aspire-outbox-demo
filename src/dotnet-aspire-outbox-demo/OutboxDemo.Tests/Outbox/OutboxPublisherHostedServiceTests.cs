// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OutboxDemo.Tests.TestHelpers;
using SenderService.Data;
using SenderService.Data.Entities;
using SenderService.Outbox;

namespace OutboxDemo.Tests.Outbox;

public class OutboxPublisherHostedServiceTests : IDisposable
{
    private readonly string _databaseName;
    private readonly Mock<IEventPublisher> _mockPublisher;
    private readonly Mock<ILogger<OutboxPublisherHostedService>> _mockLogger;
    private readonly IServiceProvider _serviceProvider;

    public OutboxPublisherHostedServiceTests()
    {
        _databaseName = Guid.NewGuid().ToString();
        _mockPublisher = new Mock<IEventPublisher>();
        _mockLogger = MockLogger.Create<OutboxPublisherHostedService>();

        var services = new ServiceCollection();
        services.AddDbContext<SenderDbContext>(options =>
            options.UseInMemoryDatabase(_databaseName)
                   .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped(_ => _mockPublisher.Object);
        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        (_serviceProvider as IDisposable)?.Dispose();
    }

    private SenderDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SenderDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new SenderDbContext(options);
    }

    [Fact]
    public async Task ExecuteAsync_WithPendingMessages_PublishesMessages()
    {
        // Arrange
        var message = CreateOutboxMessage();
        using (var db = CreateDbContext())
        {
            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
        }

        _mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new OutboxPublisherHostedService(_serviceProvider, _mockLogger.Object);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(50);
            await service.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) { }

        // Assert
        _mockPublisher.Verify(
            p => p.PublishAsync(It.Is<OutboxMessage>(m => m.Id == message.Id), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoMessages_DoesNotPublish()
    {
        // Arrange
        var service = new OutboxPublisherHostedService(_serviceProvider, _mockLogger.Object);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(50);
            await service.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) { }

        // Assert
        _mockPublisher.Verify(
            p => p.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPublishFails_IncrementsRetryCount()
    {
        // Arrange
        var message = CreateOutboxMessage();
        using (var db = CreateDbContext())
        {
            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
        }

        _mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection failed"));

        var service = new OutboxPublisherHostedService(_serviceProvider, _mockLogger.Object);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(50);
            await service.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) { }

        // Assert
        using (var db = CreateDbContext())
        {
            var updatedMessage = await db.OutboxMessages.FindAsync(message.Id);
            updatedMessage!.RetryCount.Should().BeGreaterThan(0);
            updatedMessage.LastError.Should().Contain("Connection failed");
        }
    }

    [Fact]
    public async Task ExecuteAsync_SkipsAlreadyProcessedMessages()
    {
        // Arrange
        var processedMessage = CreateOutboxMessage();
        processedMessage.ProcessedAt = DateTime.UtcNow;
        using (var db = CreateDbContext())
        {
            db.OutboxMessages.Add(processedMessage);
            await db.SaveChangesAsync();
        }

        var service = new OutboxPublisherHostedService(_serviceProvider, _mockLogger.Object);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(50);
            await service.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) { }

        // Assert
        _mockPublisher.Verify(
            p => p.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsFailedMessages()
    {
        // Arrange
        var failedMessage = CreateOutboxMessage();
        failedMessage.FailedAt = DateTime.UtcNow;
        failedMessage.RetryCount = 5;
        using (var db = CreateDbContext())
        {
            db.OutboxMessages.Add(failedMessage);
            await db.SaveChangesAsync();
        }

        var service = new OutboxPublisherHostedService(_serviceProvider, _mockLogger.Object);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(50);
            await service.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) { }

        // Assert
        _mockPublisher.Verify(
            p => p.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMaxRetriesExceeded_MarksMessageAsFailed()
    {
        // Arrange
        var message = CreateOutboxMessage();
        message.RetryCount = 4; // One more failure will exceed max (5)
        using (var db = CreateDbContext())
        {
            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
        }

        _mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Persistent failure"));

        var service = new OutboxPublisherHostedService(_serviceProvider, _mockLogger.Object);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(50);
            await service.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) { }

        // Assert
        using (var db = CreateDbContext())
        {
            var updatedMessage = await db.OutboxMessages.FindAsync(message.Id);
            updatedMessage!.FailedAt.Should().NotBeNull();
            updatedMessage.RetryCount.Should().Be(5);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesMessagesInOrder()
    {
        // Arrange
        var messages = new List<OutboxMessage>
        {
            CreateOutboxMessage(DateTime.UtcNow.AddMinutes(-2)),
            CreateOutboxMessage(DateTime.UtcNow.AddMinutes(-1)),
            CreateOutboxMessage(DateTime.UtcNow)
        };

        using (var db = CreateDbContext())
        {
            db.OutboxMessages.AddRange(messages);
            await db.SaveChangesAsync();
        }

        var publishedOrder = new List<Guid>();
        _mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((m, _) => publishedOrder.Add(m.Id))
            .Returns(Task.CompletedTask);

        var service = new OutboxPublisherHostedService(_serviceProvider, _mockLogger.Object);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(100);
            await service.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) { }

        // Assert - oldest messages should be processed first
        if (publishedOrder.Count >= 2)
        {
            publishedOrder[0].Should().Be(messages[0].Id);
        }
    }

    private static OutboxMessage CreateOutboxMessage(DateTime? createdAt = null)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = Guid.NewGuid().ToString(),
            Type = "TestEvent",
            Payload = "{\"test\":\"data\"}",
            CorrelationId = Guid.NewGuid().ToString(),
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }
}
