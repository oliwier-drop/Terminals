# Terminals (SSH.NET fork) — v1.0.1

**Not affiliated with [Terminals-Origin/Terminals](https://github.com/Terminals-Origin/Terminals).**

Patch release after v1.0.0 — UI stability on high-DPI displays and SSH session quality.

## What's new

### UI / layout
- **Toolbar recovery** — menu and standard toolbar stay visible after fullscreen and PerMonitorV2 layout save/reload
- **DPI-aware dialogs** — Settings, New Connection, and connection edit panels scale correctly on high-DPI monitors (no clipped fields)

### SSH (SSH.NET plugin)
- **256-color PTY** — negotiates `xterm-256color` for `ls --color`, `ip -c a`, and common SGR output
- **Responsive connect** — SSH handshake runs off the UI thread; app stays closable during connect
- **Connect feedback** — explicit timeout message (30 s), single-line connecting status, clear screen before shell MOTD
- **Auth / host key** — existing prompts unchanged (credentials, host key trust, MessageBox on failure)

### Installer
- Publisher metadata: **Oliwier Drop** (was `oliwier-drop` in MSI)
- Optional **Authenticode signing** — see [CODE_SIGNING.md](CODE_SIGNING.md); signed builds show a trusted publisher in Windows

## Requirements

- Windows 10/11
- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48)

## Upgrade from v1.0.0

- MSI major upgrade replaces the previous install (same `UpgradeCode`)
- Install folder: `Program Files\Terminals-fork-1.0.1`
- Portable ZIP: extract over your existing folder or use a new directory

## Files

| Asset | Description |
|-------|-------------|
| `TerminalsSetup_1.0.1.msi` | Per-machine installer (WiX) |
| `Terminals_v1.0.1.zip` | Portable layout |

## Licenses

- Upstream code: **MS-CL** — [LICENSE.md](../LICENSE.md)
- Fork-authored code: **GPL-3.0** — [FORK-AUTHORED.md](../FORK-AUTHORED.md)

## Full changelog (commits since v1.0.0)

- `fix(ui): recover toolbar visibility after fullscreen and DPI layout save`
- `fix(ui): scale connection and options dialogs with PerMonitorV2 DPI`
- `fix(ssh): negotiate xterm-256color PTY for 256-color and truecolor output`
- `fix(ssh): keep UI responsive during connect and improve status feedback`
- `docs: use Oliwier Drop as maintainer name in NOTICE and FORK-AUTHORED`
