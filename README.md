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

## Building and running

- **Requirements (this fork):** [Requirements.md](Requirements.md) — toolchain, .NET, NuGet, build commands.
- **Upstream guides:** [Developer guide](/Docs/Developer-guide.md), [System requirements](/Docs/System-Requirements.md).

## Disclaimer

**Use and installation are at your own risk.**

This fork is experimental development software. It is not an official release of Terminals-Origin, has not been audited for security, and may be unstable or incomplete. Do not use it for production or security-sensitive environments without your own testing and review. The authors and contributors are not liable for any damage or data loss arising from use of this software.

## Contributing

Issues and pull requests are welcome on this fork.

- Changes to **GPL-licensed** components (`Terminals.Plugins.SshNet`, etc.) should be contributed under **GPL-3.0** (compatible with the existing license on that code).
- Changes to **MS-CL** upstream code should follow [MS-CL](LICENSE.md) terms; consider also proposing them to [Terminals-Origin/Terminals](https://github.com/Terminals-Origin/Terminals) when they belong in the main project.
