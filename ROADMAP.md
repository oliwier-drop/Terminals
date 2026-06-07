# Fork roadmap

Single place for **planned and completed work** on this Terminals fork. Upstream documentation remains under [Docs/](Docs/).

**Status:** `[x]` done · `[ ]` planned · `[~]` in progress · `[—]` superseded

**Licensing:** fork-authored code is **GPL-3.0** — see [FORK-AUTHORED.md](FORK-AUTHORED.md) and `SPDX-License-Identifier` headers in source files.

Release notes: [v1.0.0](Docs/RELEASE-v1.0.0-notes.md) · [v1.0.1](Docs/RELEASE-v1.0.1-notes.md) · [v1.0.2](Docs/RELEASE-v1.0.2-notes.md)

---

### Done — `v1.0.0`

- [x] SSH.NET **2020.0.2** transport (auth, known hosts, deferred connect, `ShellStream`)
- [x] SSH decoupled from PuTTY plugin — **PuTTY remains Telnet-only**
- [x] Retarget host + plugin + tests to **.NET Framework 4.8**
- [x] **VtNetCore** 1.0.30 — replace `AnsiTerminalScreen` + `RichTextBox` with `SshVtSession` + GDI+ `SshTerminalControl`
- [x] PTY sizing aligned with UI (Consolas cell metrics, debounced resize, `IPostConnectTerminalSync`)
- [x] Keyboard: arrows, function keys, Ctrl/Alt letters (`GetKeySequence`), paste (Ctrl+V / Shift+Insert)
- [x] Render performance (baseline): frame bitmap cache, 16 ms coalesce, monospace grid paint, color parse cache
- [x] Render performance (large screens): partial repaint band around cursor for local echo; full repaint on ANSI / scroll / resize
- [x] **Modern renderer (phase 3)** — cell grid, glyph atlas, row diff invalidation, Hi-DPI terminal (`Rendering/`, `TerminalRenderPipeline`); host `app.manifest` PerMonitorV2
- [x] Unit tests: `SshVtSessionTests`, `TerminalCellGridBuilderTests`, `TerminalGlyphAtlasTests`, `TerminalRowDifferTests`
- [x] **Protocol icons** on connection tabs (`TabIcon` in `TabControl`)
- [x] **Per-tab close** (×) — browser-style close glyph and hit-testing
- [x] Fork branding in app and MSI

### Done — `v1.0.1`

- [x] **xterm-256color** PTY negotiation (256-color and truecolor output)
- [x] Responsive UI during SSH connect; improved status feedback
- [x] **Hi-DPI** — PerMonitorV2 scaling for connection and options dialogs
- [x] Toolbar visibility recovery after fullscreen and DPI layout save
- [x] MSI publisher metadata; optional Authenticode signing ([CODE_SIGNING.md](Docs/CODE_SIGNING.md))

### Done — `v1.0.2`

- [x] SSH.NET upgrade to **2024.2.0** (with runtime dependencies)
- [x] **Connection profiles** — Server vs Network device (algorithm sets + PTY behavior; Extreme EXOS `rsa-sha2-256`)
- [x] Profile selector UI and network-device PTY behavior
- [x] Unit tests: profile and algorithm coverage

### Planned

- [ ] **Tab strip polish** — icon/close/title alignment and sizing on Hi-DPI and mixed-DPI multi-monitor setups
- [ ] **Many tabs** — overflow behavior when the tab bar runs out of horizontal space (scroll, compact, or equivalent)
- [ ] **Tab titles** — consistent truncation, updates on connect/disconnect, and readable names on narrow tabs
- [ ] **Shell layout pass** — verify and fix toolbar, status bar, tab strip, and saved window layout after fullscreen, restore, and move between monitors
- [ ] **Dialogs & panels** — spot-check connection editor, favorites, options, and about on scaled displays; fix clipping, overlap, and anchor issues found in use
- [ ] **Local resize UI** — split UI from PTY: immediate geometry/repaint on resize; debounced `TerminalResized` → `TrySendWindowChange` only (today coupled at 200 ms in `SshTerminalControl`)
- [ ] **Local echo (type-ahead)** — show typed characters immediately before server echo returns
- [ ] **SSH.NET 2025.x** — upgrade when convenient; replace reflection-based PTY resize with public `ChangeWindowSize` API; retest network-device profiles
- [ ] **Optional later:** DirectWrite or SkiaSharp atlas if GDI+ limits are hit; parser layer unchanged

**References:** [VtNetCore](https://github.com/darrenstarr/VtNetCore), [VtNetCore.UWP](https://github.com/darrenstarr/VtNetCore.UWP), [xterm ctlseqs](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html)

---

## Not in scope (this fork)

- PuTTY Pageant / X11 in SSH.NET plugin
- Replacing upstream RDP/VNC/Telnet stacks
- Further incremental `DrawText` / span-cache tuning (superseded by phase 3)
