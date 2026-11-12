# 02-aspire-packages: Upgrade Aspire SDK and packages, consolidate AppHost SDK

Consolidate AppHost to `Sdk="Aspire.AppHost.Sdk/13.6.5"`, remove redundant `Aspire.Hosting.AppHost`, bump all Aspire packages to 13.6.5, and resolve version conflicts by aligning Microsoft.* packages to 10.0.x and updating remaining packages.

**Done when**: restore succeeds with no conflicts/downgrade warnings.

## Research
- AppHost: legacy <Sdk Name=Aspire.AppHost.Sdk 9.5.0> + Aspire.Hosting.* 13.2.0 -> mixed versions = root cause of restore failure.
- Sender/Receiver: Aspire.Npgsql.EFCore.PostgreSQL 9.5.2 -> 13.6.5 (needs EF Core >=10.0.14, Npgsql 10.0.3).
- Microsoft.* 11.0.0 targets net11 only -> pin 10.0.15.
- Swashbuckle 11.0.0 supports net10 (Microsoft.OpenApi 2.x breaking namespace changes expected).
- Tests: Test.Sdk 18.11.1, runner 4.0.1, coverlet 10.1.0, xunit 2.9.3, Moq 4.21.0, FluentAssertions 8.11.0.
- NU1107: Swashbuckle 11 needs Microsoft.OpenApi>=3.11 vs AspNetCore.OpenApi 10 <3.0 -> use Swashbuckle 10.3.1 (OpenApi 2.x). Breaking: Microsoft.OpenApi.Models namespace -> Microsoft.OpenApi.
