# Terminals (Fork)

This repository is a **fork** of the open-source [Terminals](https://github.com/Terminals-Origin/Terminals) project — a secure, multi-tab terminal services and remote desktop client (RDP, VNC, VMRC, SSH, Telnet, RAS, ICA Citrix, HTTP/HTTPS, and more).

## What this fork is for

This fork focuses on **rewriting the SSH connection layer** using [SSH.NET](https://github.com/sshnet/SSH.NET) so that SSH sessions support **modern encryption algorithms** and remain compatible with current SSH servers. Other protocols and features largely follow the upstream codebase unless changes are required for the new SSH stack.

Work in progress: the `Terminals.Plugins.SshNet` plugin and related integration.

## Upstream project

| | |
|---|---|
| **Repository** | [Terminals-Origin/Terminals](https://github.com/Terminals-Origin/Terminals) |
| **Releases** | [Releases on GitHub](https://github.com/Terminals-Origin/Terminals/releases) |
| **Documentation** | [Docs](/Docs/) (from upstream tree) |
| **License** | [Microsoft Shared Source Community License (MS-CL)](LICENSE.md) — same as the [original project](https://github.com/Terminals-Origin/Terminals/blob/master/LICENSE.md) |

This fork is derived from upstream Terminals and remains subject to the terms of that license. See [LICENSE.md](LICENSE.md) in this repository.

## Disclaimer

**Use and installation are at your own risk.**

This fork is experimental development software. It is not an official release of Terminals-Origin, has not been audited for security, and may be unstable or incomplete. Do not use it for production or security-sensitive environments without your own testing and review. The authors and contributors are not liable for any damage or data loss arising from use of this software.

## Building and running

Build instructions follow the upstream [developer guide](/Docs/Developer-guide.md). Requirements and platform notes are described in [system requirements](/Docs/System-Requirements.md).

## Contributing

Issues and pull requests are welcome on this fork. If you intend to contribute changes that belong in the main project, consider opening a pull request against [Terminals-Origin/Terminals](https://github.com/Terminals-Origin/Terminals) as well, where maintainers can review them.
