# Terminals (SSH.NET fork) — v1.0.7

**Not affiliated with [Terminals-Origin/Terminals](https://github.com/Terminals-Origin/Terminals).**

**Recommended release.** Fixes broken SSH rendering in v1.0.5/v1.0.6 and improves terminal performance.

> **Do not use v1.0.5 or v1.0.6** — SSH terminal did not render correctly. See [WITHDRAWN-RELEASES.md](WITHDRAWN-RELEASES.md).

## What's fixed

### SSH rendering (critical)

- **SkiaSharp native layout** — `libSkiaSharp.dll` is shipped at `Plugins\SshNet\x64\` (and `x86\`, `arm64\`) where .NET Framework 4.8 probes it. v1.0.6 used `runtimes\win-*\native\`, which Skia never loads → blank/gray terminal.
- **Official SkiaSharp.NativeAssets.Win32.targets** — build copies natives to the correct layout; WiX and ZIP packaging updated.

### SSH performance

- **Scroll blit on tail-follow** — viewport scroll reuses the frame bitmap + incremental diff instead of full-screen repaint every line.
- **Render row budget** — up to 32 rows per frame; excess rows deferred across timer ticks.
- **Batch DrawText** — one Skia draw call per text span instead of per character.
- **Gentler catch-up** — large PTY backlog no longer forces full-frame repaint on every tick.
- **Direct frame paint** — small diffs (≤8 rows) paint straight into `frameCache` without per-row bitmap copies.
- **VT parse off UI thread** — `TerminalVtParseWorker` parses ANSI on a background thread; UI only renders.

## Upgrade from v1.0.5 / v1.0.6

- Uninstall the broken build or extract v1.0.7 over your portable folder.
- MSI major upgrade replaces the previous install (same `UpgradeCode`).
- Install folder: `Program Files\Terminals-fork-1.0.7`

## Files

| Asset | Description |
|-------|-------------|
| `TerminalsSetup_1.0.7.msi` | Per-machine installer (WiX) |
| `Terminals_v1.0.7.zip` | Portable layout |

## Licenses

- Upstream code: **MS-CL** — [LICENSE.md](../LICENSE.md)
- Fork-authored code: **GPL-3.0** — [FORK-AUTHORED.md](../FORK-AUTHORED.md)
