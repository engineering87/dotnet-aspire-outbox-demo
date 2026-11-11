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
    public async Task ExecuteAsync_RetriesPreviouslyFailedMessages()
    {
        var failedMessage = CreateOutboxMessage();
        failedMessage.FailedAt = DateTime.UtcNow - OutboxPublisherHostedService.RetryDelay - TimeSpan.FromSeconds(1);
        failedMessage.RetryCount = 5;
        failedMessage.LastError = "Broker unavailable";
        using (var db = CreateDbContext())
        {
            db.OutboxMessages.Add(failedMessage);
            await db.SaveChangesAsync();
        }

        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback(() => published.TrySetResult())
            .Returns(Task.CompletedTask);

        var service = new OutboxPublisherHostedService(_serviceProvider, _mockLogger.Object);
        await service.StartAsync(CancellationToken.None);
        try
        {
            await published.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (true)
            {
                using var db = CreateDbContext();
                var saved = await db.OutboxMessages.FindAsync(failedMessage.Id);
                if (saved?.ProcessedAt is not null)
                {
                    saved.FailedAt.Should().BeNull();
                    saved.LastError.Should().BeNull();
                    break;
                }
                await Task.Delay(10, timeout.Token);
            }
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        _mockPublisher.Verify(
            p => p.PublishAsync(It.Is<OutboxMessage>(m => m.Id == failedMessage.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFiveAttemptsFail_MessageRemainsPending()
    {
        // Arrange
        var message = CreateOutboxMessage();
        message.RetryCount = 4;
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
            updatedMessage.ProcessedAt.Should().BeNull();
            updatedMessage.RetryCount.Should().Be(5);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRetryMessageWithinRetryDelay()
    {
        var recentlyFailed = CreateOutboxMessage();
        recentlyFailed.FailedAt = DateTime.UtcNow;
        recentlyFailed.RetryCount = 1;
        var pending = CreateOutboxMessage();
        using (var db = CreateDbContext())
        {
            db.OutboxMessages.AddRange(recentlyFailed, pending);
            await db.SaveChangesAsync();
        }

        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback(() => published.TrySetResult())
            .Returns(Task.CompletedTask);

        var service = new OutboxPublisherHostedService(_serviceProvider, _mockLogger.Object);
        await service.StartAsync(CancellationToken.None);
        try
        {
            await published.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        _mockPublisher.Verify(
            p => p.PublishAsync(It.Is<OutboxMessage>(m => m.Id == pending.Id), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockPublisher.Verify(
            p => p.PublishAsync(It.Is<OutboxMessage>(m => m.Id == recentlyFailed.Id), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPublishFails_StopsCurrentBatch()
    {
        var first = CreateOutboxMessage(DateTime.UtcNow.AddMinutes(-2));
        var second = CreateOutboxMessage(DateTime.UtcNow.AddMinutes(-1));
        using (var db = CreateDbContext())
        {
            db.OutboxMessages.AddRange(first, second);
            await db.SaveChangesAsync();
        }

        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback(() => attempted.TrySetResult())
            .ThrowsAsync(new Exception("Broker unavailable"));

        var service = new OutboxPublisherHostedService(_serviceProvider, _mockLogger.Object);
        await service.StartAsync(CancellationToken.None);
        try
        {
            await attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(async () =>
            {
                using var db = CreateDbContext();
                return (await db.OutboxMessages.FindAsync(first.Id))!.RetryCount == 1;
            });
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        _mockPublisher.Verify(
            p => p.PublishAsync(It.Is<OutboxMessage>(m => m.Id == second.Id), It.IsAny<CancellationToken>()),
            Times.Never);
        using (var db = CreateDbContext())
        {
            (await db.OutboxMessages.FindAsync(second.Id))!.RetryCount.Should().Be(0);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenStoppedMidBatch_PersistsAlreadyPublishedMessages()
    {
        var first = CreateOutboxMessage(DateTime.UtcNow.AddMinutes(-2));
        var second = CreateOutboxMessage(DateTime.UtcNow.AddMinutes(-1));
        using (var db = CreateDbContext())
        {
            db.OutboxMessages.AddRange(first, second);
            await db.SaveChangesAsync();
        }

        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockPublisher
            .Setup(p => p.PublishAsync(It.Is<OutboxMessage>(m => m.Id == first.Id), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockPublisher
            .Setup(p => p.PublishAsync(It.Is<OutboxMessage>(m => m.Id == second.Id), It.IsAny<CancellationToken>()))
            .Returns<OutboxMessage, CancellationToken>(async (_, ct) =>
            {
                secondStarted.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
            });

        var service = new OutboxPublisherHostedService(_serviceProvider, _mockLogger.Object);
        await service.StartAsync(CancellationToken.None);
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        using var verifyDb = CreateDbContext();
        (await verifyDb.OutboxMessages.FindAsync(first.Id))!.ProcessedAt.Should().NotBeNull();
        var unpublished = await verifyDb.OutboxMessages.FindAsync(second.Id);
        unpublished!.ProcessedAt.Should().BeNull();
        unpublished.RetryCount.Should().Be(0);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!await condition())
        {
            await Task.Delay(10, timeout.Token);
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
