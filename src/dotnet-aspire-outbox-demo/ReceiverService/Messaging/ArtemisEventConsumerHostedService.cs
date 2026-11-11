// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using ActiveMQ.Artemis.Client;

namespace ReceiverService.Messaging
{
    public class ArtemisEventConsumerHostedService : BackgroundService, IAsyncDisposable
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<ArtemisEventConsumerHostedService> _logger;

        private readonly ConnectionFactory _connectionFactory = new();

        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly string _addressName;

        private IConnection? _connection;
        private IConsumer? _consumer;

        public ArtemisEventConsumerHostedService(
            IServiceProvider services,
            IConfiguration configuration,
            ILogger<ArtemisEventConsumerHostedService> logger)
        {
            _services = services;
            _logger = logger;

            // es: "amqp://artemis:artemis@artemis:5672"
            var connString = configuration.GetConnectionString("Artemis")
                              ?? "amqp://artemis:artemis@artemis:5672";

            var uri = new Uri(connString);

            _host = uri.Host;
            _port = uri.IsDefaultPort ? 5672 : uri.Port;

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':', 2);
                _username = parts[0];
                _password = parts.Length > 1 ? parts[1] : string.Empty;
            }
            else
            {
                _username = "artemis";
                _password = "artemis";
            }

            // Address/queue di default
            _addressName = configuration.GetValue<string>("Artemis:Address") ?? "entity-items";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ArtemisEventConsumerHostedService starting");

            var endpoint = ActiveMQ.Artemis.Client.Endpoint.Create(
                _host,
                _port,
                _username,
                _password);

            var failedAttempts = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _connection = await _connectionFactory.CreateAsync(endpoint, stoppingToken);
                    _consumer = await _connection.CreateConsumerAsync(_addressName, RoutingType.Anycast, stoppingToken);
                    failedAttempts = 0;

                    _logger.LogInformation(
                        "Connected to Artemis at {Host}:{Port}, listening on address {Address}",
                        _host, _port, _addressName);

                    while (true)
                    {
                        var msg = await _consumer.ReceiveAsync(stoppingToken);
                        await HandleMessageAsync(_consumer, msg, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    failedAttempts++;
                    var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(failedAttempts, 5))));
                    _logger.LogWarning(ex, "Artemis consumer failed (attempt {Attempt}), reconnecting in {Delay}", failedAttempts, delay);

                    await CloseConnectionAsync();

                    try
                    {
                        await Task.Delay(delay, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }

            _logger.LogInformation("ArtemisEventConsumerHostedService stopping");
        }

        private async Task HandleMessageAsync(IConsumer consumer, Message msg, CancellationToken stoppingToken)
        {
            // Extract correlation ID for distributed tracing
            string? correlationId = msg.CorrelationId;
            if (string.IsNullOrEmpty(correlationId) && msg.ApplicationProperties is not null
                && msg.ApplicationProperties.ContainsKey("CorrelationId"))
            {
                correlationId = msg.ApplicationProperties["CorrelationId"]?.ToString();
            }
            correlationId ??= Guid.NewGuid().ToString();

            using var activity = new System.Diagnostics.Activity("ProcessMessage")
                .SetTag("messaging.message_id", msg.MessageId)
                .SetTag("messaging.correlation_id", correlationId)
                .SetTag("messaging.destination", _addressName)
                .Start();

            using var logScope = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });

            try
            {
                await using var scope = _services.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IntegrationEventProcessor>();

                var result = await processor.ProcessAsync(msg.Subject, TryGetBody(msg), stoppingToken);

                if (result == MessageProcessingResult.Processed)
                {
                    // Acknowledge only after the projection has been committed.
                    await consumer.AcceptAsync(msg);
                }
                else
                {
                    // Poison message: the broker routes rejected messages to the dead-letter address.
                    _logger.LogWarning("Rejecting message {MessageId}; it will be moved to the dead-letter address", msg.MessageId);
                    consumer.Reject(msg);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Leave the message unsettled: the broker redelivers it once the link is closed.
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageId}, requesting redelivery", msg.MessageId);

                // Avoid a tight redelivery loop while the failure (e.g. database outage) persists.
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

                // Redelivered by the broker; after max-delivery-attempts it is moved to the dead-letter address.
                consumer.Modify(msg, deliveryFailed: true, undeliverableHere: false);
            }
        }

        private string? TryGetBody(Message msg)
        {
            try
            {
                return msg.GetBody<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Message {MessageId} does not contain a string body", msg.MessageId);
                return null;
            }
        }

        private async Task CloseConnectionAsync()
        {
            var consumer = _consumer;
            var connection = _connection;
            _consumer = null;
            _connection = null;

            try
            {
                if (consumer is not null)
                {
                    await consumer.DisposeAsync();
                }

                if (connection is not null)
                {
                    await connection.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error while closing the Artemis connection");
            }
        }

        public ValueTask DisposeAsync() => new(CloseConnectionAsync());
    }
}
