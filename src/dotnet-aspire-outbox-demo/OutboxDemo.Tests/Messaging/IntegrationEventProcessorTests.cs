// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OutboxDemo.Tests.TestHelpers;
using ReceiverService.Data;
using ReceiverService.Messaging;
using System.Text.Json;

namespace OutboxDemo.Tests.Messaging;

public class IntegrationEventProcessorTests : IDisposable
{
    private readonly ReceiverDbContext _db = InMemoryDbContextFactory.CreateReceiverDbContext();
    private readonly IntegrationEventProcessor _processor;

    public IntegrationEventProcessorTests()
    {
        _processor = new IntegrationEventProcessor(_db, MockLogger.Create<IntegrationEventProcessor>().Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ProcessAsync_EntityItemCreated_CreatesProjection()
    {
        var id = Guid.NewGuid();
        var createdAt = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        var result = await _processor.ProcessAsync(
            IntegrationEventProcessor.EntityItemCreatedType,
            CreatePayload(id, "Item", 12.5m, createdAt),
            TestContext.Current.CancellationToken);

        result.Should().Be(MessageProcessingResult.Processed);
        var projection = await _db.EntityItems.SingleAsync(TestContext.Current.CancellationToken);
        projection.Id.Should().Be(id);
        projection.Name.Should().Be("Item");
        projection.Value.Should().Be(12.5m);
        projection.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public async Task ProcessAsync_TimestampWithOffset_IsStoredAsUtc()
    {
        var payload = $"{{\"id\":\"{Guid.NewGuid()}\",\"name\":\"Item\",\"value\":1,\"createdAt\":\"2025-01-02T05:04:05+02:00\"}}";

        var result = await _processor.ProcessAsync(IntegrationEventProcessor.EntityItemCreatedType, payload, TestContext.Current.CancellationToken);

        result.Should().Be(MessageProcessingResult.Processed);
        var projection = await _db.EntityItems.SingleAsync(TestContext.Current.CancellationToken);
        projection.CreatedAt.Should().Be(new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        projection.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task ProcessAsync_DuplicateDelivery_IsIdempotent()
    {
        var id = Guid.NewGuid();
        var payload = CreatePayload(id, "Item", 10m, DateTime.UtcNow);

        var first = await _processor.ProcessAsync(IntegrationEventProcessor.EntityItemCreatedType, payload, TestContext.Current.CancellationToken);
        var second = await _processor.ProcessAsync(IntegrationEventProcessor.EntityItemCreatedType, payload, TestContext.Current.CancellationToken);

        first.Should().Be(MessageProcessingResult.Processed);
        second.Should().Be(MessageProcessingResult.Processed);
        (await _db.EntityItems.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{\"id\":\"00000000-0000-0000-0000-000000000000\",\"name\":\"Item\",\"value\":1}")]
    [InlineData("{\"id\":\"8d7f2b0e-5d0c-4b43-9f5c-1f1f7a1f2f3a\",\"name\":\" \",\"value\":1}")]
    public async Task ProcessAsync_InvalidPayload_IsRejected(string? payload)
    {
        var result = await _processor.ProcessAsync(IntegrationEventProcessor.EntityItemCreatedType, payload, TestContext.Current.CancellationToken);

        result.Should().Be(MessageProcessingResult.Rejected);
        (await _db.EntityItems.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("UnknownEvent")]
    public async Task ProcessAsync_UnknownType_IsRejected(string? type)
    {
        var result = await _processor.ProcessAsync(type, CreatePayload(Guid.NewGuid(), "Item", 1m, DateTime.UtcNow), TestContext.Current.CancellationToken);

        result.Should().Be(MessageProcessingResult.Rejected);
        (await _db.EntityItems.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    private static string CreatePayload(Guid id, string name, decimal value, DateTime createdAt) =>
        JsonSerializer.Serialize(new { Id = id, Name = name, Value = value, CreatedAt = createdAt });
}
