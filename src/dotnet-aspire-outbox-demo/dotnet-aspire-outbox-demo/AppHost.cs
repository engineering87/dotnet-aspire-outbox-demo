// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
var builder = DistributedApplication.CreateBuilder(args);

// ActiveMQ Artemis broker (AMQP) – container orchestrated by Aspire
var artemis = builder
    .AddContainer("artemis", "quay.io/artemiscloud/activemq-artemis-broker")
    .WithImageTag("latest")
    .WithEndpoint(
        name: "amqp",
        targetPort: 5672
    )
    .WithEnvironment("AMQ_USER", "artemis")
    .WithEnvironment("AMQ_PASSWORD", "artemis");

// PostgreSQL for SenderService
var senderPostgres = builder
    .AddPostgres("sender-postgres")
    .WithImageTag("16")
    .WithDataVolume("senderdbdata");

var senderDb = senderPostgres.AddDatabase("senderdb");

// PostgreSQL for ReceiverService
var receiverPostgres = builder
    .AddPostgres("receiver-postgres")
    .WithImageTag("16")
    .WithDataVolume("receiverdbdata");

var receiverDb = receiverPostgres.AddDatabase("receiverdb");

// SenderService (REST + outbox + Artemis publisher)
builder.AddProject<Projects.SenderService>("senderservice")
    .WithReference(senderDb);

// ReceiverService (Artemis consumer + read-only REST)
builder.AddProject<Projects.ReceiverService>("receiverservice")
    .WithReference(receiverDb);

builder.Build().Run();