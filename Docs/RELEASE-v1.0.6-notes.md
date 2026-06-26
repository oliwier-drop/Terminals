# Terminals (SSH.NET fork) — v1.0.6

**Not affiliated with [Terminals-Origin/Terminals](https://github.com/Terminals-Origin/Terminals).**

Hotfix after v1.0.5 — SSH terminal was blank (gray square) because SkiaSharp runtime files were missing from the MSI/ZIP.

## What's fixed

### SSH (SSH.NET plugin)

- **SkiaSharp packaging** — `SkiaSharp.dll` and native `libSkiaSharp` (`win-x86`, `win-x64`, `win-arm64`) are now included in the WiX installer and portable ZIP under `Plugins\SshNet\runtimes\...`.
- **Native layout** — native libraries use the standard `runtimes/win-*/native/` layout expected by SkiaSharp 2.88.

## Upgrade from v1.0.5

- MSI major upgrade replaces the previous install (same `UpgradeCode`)
- Install folder: `Program Files\Terminals-fork-1.0.6`
- Portable ZIP: extract over your folder or use a new directory

## Files

| Asset | Description |
|-------|-------------|
| `TerminalsSetup_1.0.6.msi` | Per-machine installer (WiX) |
| `Terminals_v1.0.6.zip` | Portable layout |

## Licenses

- Upstream code: **MS-CL** — [LICENSE.md](../LICENSE.md)
- Fork-authored code: **GPL-3.0** — [FORK-AUTHORED.md](../FORK-AUTHORED.md)
