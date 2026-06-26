# Terminals (SSH.NET fork) — v1.0.5

**Not affiliated with [Terminals-Origin/Terminals](https://github.com/Terminals-Origin/Terminals).**

Performance release after v1.0.4 — SkiaSharp SSH terminal renderer, adaptive display scaling, and fixes for vim/nano and fast server output.

## What's new

### SSH (SSH.NET plugin)

- **SkiaSharp renderer** — replaces the GDI+ per-cell glyph atlas. Rows are painted with run-length background fills and batched text spans instead of thousands of `DrawImage` calls per frame.
- **Row bitmap cache** — dirty rows are rasterized once and blitted to the frame buffer; full grid `Clone()` replaced with `CopyFrom` reuse.
- **Adaptive render scheduler** — small output (typing/local echo) renders immediately; large bursts coalesce at 16 ms; catch-up mode forces full repaints when the PTY backlog grows.
- **Fast output stability** — scroll-bitmap optimization disabled on viewport changes; row deferral removed; `RebuildFullFrame` on full repaint and catch-up to prevent gaps, ghost lines, and duplicate prompts under heavy server output.
- **Alternate screen (vim/nano)** — clears stale alternate-buffer storage on re-entry (`1049h`, `1047h`, `47h`); transition repaint and row-cache invalidation so nano no longer shows leftover vim UI.
- **Display scaling** — font size adapts to monitor DPI and viewport (reference 1600×900). Manual zoom: `Ctrl` + mouse wheel, `Ctrl` + `+`/`-`, `Ctrl` + `0` (reset). PTY resizes automatically after zoom.
- **Skia cell metrics** — monospace cell width/height and glyph advances measured via SkiaSharp (fixes blank terminal after scaling).
- **I/O batching** — SSH read buffer 16 KB; UI output flush in 16 KB chunks; selection overlay reuses the last rendered cell grid.

### Dependencies

- **SkiaSharp 2.88.9** + native `libSkiaSharp` (win-x86, win-x64, win-arm64) shipped with the SSH.NET plugin output.

## Requirements

- Windows 10/11
- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48)

## Upgrade from v1.0.4

- MSI major upgrade replaces the previous install (same `UpgradeCode`)
- Install folder: `Program Files\Terminals-fork-1.0.5`
- Portable ZIP: extract over your existing folder or use a new directory
- SSH favorites and connection profiles are unchanged

## Files

| Asset | Description |
|-------|-------------|
| `TerminalsSetup_1.0.5.msi` | Per-machine installer (WiX) |
| `Terminals_v1.0.5.zip` | Portable layout |

## Licenses

- Upstream code: **MS-CL** — [LICENSE.md](../LICENSE.md)
- Fork-authored code: **GPL-3.0** — [FORK-AUTHORED.md](../FORK-AUTHORED.md)
