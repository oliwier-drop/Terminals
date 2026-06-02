# Requirements

This document describes what you need to **build** and **run** this fork of Terminals. For upstream platform notes (RDP client versions, Wine experiments, etc.), see also [Docs/System-Requirements.md](Docs/System-Requirements.md).

## Runtime (end users)

| Requirement | Notes |
|-------------|--------|
| **Operating system** | Windows (same as upstream Terminals). Tested on Windows 10/11. |
| **.NET Framework** | **4.0 or later** (4.8 recommended). The solution targets .NET Framework 4.0; newer Windows versions include a compatible runtime. |
| **PuTTY** (optional) | Required only for the legacy **SSH** protocol plugin (`putty.exe` in `Resources` or PATH). Not required for the **SSH.NET** plugin. |
| **RDP / VNC / other protocols** | Same dependencies as [upstream system requirements](Docs/System-Requirements.md) for each protocol you use. |

### SSH.NET plugin

- Outbound TCP to the SSH server (default port **22**).
- Credentials: password and/or private key (`KeyFile` path or `KeyTag` entry in application SSH key store).
- On first connect to a host, you must confirm the server host key (known-hosts file under `%AppData%\Terminals\SshKnownHosts.xml`).

## Build (developers)

| Requirement | Notes |
|-------------|--------|
| **OS** | Windows x64 (recommended for tooling). |
| **MSBuild** | Visual Studio 2022 **Build Tools** or full Visual Studio with **.NET desktop development** workload. |
| **.NET Framework targeting pack** | **4.8** (installed with VS Build Tools). Projects declare `v4.0`; build with `/p:TargetFrameworkVersion=v4.8` on modern toolchains (see below). |
| **.NET SDK** (optional) | SDK 8.x is useful for `dotnet msbuild` and general tooling; not strictly required if you use VS MSBuild. |
| **NuGet** | Restore packages before the first build (`nuget restore` on `Source/Terminals.sln`). |

### NuGet / key dependencies

| Package | Used by |
|---------|---------|
| [SSH.NET](https://www.nuget.org/packages/SSH.NET) **2020.0.2** | `Terminals.Plugins.SshNet` |
| log4net, Moq, Entity Framework, etc. | See `packages.config` in each project under `Source/` |

### Build commands

From the repository root (PowerShell):

```powershell
# 1. Restore NuGet packages (once, or after clone)
nuget restore Source\Terminals.sln

# 2. Build Debug (recommended on Build Tools without .NET 4.0 targeting pack)
& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" `
  Source\Terminals.sln `
  /p:Configuration=Debug `
  /p:TargetFrameworkVersion=v4.8 `
  /m
```

Output (main app): `Build\Output\Debug\Terminals.exe`  
SSH.NET plugin: `Build\Output\Debug\Plugins\SshNet\Terminals.Plugins.SshNet.dll`

Release configuration: use `/p:Configuration=Release` (see [Docs/Developer-guide.md](Docs/Developer-guide.md) for release/setup steps).

### Tests

Build the **Tests** project, then run unit tests from Visual Studio Test Explorer, or install the **Testing tools** workload in Build Tools and use `vstest.console.exe` on `Source\Tests\bin\Debug\Tests.dll`.

SSH.NET-related tests live under `Source\Tests\SshNet\`.

## Optional (upstream / full release pipeline)

- **WiX Toolset** — installer build (`Build\installprerequisities.ps1`, Chocolatey).
- **SQL Server** — only if using database-backed favorites (see test `app.config` and developer guide).
- **Visual Studio 2017+** — matches upstream documentation; 2022 Build Tools is sufficient for this fork.

## Quick install (winget)

Example toolchain used for CI-style local builds:

```powershell
winget install Microsoft.DotNet.SDK.8
winget install Microsoft.VisualStudio.2022.BuildTools
# In Visual Studio Installer, add: .NET Framework 4.8 targeting pack + MSBuild
```

Then run `nuget restore` and MSBuild as above.
