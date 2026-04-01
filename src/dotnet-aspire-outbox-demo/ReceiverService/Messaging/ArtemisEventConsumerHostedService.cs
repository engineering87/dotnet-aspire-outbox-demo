// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using ActiveMQ.Artemis.Client;
using Microsoft.EntityFrameworkCore;
using ReceiverService.Data;
using ReceiverService.Data.Entities;
using System.Text.Json;

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

            // Connessione e consumer con retry
            var endpoint = ActiveMQ.Artemis.Client.Endpoint.Create(
                _host,
                _port,
                _username,
                _password);

            const int maxConnectionRetries = 5;
            for (int attempt = 1; attempt <= maxConnectionRetries; attempt++)
            {
                try
                {
                    _connection = await _connectionFactory.CreateAsync(endpoint, stoppingToken);
                    _consumer = await _connection.CreateConsumerAsync(_addressName, RoutingType.Anycast);
                    break;
                }
                catch (Exception ex) when (attempt < maxConnectionRetries && !stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Failed to connect to Artemis (attempt {Attempt}/{MaxRetries}), retrying...", attempt, maxConnectionRetries);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), stoppingToken);
                }
            }

            if (_connection is null || _consumer is null)
            {
                _logger.LogError("Failed to establish connection to Artemis after {MaxRetries} attempts", maxConnectionRetries);
                return;
            }

            _logger.LogInformation(
                "Connected to Artemis at {Host}:{Port}, listening on address {Address}",
                _host, _port, _addressName);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Receive con CancellationToken per shutdown pulito
                    var msg = await _consumer.ReceiveAsync(stoppingToken);

                    var bodyJson = msg.GetBody<string>();
                    var type = msg.Subject;

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

                    using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
                    {
                        if (type == "EntityItemCreated")
                        {
                            await HandleEntityItemCreated(bodyJson, correlationId, stoppingToken);
                        }
                        else
                        {
                            _logger.LogWarning("Received message with unknown type {Type}", type);
                        }

                        // Ack del messaggio solo dopo elaborazione riuscita
                        await _consumer.AcceptAsync(msg);
                    }
                }
                catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // shutdown richiesto → esco dal loop senza loggare errori
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while consuming message from Artemis");
                    // piccola pausa per evitare loop serrato in caso di errori continui
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }

            _logger.LogInformation("ArtemisEventConsumerHostedService stopping");
        }

        private async Task HandleEntityItemCreated(string json, string correlationId, CancellationToken ct)
        {
            EntityItemCreatedDto? dto;

            try
            {
                dto = JsonSerializer.Deserialize<EntityItemCreatedDto>(json, _jsonOptions);
                if (dto is null)
                {
                    _logger.LogWarning("Unable to deserialize EntityItemCreatedDto from payload: {Payload}", json);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing EntityItemCreatedDto from payload: {Payload}", json);
                return;
            }

            _logger.LogInformation("Processing EntityItemCreated for {EntityId} with CorrelationId {CorrelationId}", dto.Id, correlationId);

            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ReceiverDbContext>();

            var existing = await db.EntityItems
                .FirstOrDefaultAsync(e => e.Id == dto.Id, ct);

            if (existing is null)
            {
                existing = new EntityItemProjection
                {
                    Id = dto.Id,
                    Name = dto.Name,
                    Value = dto.Value,
                    CreatedAt = dto.CreatedAt,
                    ReceivedAt = DateTime.UtcNow
                };

                db.EntityItems.Add(existing);
            }
            else
            {
                existing.Name = dto.Name;
                existing.Value = dto.Value;
                // opzionale: puoi aggiornare anche CreatedAt/ReceivedAt se ti serve
            }

            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Successfully processed EntityItemCreated for {EntityId}", dto.Id);
        }

        public async ValueTask DisposeAsync()
        {
            if (_consumer is not null)
            {
                await _consumer.DisposeAsync();
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }
        }

        // DTO di supporto per deserializzazione dell'evento
        private sealed record EntityItemCreatedDto(
            Guid Id,
            string Name,
            decimal Value,
            DateTime CreatedAt);

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
    }
}
