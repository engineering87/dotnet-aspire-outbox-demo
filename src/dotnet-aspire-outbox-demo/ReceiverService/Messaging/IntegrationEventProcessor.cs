// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.EntityFrameworkCore;
using ReceiverService.Data;
using ReceiverService.Data.Entities;
using System.Text.Json;

namespace ReceiverService.Messaging
{
    public enum MessageProcessingResult
    {
        /// <summary>The message was applied and can be acknowledged.</summary>
        Processed,

        /// <summary>The message can never be processed (unknown type or invalid payload) and must be dead-lettered.</summary>
        Rejected
    }

    /// <summary>
    /// Applies integration events to the receiver read model.
    /// </summary>
    /// <remarks>
    /// Reprocessing the same EntityItemCreated payload updates the existing projection rather than inserting another row.
    /// Transient failures (e.g. database unavailable) are thrown so the caller can request a redelivery.
    /// </remarks>
    public class IntegrationEventProcessor
    {
        public const string EntityItemCreatedType = "EntityItemCreated";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ReceiverDbContext _db;
        private readonly ILogger<IntegrationEventProcessor> _logger;

        public IntegrationEventProcessor(ReceiverDbContext db, ILogger<IntegrationEventProcessor> logger)
        {
            _db = db;
            _logger = logger;
        }

        public Task<MessageProcessingResult> ProcessAsync(string? type, string? payload, CancellationToken cancellationToken = default)
        {
            if (type == EntityItemCreatedType)
            {
                return HandleEntityItemCreatedAsync(payload, cancellationToken);
            }

            _logger.LogWarning("Received message with unknown type {Type}", type);
            return Task.FromResult(MessageProcessingResult.Rejected);
        }

        private async Task<MessageProcessingResult> HandleEntityItemCreatedAsync(string? payload, CancellationToken cancellationToken)
        {
            EntityItemCreatedDto? dto;
            try
            {
                dto = string.IsNullOrWhiteSpace(payload)
                    ? null
                    : JsonSerializer.Deserialize<EntityItemCreatedDto>(payload, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid EntityItemCreated payload: {Payload}", payload);
                return MessageProcessingResult.Rejected;
            }

            if (dto is null || dto.Id == Guid.Empty || string.IsNullOrWhiteSpace(dto.Name))
            {
                _logger.LogError("Invalid EntityItemCreated payload: {Payload}", payload);
                return MessageProcessingResult.Rejected;
            }

            _logger.LogInformation("Processing EntityItemCreated for {EntityId}", dto.Id);

            var existing = await _db.EntityItems.FirstOrDefaultAsync(e => e.Id == dto.Id, cancellationToken);
            if (existing is null)
            {
                _db.EntityItems.Add(new EntityItemProjection
                {
                    Id = dto.Id,
                    Name = dto.Name,
                    Value = dto.Value,
                    CreatedAt = dto.CreatedAt.UtcDateTime,
                    ReceivedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Name = dto.Name;
                existing.Value = dto.Value;
            }

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully processed EntityItemCreated for {EntityId}", dto.Id);
            return MessageProcessingResult.Processed;
        }

        // DateTimeOffset accepts any ISO 8601 offset; PostgreSQL timestamptz requires UTC values.
        private sealed record EntityItemCreatedDto(
            Guid Id,
            string? Name,
            decimal Value,
            DateTimeOffset CreatedAt);
    }
}
