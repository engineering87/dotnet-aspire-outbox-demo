# 02-aspire-packages: Upgrade Aspire SDK and packages, consolidate AppHost SDK

Consolidate AppHost to `Sdk="Aspire.AppHost.Sdk/13.6.5"`, remove redundant `Aspire.Hosting.AppHost`, bump all Aspire packages to 13.6.5, and resolve version conflicts by aligning Microsoft.* packages to 10.0.x and updating remaining packages.

**Done when**: restore succeeds with no conflicts/downgrade warnings.
