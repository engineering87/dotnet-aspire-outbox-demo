# Transactional Outbox with .NET Aspire

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![GitHub issues](https://img.shields.io/github/issues/engineering87/dotnet-aspire-outbox-demo)](https://github.com/engineering87/dotnet-aspire-outbox-demo/issues)
[![C#](https://img.shields.io/static/v1?label=Language&message=C%23&color=blueviolet)](https://dotnet.microsoft.com/languages/csharp)
[![GitHub stars](https://img.shields.io/github/stars/engineering87/dotnet-aspire-outbox-demo?style=social)](https://github.com/engineering87/dotnet-aspire-outbox-demo)

A reference application demonstrating reliable event publication with the **Transactional Outbox Pattern**, built with .NET 11, .NET Aspire, PostgreSQL, and ActiveMQ Artemis.

## Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Event flow](#event-flow)
- [Getting started](#getting-started)
- [API](#api)
- [Reliability and limitations](#reliability-and-limitations)
- [Technology stack](#technology-stack)
- [License](#license)

## Overview

The sample contains two independently persisted services. `SenderService` accepts an HTTP request, saves a domain entity and its outbox record in one database transaction, and publishes the event asynchronously. `ReceiverService` consumes the event from Artemis and stores a read-side projection in its own PostgreSQL database.

The repository is intended for learning and experimentation. It illustrates the core pattern, but is **not a production-ready template**. Review the [limitations](#reliability-and-limitations) before adapting it for another environment.

## Architecture

| Component | Responsibility | Storage / connection |
| --- | --- | --- |
| `SenderService` | HTTP API, domain writes, outbox publishing | Sender PostgreSQL database; Artemis over AMQP |
| `ReceiverService` | Artemis consumer and projection API | Separate receiver PostgreSQL database; Artemis over AMQP |
| Aspire AppHost | Local orchestration, service references, and resource lifecycle | Artemis container and two PostgreSQL resources |
| `ServiceDefaults` | Shared service configuration, health checks, and telemetry conventions | Referenced by both services |

Aspire starts the dependencies and injects connection information into the services. A container runtime must be available locally.

## Event flow

1. A client submits an entity to `SenderService`.
2. The service writes the entity and a serialized `EntityItemCreated` outbox record in the same database transaction.
3. A background publisher claims pending records and sends them as durable messages to the `entity-items` Artemis address. A record is marked as processed only after the broker accepts the message.
4. After processing the event, `ReceiverService` stores or updates its projection and acknowledges the message only after the database commit.
5. Clients can query the receiver’s projection API.

This makes the sender’s database write and event record atomic. Publishing and projection updates happen asynchronously, so consistency between services is eventual.

## Getting started

### Prerequisites

- .NET 11 SDK
- Docker-compatible container runtime (for example, Docker Desktop or Podman)

### Run the application

From the repository root, start the Aspire AppHost:

```powershell
dotnet run --project src\dotnet-aspire-outbox-demo\dotnet-aspire-outbox-demo\Aspire.AppHost.csproj
```

Aspire starts the services, PostgreSQL instances, and Artemis broker. Use the Aspire dashboard URL printed by the AppHost to inspect resource status, logs, and telemetry.

### Run the tests

```powershell
dotnet test src\dotnet-aspire-outbox-demo\OutboxDemo.Tests\OutboxDemo.Tests.csproj
```

The repository’s `global.json` configures the Microsoft.Testing.Platform runner used by xUnit v3.

## API

The services expose these endpoints:

| Service | Method | Endpoint | Description |
| --- | --- | --- | --- |
| Sender | `POST` | `/api/EntityItems` | Creates an entity and its outbox record |
| Sender | `GET` | `/api/EntityItems` | Lists sender entities |
| Sender | `GET` | `/api/EntityItems/{id}` | Gets a sender entity by ID |
| Receiver | `GET` | `/api/EntityItemsProjection` | Lists received projections |
| Receiver | `GET` | `/api/EntityItemsProjection/{id}` | Gets a projection by ID |

Swagger is enabled in the Development environment. Check the Aspire dashboard for the service URLs assigned at runtime.

## Reliability and limitations

The sample provides **at-least-once** delivery end to end.

- **Atomic local write:** the entity and its outbox record are saved in one transaction.
- **Asynchronous delivery:** the publisher polls every five seconds and processes pending records in batches of 50, immediately continuing while full batches are available.
- **Multiple sender instances:** each batch is claimed with PostgreSQL `FOR UPDATE SKIP LOCKED`, so concurrent publishers never send the same record at the same time. Global ordering across instances is not guaranteed.
- **Retries:** each publish attempt has a 10-second timeout. On failure, the record stays pending, the batch stops, and the record is retried after 30 seconds. Persistent failures are visible through `RetryCount`, `LastError`, and `FailedAt` and require monitoring.
- **Duplicate prevention:** messages carry the outbox ID as Artemis duplicate detection ID (`_AMQ_DUPL_ID`), so a re-published record is accepted but not routed twice while it remains in the broker's duplicate ID cache.
- **Idempotent consumer:** duplicates can still occur (for example, after the duplicate ID cache rotates), so the receiver upserts projections by entity ID.
- **Failure handling in the receiver:** transient processing failures are returned to the broker for redelivery. Unknown event types and invalid payloads are rejected and moved to the `DLQ` address, as are messages that exceed Artemis `max-delivery-attempts` (10 by default). Dead-lettered messages need operational handling.
- **Broker durability:** the Artemis journal is stored in the `artemisdata` volume, so queued messages survive container re-creation.
- **Database initialization:** both services use `EnsureCreated()` without deleting existing data. It does not update an existing schema; introduce EF Core migrations and plan the transition for existing databases before evolving the model.

## Technology stack

- .NET 11 and ASP.NET Core
- .NET Aspire 13
- Entity Framework Core and PostgreSQL
- ActiveMQ Artemis using AMQP
- xUnit v3 and Microsoft.Testing.Platform

## Purpose

Use this repository as a learning reference or a starting point for exploring message-driven services. Before using the sample as a production foundation, address the operational limitations described above, particularly schema migrations, dead-letter handling, and monitoring of pending outbox records.

## License

This project is distributed under the MIT License. See [LICENSE](LICENSE) for details.
