# Terminals (SSH.NET fork) — v1.0.2

**Not affiliated with [Terminals-Origin/Terminals](https://github.com/Terminals-Origin/Terminals).**

Patch release after v1.0.1 — SSH.NET upgrade and network-device connection profiles.

## What's new

### SSH (SSH.NET plugin)

- **SSH.NET 2024.2.0** — `rsa-sha2-256` / `rsa-sha2-512` host keys, ETM MACs, improved OpenSSH negotiation
- **Connection profile** (SSH Options panel):
  - **Server (Linux / OpenSSH)** — default; `xterm-256color`, full algorithm set, optional compression
  - **Network device (switch / router)** — constrained KEX/cipher/MAC for Extreme EXOS and similar gear; `vt100` PTY; no MOTD wait; compression disabled
- Connect log includes negotiated host-key and KEX algorithms

## Requirements

- Windows 10/11
- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48)

## Upgrade from v1.0.1

- MSI major upgrade replaces the previous install (same `UpgradeCode`)
- Install folder: `Program Files\Terminals-fork-1.0.2`
- Portable ZIP: extract over your existing folder or use a new directory
- Existing SSH favorites default to **Server** profile (unchanged behavior)
- For Extreme Networks switches with `rsa-sha2-256` host key, set profile to **Network device** in connection SSH Options

## Files

| Asset | Description |
|-------|-------------|
| `TerminalsSetup_1.0.2.msi` | Per-machine installer (WiX) |
| `Terminals_v1.0.2.zip` | Portable layout |

## Licenses

- Upstream code: **MS-CL** — [LICENSE.md](../LICENSE.md)
- Fork-authored code: **GPL-3.0** — [FORK-AUTHORED.md](../FORK-AUTHORED.md)
