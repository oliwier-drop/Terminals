# Terminals (Fork)

This repository is a **fork** of the open-source [Terminals](https://github.com/Terminals-Origin/Terminals) project — a secure, multi-tab terminal services and remote desktop client (RDP, VNC, VMRC, SSH, Telnet, RAS, ICA Citrix, HTTP/HTTPS, and more).

## What this fork is for

This fork focuses on **rewriting the SSH connection layer** using [SSH.NET](https://github.com/sshnet/SSH.NET) so that SSH sessions support **modern encryption algorithms** and remain compatible with current SSH servers. Other protocols and features largely follow the upstream codebase unless changes are required for the new SSH stack.

Work in progress: the `Terminals.Plugins.SshNet` plugin and related integration.

The SSH.NET plugin uses **SSH.NET 2020.0.2** (upgraded from 2016.1.0) for improved algorithms and key support. Authentication supports password, private keys (`KeyFile` / `KeyTag` + application SSH key store), and keyboard-interactive prompts. Host keys are verified against a persisted known-hosts file with a trust prompt (no silent auto-trust). PuTTY Pageant and X11 forwarding are not supported by this plugin.

### Roadmap

Fork features and status (SSH.NET, terminal renderer, performance): **[ROADMAP.md](ROADMAP.md)**.

### Which code is fork-authored (GPL-3.0)

Open-source components written for this fork are listed in **[FORK-AUTHORED.md](FORK-AUTHORED.md)**. GPL sources are marked with `SPDX-License-Identifier: GPL-3.0-or-later` at the top of each file. See also [NOTICE](NOTICE) and [LICENSE.md](LICENSE.md#gnu-general-public-license-v30--fork-authored-code) (GPL-3.0 section).

The SSH.NET plugin uses **VtNetCore** for xterm-grade in-process display (.NET Framework 4.8). For PuTTY-identical behavior, use the legacy **PuTTY** SSH connection type.

## Upstream project

| | |
|---|---|
| **Repository** | [Terminals-Origin/Terminals](https://github.com/Terminals-Origin/Terminals) |
| **Releases** | [Releases on GitHub](https://github.com/Terminals-Origin/Terminals/releases) |
| **Documentation** | [Docs](/Docs/) (from upstream tree) |
| **Upstream license** | [Microsoft Shared Source Community License (MS-CL)](LICENSE.md) |

## License (mixed)

This repository contains **two licenses**. Together, the distribution is **fully open source**, but you must respect the terms that apply to each part.

| Codebase | License | Scope |
|----------|---------|--------|
| **Original Terminals code** (from [Terminals-Origin/Terminals](https://github.com/Terminals-Origin/Terminals)) | [**MS-CL**](LICENSE.md) — Microsoft Shared Source Community License | Most of the tree: core app, RDP/VNC/Telnet plugins, legacy PuTTY SSH plugin, shared libraries, docs carried from upstream, etc. |
| **Fork-authored code** (written for this fork) | [**GNU GPL v3**](LICENSE.md#gnu-general-public-license-v30--fork-authored-code) | Primarily `Terminals.Plugins.SshNet`, related tests, and supporting changes required for the new SSH stack |

**In short:**

- Upstream-derived files remain under **MS-CL** (see [LICENSE.md](LICENSE.md)).
- New and substantially rewritten code by the fork maintainer(s) is released under **GPL-3.0** so the SSH.NET work stays **complete open source** with copyleft protection.

If you redistribute binaries or source that combine both parts, you need to comply with **MS-CL** for the upstream portions (including source availability rules in that license) and **GPL-3.0** for the GPL-covered portions (including providing corresponding source for those components). This is not legal advice; consult a lawyer for commercial or combined-product distribution.

## Requirements

What you need to **build** and **run** this fork. For upstream platform notes (RDP client versions, Wine experiments, etc.), see also [Docs/System-Requirements.md](Docs/System-Requirements.md).

### Runtime (end users)

| Requirement | Notes |
|-------------|--------|
| **Operating system** | Windows (same as upstream Terminals). Tested on Windows 10/11. |
| **.NET Framework** | **4.8** (required). The main app and SSH.NET plugin target .NET Framework 4.8. |
| **PuTTY** (optional) | Required only for **Telnet** (`putty.exe` in `Resources` or PATH). Not required for **SSH** (SSH.NET plugin). |
| **RDP / VNC / other protocols** | Same dependencies as [upstream system requirements](Docs/System-Requirements.md) for each protocol you use. |

#### SSH plugin (SSH.NET)

- Outbound TCP to the SSH server (default port **22**).
- Credentials: password and/or private key (`KeyFile` path or `KeyTag` entry in application SSH key store).
- On first connect to a host, you must confirm the server host key (known-hosts file under `%AppData%\Terminals\SshKnownHosts.xml`).

### Build (developers)

| Requirement | Notes |
|-------------|--------|
| **OS** | Windows x64 (recommended for tooling). |
| **MSBuild** | Visual Studio 2022 **Build Tools** or full Visual Studio with **.NET desktop development** workload. |
| **.NET Framework targeting pack** | **4.8** (installed with VS Build Tools). All solution projects target **.NET Framework 4.8**. |
| **.NET SDK** (optional) | SDK 8.x is useful for `dotnet msbuild` and general tooling; not strictly required if you use VS MSBuild. |
| **NuGet** | Restore packages before the first build (`nuget restore` on `Source/Terminals.sln`). |

#### NuGet / key dependencies

| Package | Used by |
|---------|---------|
| [SSH.NET](https://www.nuget.org/packages/SSH.NET) **2020.0.2** | `Terminals.Plugins.SshNet` |
| [VtNetCore](https://www.nuget.org/packages/VtNetCore) **1.0.30** | `Terminals.Plugins.SshNet` (terminal emulation) |
| log4net, Moq, Entity Framework, etc. | See `packages.config` in each project under `Source/` |

Phase 3 SSH terminal rendering uses **GDI+ only** (glyph atlas in `Terminals.Plugins.SshNet/Rendering/`); no additional NuGet packages.

#### Build commands

From the repository root (PowerShell):

```powershell
# 1. Restore NuGet packages (once, or after clone)
nuget restore Source\Terminals.sln

# 2. Build Debug
& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" `
  Source\Terminals.sln `
  /p:Configuration=Debug `
  /m
```

Output (main app): `Build\Output\Debug\Terminals.exe`  
SSH.NET plugin: `Build\Output\Debug\Plugins\SshNet\Terminals.Plugins.SshNet.dll`

Release configuration: use `/p:Configuration=Release` (see [Docs/Developer-guide.md](Docs/Developer-guide.md) for release/setup steps).

#### Tests

Build the **Tests** project, then run unit tests from Visual Studio Test Explorer, or install the **Testing tools** workload in Build Tools and use `vstest.console.exe` on `Source\Tests\bin\Debug\Tests.dll`.

SSH.NET-related tests live under `Source\Tests\SshNet\`.

### Optional (upstream / full release pipeline)

- **WiX Toolset** — installer build (`Build\installprerequisities.ps1`, Chocolatey).
- **SQL Server** — only if using database-backed favorites (see test `app.config` and developer guide).
- **Visual Studio 2017+** — matches upstream documentation; 2022 Build Tools is sufficient for this fork.

### Quick install (winget)

Example toolchain used for CI-style local builds:

```powershell
winget install Microsoft.DotNet.SDK.8
winget install Microsoft.VisualStudio.2022.BuildTools
# In Visual Studio Installer, add: .NET Framework 4.8 targeting pack + MSBuild
```

Then run `nuget restore` and MSBuild as above.

### Upstream guides

- [Developer guide](/Docs/Developer-guide.md)
- [System requirements](/Docs/System-Requirements.md) (upstream protocols)

## Disclaimer

**Use and installation are at your own risk.**

This fork is experimental development software. It is not an official release of Terminals-Origin, has not been audited for security, and may be unstable or incomplete. Do not use it for production or security-sensitive environments without your own testing and review. The authors and contributors are not liable for any damage or data loss arising from use of this software.

## Contributing

Issues and pull requests are welcome on this fork.

- Changes to **GPL-licensed** components (`Terminals.Plugins.SshNet`, etc.) should be contributed under **GPL-3.0** (compatible with the existing license on that code).
- Changes to **MS-CL** upstream code should follow [MS-CL](LICENSE.md) terms; consider also proposing them to [Terminals-Origin/Terminals](https://github.com/Terminals-Origin/Terminals) when they belong in the main project.
