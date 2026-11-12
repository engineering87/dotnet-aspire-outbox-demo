# Aspire Version Upgrade Plan

## Overview

**Target**: Aspire 13.6.5 on .NET 10, all NuGet packages at latest compatible versions
**Scope**: small solution, 5 projects

## Tasks

### 01-upgrade-tfm: Upgrade target framework to net10.0

Update `<TargetFramework>` from net9.0 to net10.0 in all projects.

**Done when**: all projects target net10.0.

---

### 02-aspire-packages: Upgrade Aspire SDK and packages, consolidate AppHost SDK

Consolidate AppHost to `Sdk="Aspire.AppHost.Sdk/13.6.5"`, remove redundant `Aspire.Hosting.AppHost`, bump all Aspire packages to 13.6.5, and resolve version conflicts by aligning Microsoft.* packages to 10.0.x and updating remaining packages.

**Done when**: restore succeeds with no conflicts/downgrade warnings.

---

### 03-build-fix: Build and fix breaking changes

**Done when**: solution builds with no errors and no new upgrade warnings.

---

### 04-validation: Run tests and validate

**Done when**: all tests pass.
