# Aspire Upgrade Assessment

| | Current | Target |
|---|---|---|
| Aspire | SDK 9.5.0 / Hosting 13.2.0 / client 9.5.2 (mixed) | 13.6.5 |
| TFM | net9.0 (6 projects) | net10.0 (latest installed SDK: 10.0.402) |

## Root cause of the current dependency failure
- AppHost uses legacy `<Sdk Name="Aspire.AppHost.Sdk" Version="9.5.0" />` while `Aspire.Hosting.*` are 13.2.0 (require net10.0) and the project targets net9.0.
- Aspire 13.x client packages (Npgsql EF) require EF Core 10 / Npgsql 10 on net10.0; `Microsoft.EntityFrameworkCore`/`OpenApi` 11.0.0 (latest on NuGet) require net11.0, which no installed SDK supports → must pin Microsoft.* 10.0.x.

## Projects
Aspire.AppHost, ServiceDefaults, SenderService, ReceiverService, OutboxDemo.Tests.

## Package targets
- Aspire.* → 13.6.5; Microsoft.EntityFrameworkCore / AspNetCore.OpenApi / Mvc.Testing / EF InMemory → 10.0.15
- ArtemisNetClient 3.1.2, Swashbuckle latest net10-compatible, test packages latest (xunit 2.9.3, runner 3.x compatible with xunit v2, Test.Sdk 18.x, coverlet, Moq, FluentAssertions)

## Format
- Legacy `<Sdk>` child element → consolidate to `Sdk="Aspire.AppHost.Sdk/13.6.5"`; remove `Aspire.Hosting.AppHost` package.
