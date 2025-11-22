// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using ActiveMQ.Artemis.Client;
using SenderService.Data.Entities;

namespace SenderService.Outbox
{
    public class ArtemisEventPublisher : IEventPublisher, IAsyncDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();

        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly string _addressName;

        private IConnection? _connection;
        private IProducer? _producer;

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

        private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
        {
            if (_connection is not null && _producer is not null)
                return;

            var endpoint = ActiveMQ.Artemis.Client.Endpoint.Create(
                _host,
                _port,
                _username,
                _password);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            _connection = await _connectionFactory.CreateAsync(endpoint, cts.Token);
            _producer = await _connection.CreateProducerAsync(_addressName, RoutingType.Anycast);
        }

        public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            var artemisMessage = new Message(message.Payload)
            {
                MessageId = message.Id.ToString(),
                Subject = message.Type
            };

            await _producer!.SendAsync(artemisMessage, cancellationToken);
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
        }
    }
}