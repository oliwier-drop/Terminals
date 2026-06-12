# Terminals (SSH.NET fork) — v1.0.4

**Not affiliated with [Terminals-Origin/Terminals](https://github.com/Terminals-Origin/Terminals).**

Patch release after v1.0.3 — SSH terminal scroll/selection UX, password masking, and fork-owned update links.

## What's new

### SSH (SSH.NET plugin)

- **Scrollback viewport** — unified follow-tail logic; autoscroll only when you are at the bottom; scrollbar math fixed for WinForms `VScrollBar`
- **Large selection & copy** — selection uses absolute scrollback rows; copy works across hundreds of lines outside the visible viewport
- **Smoother scrolling** — narrower invalidation during scroll and selection; reduced full re-renders
- **Password masking** — `sudo`, SSH, and Polish (`Hasło`) prompts hide typed characters by suppressing local and server echo
- **Selection auto-scroll** — dragging a selection near the top or bottom edge scrolls the viewport

### App & distribution

- **Update checker & links** — startup release notification, About dialog, Options, and Chocolatey/nuspec metadata point to [oliwier-drop/Terminals](https://github.com/oliwier-drop/Terminals) instead of upstream

## Requirements

- Windows 10/11
- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48)

## Upgrade from v1.0.3

- MSI major upgrade replaces the previous install (same `UpgradeCode`)
- Install folder: `Program Files\Terminals-fork-1.0.4`
- Portable ZIP: extract over your existing folder or use a new directory
- SSH favorites and connection profiles are unchanged

## Files

| Asset | Description |
|-------|-------------|
| `TerminalsSetup_1.0.4.msi` | Per-machine installer (WiX) |
| `Terminals_v1.0.4.zip` | Portable layout |

## Licenses

- Upstream code: **MS-CL** — [LICENSE.md](../LICENSE.md)
- Fork-authored code: **GPL-3.0** — [FORK-AUTHORED.md](../FORK-AUTHORED.md)
