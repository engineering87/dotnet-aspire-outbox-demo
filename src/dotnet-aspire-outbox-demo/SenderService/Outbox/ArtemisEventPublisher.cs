// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using ActiveMQ.Artemis.Client;
using SenderService.Data.Entities;

namespace SenderService.Outbox
{
    public class ArtemisEventPublisher : IEventPublisher, IAsyncDisposable
    {
        public const string DuplicateDetectionIdProperty = "_AMQ_DUPL_ID";

        private readonly ConnectionFactory _connectionFactory = new();

        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly string _addressName;

        private readonly SemaphoreSlim _initLock = new(1, 1);
        private IConnection? _connection;
        private volatile IProducer? _producer;

        public ArtemisEventPublisher(IConfiguration configuration)
        {
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

            _addressName = configuration.GetValue<string>("Artemis:Address") ?? "entity-items";
        }

        private async Task<IProducer> EnsureInitializedAsync(CancellationToken cancellationToken)
        {
            if (_producer is { } existing)
                return existing;

            await _initLock.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring lock
                if (_producer is { } initialized)
                    return initialized;

                var endpoint = ActiveMQ.Artemis.Client.Endpoint.Create(
                    _host,
                    _port,
                    _username,
                    _password);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var connection = await _connectionFactory.CreateAsync(endpoint, cts.Token);
                try
                {
                    var producer = await connection.CreateProducerAsync(_addressName, RoutingType.Anycast, cts.Token);
                    _connection = connection;
                    _producer = producer;
                    return producer;
                }
                catch
                {
                    await connection.DisposeAsync();
                    throw;
                }
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            var producer = await EnsureInitializedAsync(cancellationToken);

            var artemisMessage = new Message(message.Payload)
            {
                MessageId = message.Id.ToString(),
                Subject = message.Type,
                CorrelationId = message.CorrelationId,
                DurabilityMode = DurabilityMode.Durable
            };

            // Also add as application property for broader compatibility
            artemisMessage.ApplicationProperties["CorrelationId"] = message.CorrelationId;

            // Broker-side duplicate detection: a re-publish of the same outbox record (e.g. after a timeout
            // whose send actually reached the broker) is accepted but not routed again.
            artemisMessage.ApplicationProperties[DuplicateDetectionIdProperty] = message.Id.ToString();

            // Completes only when the broker has accepted (and, for durable messages, persisted) the message.
            await producer.SendAsync(artemisMessage, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (_producer is not null)
            {
                await _producer.DisposeAsync();
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            _initLock.Dispose();
        }
    }
}