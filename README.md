# Outbox Pattern with .NET Aspire, PostgreSQL and ActiveMQ Artemis
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![issues - dotnet-aspire-outbox-demo](https://img.shields.io/github/issues/engineering87/dotnet-aspire-outbox-demo)](https://github.com/engineering87/dotnet-aspire-outbox-demo/issues)
[![Language - C#](https://img.shields.io/static/v1?label=Language&message=C%23&color=blueviolet)](https://dotnet.microsoft.com/it-it/languages/csharp)
[![stars - dotnet-aspire-outbox-demo](https://img.shields.io/github/stars/engineering87/dotnet-aspire-outbox-demo?style=social)](https://github.com/engineering87/dotnet-aspire-outbox-demo)

*A clean and production-ready template for reliable messaging in distributed systems*

## Table of Contents

- [Overview](#overview)
- [Architecture Overview](#architecture-overview)
  - [1 SenderService](#1-senderservice)
  - [2 ReceiverService](#2-receiverservice)
  - [3 Infrastructure Managed by NET Aspire](#3-infrastructure-managed-by-net-aspire)
- [Why Use the Outbox Pattern?](#why-use-the-outbox-pattern)
  - [Strong Consistency Inside the Service](#strong-consistency-inside-the-service)
  - [Eventual Consistency Between Services](#eventual-consistency-between-services)
- [API Endpoints](#api-endpoints)
- [Reliability Scenarios](#reliability-scenarios)
- [Technical Highlights](#technical-highlights)
- [Purpose of This Template](#purpose-of-this-template)
- [License](#license)
- [Contact](#contact)

## Overview

This repository provides a **complete, professional reference implementation of the Outbox Pattern** using:

- **.NET 9**
- **Entity Framework Core** with PostgreSQL
- **ActiveMQ Artemis** (AMQP broker)
- **.NET Aspire** for orchestration and infrastructure provisioning
- **Background services** for reliable event publishing

The goal is to demonstrate a correct, production-ready approach to:

- Reliable event-driven communication  
- Consistent domain state  
- Guaranteed message delivery  
- Decoupled microservice communication

While this template is didactic, the architecture follows **industry best practices** and can be used as the foundation for real-world solutions.

## Architecture Overview

This solution models a distributed system composed of three cooperating components.

### **1. SenderService**

A REST API that:

- Processes HTTP requests  
- Persists domain data in PostgreSQL  
- Writes corresponding outbox records in the same transaction  
- Uses a `BackgroundService` to publish pending events to ActiveMQ Artemis  

This service demonstrates the full Outbox Pattern workflow.

### **2. ReceiverService**

A consumer microservice that:

- Connects to Artemis using AMQP  
- Listens for and processes messages  
- Persists results in its own PostgreSQL database  

This models a typical "read-side" or downstream consumer in event-driven systems.

### **3. Infrastructure Managed by .NET Aspire**

.NET Aspire automatically provisions and orchestrates:

- ActiveMQ Artemis message broker  
- PostgreSQL servers (one per microservice)  
- Networks and container environments  
- Connection strings and secret injection  
- Logging, health, diagnostics, dashboards  

No Docker Compose, Kubernetes manifests, or manual scripts required.

## Why Use the Outbox Pattern?

When a service needs to:

1. Save domain data to its database, and  
2. Publish an integration event to other services  

the operations are distributed and may fail independently.

The Outbox Pattern eliminates this inconsistency by splitting responsibilities across two consistency domains.

### **Strong Consistency Inside the Service**

All domain writes and outbox entries occur inside a **single atomic EF Core transaction**.

This ensures:

- If the domain entity is written → the outbox event is written  
- If writing fails → nothing is committed  
- The internal state of the service is always correct  
- No “write succeeded, event failed” scenarios

This gives **strong transactional consistency**, where the service’s internal state is never ambiguous.

### **Eventual Consistency Between Services**

Publishing messages happens **outside the transaction**, asynchronously.

A background worker:

- Reads pending outbox messages  
- Publishes them to Artemis  
- Retries if the broker is down  
- Marks messages as processed only on successful delivery  

This results in:

- **At-least-once delivery guarantees**  
- **No message loss**  
- **Graceful recovery from outages**  
- Downstream systems becoming consistent **eventually**

Cross-service consistency is therefore **eventual**, as expected in distributed systems.

✔ Strong consistency internally  
✔ Eventual consistency across microservices  
✔ Reliable event-driven communication

## API Endpoints

### **SenderService**

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/EntityItems` | Creates a new entity + outbox record |
| GET | `/api/EntityItems` | Returns all stored entities |
| GET | `/api/EntityItems/{id}` | Returns a specific entity |

Swagger UI is automatically enabled in Development.

## Reliability Scenarios

### **Broker temporarily unavailable**
- Business data is committed  
- Outbox message is stored  
- Publisher retries indefinitely  
- Message is delivered when the broker becomes available  

➡ Guarantees **strong internal consistency** + **eventual cross-service consistency**.

### **ReceiverService offline**
- Sender continues publishing  
- Artemis buffers messages durably  
- Receiver processes backlog on restart  

➡ Ensures **no message loss** and a resilient event pipeline.

### **Crash after DB commit but before publishing**
- Outbox entry remains in the database  
- Background worker publishes it on next iteration  

➡ Ensures **reliable recovery and eventual delivery**.

## Technical Highlights

### **Transactional Outbox Storage**
Atomic EF Core transaction guarantees consistency of local writes.

### **Background Event Publisher**
Fault-tolerant and continuously retrying.

### **ActiveMQ Artemis Integration**
Uses AMQP and persistent queues for reliability.

### **Idempotent Consumer Logic**
ReceiverService is designed to tolerate re-delivery.

### **Shared App Conventions**
The `ServiceDefaults` library centralizes:

- Swagger
- Health checks
- API metadata
- Common pipeline conventions

Ensures consistency across microservices.

## Purpose of This Template

This repository serves as:

- A **learning reference** for the Outbox Pattern  
- A **realistic blueprint** for building distributed systems  
- A **clean starting template** for message-driven architectures  
- A **teaching tool** for workshops and technical sessions  

It aims to balance clarity, correctness, and production readiness.

## License

Source code is available under the MIT License (see the LICENSE file).

## Contact

For questions or details:  
**francesco.delre[at]protonmail.com**
