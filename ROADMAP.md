# Fork roadmap

Single place for **planned and completed work** on this Terminals fork (SSH.NET plugin and related changes). Upstream documentation remains under [Docs/](Docs/).

**Status:** `[x]` done · `[ ]` planned · `[~]` in progress

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

### Planned — render performance (incremental)

- [ ] **Dirty rows (1–2 lines)** — on typing, repaint only previous + current cursor row (not full viewport band)
- [ ] **Per-row bitmap cache** — invalidate and redraw changed rows only; scroll shifts cache
- [ ] **Background frame build** — build `frameCache` off UI thread; swap on UI thread to avoid stalls on full refresh

### Planned — target renderer (phase 3)

- [ ] **Glyph atlas terminal renderer** (recommended long-term exit for large / 4K displays)
  - Replace per-span `TextRenderer.DrawText` with fixed cell grid + pre-rasterized Consolas glyphs (or **DirectWrite** / **SkiaSharp** atlas)
  - Keep **VtNetCore** for parsing and buffer; only replace the WinForms paint path
  - Goals: stable cost per keystroke regardless of column count; smooth fullscreen on wide monitors
  - Reference: [VtNetCore.UWP](https://github.com/darrenstarr/VtNetCore.UWP) (paint pattern), Windows Terminal-style glyph pipeline

### Planned — UX (optional)

- [ ] **Local echo (type-ahead)** — show typed characters immediately before server echo returns

### Manual verification (SSH terminal)

| Scenario | Expected |
|----------|----------|
| bash prompt | Correct cursor, wrap, Enter / Backspace |
| `ls --color` | SGR colors |
| `nano` / `vim` | Alternate screen (`?1049`), Ctrl shortcuts |
| Window resize | Log `PTY CxR` matches UI after ~200 ms |
| Fullscreen | Responsive typing (no full-screen redraw per character) |

### Architecture (current)

| Layer | Component |
|-------|-----------|
| Transport | SSH.NET `ShellStream`, PTY resize |
| Parser / buffer | `SshVtSession` → VtNetCore `DataConsumer` + `VirtualTerminalController` |
| UI | `SshTerminalControl` — WinForms GDI+, `GetPageSpans`, `frameCache` |

**PuTTY plugin** remains the option for PuTTY-identical behavior (embedded `putty.exe`). **SSH.NET plugin** is in-process VtNetCore + GDI+.

**References:** [xterm ctlseqs](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html) · [VtNetCore](https://github.com/darrenstarr/VtNetCore)

---

## Not in scope (this fork)

- PuTTY Pageant / X11 in SSH.NET plugin
- Replacing upstream RDP/VNC/Telnet stacks

---

## Changelog (high level)

| When | What |
|------|------|
| Phase 1 | SSH.NET plugin, credentials, known hosts |
| Phase 2 | VtNetCore migration, .NET 4.8, GDI+ terminal |
| Post-2 | Render + keyboard fixes on large displays |
