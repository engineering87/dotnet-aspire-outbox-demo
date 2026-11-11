// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.EntityFrameworkCore;
using SenderService.Data;
using SenderService.Data.Entities;

namespace SenderService.Outbox
{
    /// <summary>
    /// Relays committed outbox records to the message broker with at-least-once semantics.
    /// </summary>
    /// <remarks>
    /// Each cycle claims a batch inside a database transaction. On PostgreSQL the rows are locked with
    /// <c>FOR UPDATE SKIP LOCKED</c>, so multiple SenderService instances never publish the same record concurrently.
    /// A record is marked as processed only after the broker has accepted it; a crash between the broker
    /// acknowledgement and the commit can still cause a duplicate, which consumers must tolerate.
    /// </remarks>
    public class OutboxPublisherHostedService : BackgroundService
    {
        private const int BatchSize = 50;
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Minimum delay before a record whose last publish attempt failed is attempted again.
        /// </summary>
        public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

        private readonly IServiceProvider _services;
        private readonly ILogger<OutboxPublisherHostedService> _logger;

        public OutboxPublisherHostedService(
            IServiceProvider services,
            ILogger<OutboxPublisherHostedService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OutboxPublisherHostedService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                var batchWasFull = false;

                try
                {
                    batchWasFull = await PublishPendingBatchAsync(stoppingToken) == BatchSize;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OutboxPublisherHostedService cycle failed");
                }

                if (batchWasFull)
                {
                    continue;
                }

                try
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation("OutboxPublisherHostedService stopping");
        }

        private async Task<int> PublishPendingBatchAsync(CancellationToken stoppingToken)
        {
            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SenderDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);

            var messages = await ClaimPendingMessagesAsync(db, DateTime.UtcNow - RetryDelay, stoppingToken);
            if (messages.Count == 0)
            {
                return 0;
            }

            var published = 0;
            foreach (var msg in messages)
            {
                try
                {
                    using var publishTimeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    publishTimeout.CancelAfter(PublishTimeout);

                    await publisher.PublishAsync(msg, publishTimeout.Token);

                    msg.ProcessedAt = DateTime.UtcNow;
                    msg.FailedAt = null;
                    msg.LastError = null;
                    published++;
                    _logger.LogDebug("Published message {MessageId} with CorrelationId {CorrelationId}", msg.Id, msg.CorrelationId);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error publishing outbox message {MessageId} with CorrelationId {CorrelationId}", msg.Id, msg.CorrelationId);
                    msg.RetryCount++;
                    msg.LastError = ex.Message;
                    msg.FailedAt = DateTime.UtcNow;

                    // The broker is most likely unavailable: stop the batch and let the next cycle retry.
                    break;
                }
            }

            // Persist progress even during shutdown, otherwise records already accepted by the broker would be published again.
            await db.SaveChangesAsync(CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);

            return published;
        }

        private static Task<List<OutboxMessage>> ClaimPendingMessagesAsync(
            SenderDbContext db,
            DateTime retryFailedBefore,
            CancellationToken cancellationToken)
        {
            if (db.Database.IsNpgsql())
            {
                return db.OutboxMessages
                    .FromSql($"""
                        SELECT * FROM "OutboxMessages"
                        WHERE "ProcessedAt" IS NULL
                          AND ("FailedAt" IS NULL OR "FailedAt" <= {retryFailedBefore})
                        ORDER BY "CreatedAt"
                        LIMIT {BatchSize}
                        FOR UPDATE SKIP LOCKED
                        """)
                    .ToListAsync(cancellationToken);
            }

            return db.OutboxMessages
                .Where(m => m.ProcessedAt == null && (m.FailedAt == null || m.FailedAt <= retryFailedBefore))
                .OrderBy(m => m.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
        }
    }
}