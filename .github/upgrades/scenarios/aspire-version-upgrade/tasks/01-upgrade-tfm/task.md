# 01-upgrade-tfm: Upgrade target framework to net10.0

Update `<TargetFramework>` from net9.0 to net10.0 in all projects.

**Done when**: all projects target net10.0.

## Research
- Confirmed 5 csproj with <TargetFramework>net9.0</TargetFramework>, no Directory.Build.props / global.json. SDK 10.0.402 installed.
