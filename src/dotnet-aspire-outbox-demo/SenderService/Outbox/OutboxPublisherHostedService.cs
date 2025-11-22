// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.EntityFrameworkCore;
using SenderService.Data;

namespace SenderService.Outbox
{
    public class OutboxPublisherHostedService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<OutboxPublisherHostedService> _logger;
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

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
                try
                {
                    await using var scope = _services.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<SenderDbContext>();
                    var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

                    var pendingMessages = await db.OutboxMessages
                        .Where(m => m.ProcessedAt == null)
                        .OrderBy(m => m.CreatedAt)
                        .Take(50)
                        .ToListAsync(stoppingToken);

                    foreach (var msg in pendingMessages)
                    {
                        try
                        {
                            await publisher.PublishAsync(msg, stoppingToken);
                            msg.ProcessedAt = DateTime.UtcNow;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error publishing outbox message {MessageId}", msg.Id);
                            msg.RetryCount++;
                        }
                    }

                    if (pendingMessages.Count > 0)
                    {
                        await db.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OutboxPublisherHostedService cycle failed");
                }

                await Task.Delay(_pollInterval, stoppingToken);
            }

            _logger.LogInformation("OutboxPublisherHostedService stopping");
        }
    }
}