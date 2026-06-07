# Terminals (SSH.NET fork) — v1.0.3

**Not affiliated with [Terminals-Origin/Terminals](https://github.com/Terminals-Origin/Terminals).**

Patch release after v1.0.2 — SSH type-ahead, terminal resize UX, SSH.NET 2025.1.0, and tab strip shape fix.

## What's new

### SSH (SSH.NET plugin)

- **Local echo (type-ahead)** — guarded optimistic echo for printable input with server-echo suppression; disabled on alternate screen, paste, and password-like prompts
- **SSH.NET 2025.1.0** — latest transport; public `ShellStream.ChangeWindowSize` for PTY resize (no reflection)
- **Resize UX** — immediate UI repaint (~16 ms) vs debounced PTY sync (~50 ms); alternate-screen apps (nano/vim) defer local buffer resize until SIGWINCH; pixel dimensions sent to PTY
- **Runtime dependencies** — BouncyCastle.Cryptography 2.6.2, Microsoft.Extensions.Logging.Abstractions 8.0.3, and related assemblies bundled in MSI/portable layout

### UI

- **Tab strip** — first visible tab keeps the classic slanted left edge; every other tab uses straight vertical left and right edges so the close (×) button and titles align cleanly

## Requirements

- Windows 10/11
- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48)

## Upgrade from v1.0.2

- MSI major upgrade replaces the previous install (same `UpgradeCode`)
- Install folder: `Program Files\Terminals-fork-1.0.3`
- Portable ZIP: extract over your existing folder or use a new directory
- SSH favorites and connection profiles are unchanged

## Files

| Asset | Description |
|-------|-------------|
| `TerminalsSetup_1.0.3.msi` | Per-machine installer (WiX) |
| `Terminals_v1.0.3.zip` | Portable layout |

## Licenses

- Upstream code: **MS-CL** — [LICENSE.md](../LICENSE.md)
- Fork-authored code: **GPL-3.0** — [FORK-AUTHORED.md](../FORK-AUTHORED.md)
