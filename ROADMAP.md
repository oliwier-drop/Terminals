# Fork roadmap

Single place for **planned and completed work** on this Terminals fork (SSH.NET plugin and related changes). Upstream documentation remains under [Docs/](Docs/).

**Status:** `[x]` done · `[ ]` planned · `[~]` in progress · `[—]` superseded

**Licensing:** fork-authored code is **GPL-3.0** — see [FORK-AUTHORED.md](FORK-AUTHORED.md) and `SPDX-License-Identifier` headers in source files.

---

## SSH.NET plugin (`Terminals.Plugins.SshNet`)

### Done

- [x] SSH.NET **2020.0.2** transport (auth, known hosts, deferred connect, `ShellStream`)
- [x] Retarget host + plugin + tests to **.NET Framework 4.8**
- [x] **VtNetCore** 1.0.30 — replace `AnsiTerminalScreen` + `RichTextBox` with `SshVtSession` + GDI+ `SshTerminalControl`
- [x] PTY sizing aligned with UI (Consolas cell metrics, debounced resize, `IPostConnectTerminalSync`)
- [x] Keyboard: arrows, function keys, Ctrl/Alt letters (`GetKeySequence`), paste (Ctrl+V / Shift+Insert)
- [x] Render performance (baseline): frame bitmap cache, 16 ms coalesce, monospace grid paint, color parse cache
- [x] Render performance (large screens): partial repaint band around cursor for local echo; full repaint on ANSI / scroll / resize
- [x] Unit tests: `SshVtSessionTests` (replaces `AnsiTerminalScreenTests`)

### Superseded — incremental GDI+ optimizations

The items below were planned as intermediate wins on the current `TextRenderer.DrawText` path. **They are deferred** in favor of the modern renderer (phase 3): they add complexity that would be discarded on migration and do not fix **O(columns)** cost per keystroke on wide / 4K displays.

- [—] **Dirty rows (1–2 lines)** — on typing, repaint only previous + current cursor row
- [—] **Per-row bitmap cache** — invalidate and redraw changed rows only; scroll shifts cache
- [—] **Background frame build** — build `frameCache` off UI thread; swap on UI thread

### In progress — modern terminal renderer (phase 3)

**Goal:** responsive SSH terminal on **every screen** — laptop panels, ultrawide, and 4K fullscreen — with stable typing latency regardless of column count.

**Strategy:** keep **VtNetCore** for parsing and buffer (`SshVtSession`); replace the WinForms paint path in `SshTerminalControl` with a **fixed cell grid + glyph atlas** (Windows Terminal–style pipeline). Do not invest further in span-based `DrawText` optimizations.

- [~] **Phase 3 — modern renderer** (active focus)
  - [ ] **Cell grid** — map `VirtualTerminalController` viewport to a dense cell buffer (char + fg/bg + attributes per cell)
  - [ ] **Glyph atlas** — pre-rasterize Consolas (normal / bold / italic / bold-italic) into a texture atlas; blit per cell instead of `TextRenderer.DrawText` per span
  - [ ] **Invalidation** — repaint changed rows only; efficient scroll (shift row cache / blit) — subsumes the superseded dirty-row and per-row cache ideas in the new architecture
  - [ ] **Hi-DPI** — correct cell metrics and atlas scale on high-DPI displays
  - [ ] **Optional later:** DirectWrite or SkiaSharp atlas if GDI+ limits are hit; parser layer unchanged
  - **References:** [VtNetCore.UWP](https://github.com/darrenstarr/VtNetCore.UWP) (paint pattern), [xterm ctlseqs](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html), Windows Terminal glyph pipeline

### Planned — UX (optional, after or parallel to phase 3)

- [ ] **Local echo (type-ahead)** — show typed characters immediately before server echo returns (easier once cell-level invalidation exists)

### Manual verification (SSH terminal)

| Scenario | Expected |
|----------|----------|
| bash prompt | Correct cursor, wrap, Enter / Backspace |
| `ls --color` | SGR colors |
| `nano` / `vim` | Alternate screen (`?1049`), Ctrl shortcuts |
| Window resize | Log `PTY CxR` matches UI after ~200 ms |
| Fullscreen / 4K / ultrawide | Responsive typing; no perceptible lag per keystroke |
| Hi-DPI display | Sharp glyphs; correct cell size vs. PTY dimensions |

### Architecture

| Layer | Current | Target (phase 3) |
|-------|---------|------------------|
| Transport | SSH.NET `ShellStream`, PTY resize | unchanged |
| Parser / buffer | `SshVtSession` → VtNetCore `DataConsumer` + `VirtualTerminalController` | unchanged |
| UI | `SshTerminalControl` — GDI+, `GetPageSpans`, `frameCache` | `SshTerminalControl` — cell grid + glyph atlas blit |

**PuTTY plugin** remains the option for PuTTY-identical behavior (embedded `putty.exe`). **SSH.NET plugin** is in-process VtNetCore + modern cell renderer.

**References:** [VtNetCore](https://github.com/darrenstarr/VtNetCore)

---

## Not in scope (this fork)

- PuTTY Pageant / X11 in SSH.NET plugin
- Replacing upstream RDP/VNC/Telnet stacks
- Further incremental `DrawText` / span-cache tuning (superseded by phase 3)

---

## Changelog (high level)

| When | What |
|------|------|
| Phase 1 | SSH.NET plugin, credentials, known hosts |
| Phase 2 | VtNetCore migration, .NET 4.8, GDI+ terminal |
| Post-2 | Render + keyboard fixes on large displays |
| Phase 3 (planned) | Glyph atlas renderer; good UX on all screen sizes; incremental GDI+ path superseded |
