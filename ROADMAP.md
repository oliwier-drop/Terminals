# Fork roadmap

Single place for **planned and completed work** on this Terminals fork. Upstream documentation remains under [Docs/](Docs/).

**Status:** `[x]` done · `[ ]` planned · `[~]` in progress · `[—]` superseded

**Licensing:** fork-authored code is **GPL-3.0** — see [FORK-AUTHORED.md](FORK-AUTHORED.md) and `SPDX-License-Identifier` headers in source files.

Release notes: [v1.0.0](Docs/RELEASE-v1.0.0-notes.md) · [v1.0.1](Docs/RELEASE-v1.0.1-notes.md) · [v1.0.2](Docs/RELEASE-v1.0.2-notes.md) · [v1.0.3](Docs/RELEASE-v1.0.3-notes.md)

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

### Done — `v1.0.3`

- [x] **Local echo (type-ahead)** — guarded optimistic echo for printable SSH input with server-echo suppression
- [x] **SSH.NET 2025.1.0** — upgrade with public `ShellStream.ChangeWindowSize` for PTY resize
- [x] **Local resize UI** — split immediate repaint from debounced PTY sync; alternate-screen-safe resize for nano/vim; pixel PTY dimensions
- [x] **Tab strip shape** — first tab slanted left edge only; subsequent tabs straight vertical edges with right-side close button
- [x] Unit tests: local echo, SSH.NET helper, VT session, resize, and SSH profile coverage

### Planned

- [ ] **Tab strip polish** — icon/close/title alignment and sizing on Hi-DPI and mixed-DPI multi-monitor setups
- [ ] **Many tabs** — overflow behavior when the tab bar runs out of horizontal space (scroll, compact, or equivalent)
- [ ] **Tab titles** — consistent truncation, updates on connect/disconnect, and readable names on narrow tabs
- [ ] **Shell layout pass** — verify and fix toolbar, status bar, tab strip, and saved window layout after fullscreen, restore, and move between monitors
- [ ] **Dialogs & panels** — spot-check connection editor, favorites, options, and about on scaled displays; fix clipping, overlap, and anchor issues found in use
- [ ] **Optional later:** DirectWrite or SkiaSharp atlas if GDI+ limits are hit; parser layer unchanged

**References:** [VtNetCore](https://github.com/darrenstarr/VtNetCore), [VtNetCore.UWP](https://github.com/darrenstarr/VtNetCore.UWP), [xterm ctlseqs](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html)

