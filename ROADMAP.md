# Fork roadmap

Single place for **planned and completed work** on this Terminals fork. Upstream documentation remains under [Docs/](Docs/).

**Status:** `[x]` done · `[ ]` planned · `[~]` in progress · `[—]` superseded

**Licensing:** fork-authored code is **GPL-3.0** — see [FORK-AUTHORED.md](FORK-AUTHORED.md) and `SPDX-License-Identifier` headers in source files.

Release notes: [v1.0.0](Docs/RELEASE-v1.0.0-notes.md) · [v1.0.1](Docs/RELEASE-v1.0.1-notes.md) · [v1.0.2](Docs/RELEASE-v1.0.2-notes.md) · [v1.0.3](Docs/RELEASE-v1.0.3-notes.md) · [v1.0.4](Docs/RELEASE-v1.0.4-notes.md) · ~~[v1.0.5](Docs/RELEASE-v1.0.5-notes.md)~~ · ~~[v1.0.6](Docs/RELEASE-v1.0.6-notes.md)~~ · **[v1.0.7](Docs/RELEASE-v1.0.7-notes.md)** · [Withdrawn releases](Docs/WITHDRAWN-RELEASES.md)

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

### Done — `v1.0.4`

- [x] **Scrollback viewport** — follow-tail autoscroll, fixed scrollbar range, smoother wheel/thumb scrolling
- [x] **Large selection & copy** — absolute scrollback coordinates; copy across full scrollback; auto-scroll while dragging selection
- [x] **Password masking** — hide typed characters at password prompts (local + server echo suppression; Polish prompts)
- [x] **Fork update links** — release checker and UI links point to `oliwier-drop/Terminals`
- [x] Unit tests: scroll clamp/follow-tail, document-range copy, password echo suppression

### Done — `v1.0.5`

- [x] **SkiaSharp SSH renderer** — replace GDI+ per-cell atlas with row/span painting (`SkiaTerminalPainter`, `TerminalRowSpanBuilder`); VtNetCore parser unchanged
- [x] **Row bitmap cache** wired into `TerminalRenderPipeline` for dirty-row blit
- [x] **Grid reuse** — `CopyFrom` instead of per-frame `Clone`
- [x] **Adaptive render scheduler** — immediate paint for small chunks, coalesced timer for large output, catch-up full repaint on PTY backlog
- [x] **Fast output fixes** — no row deferral; full frame rebuild on scroll/viewport change and catch-up; alternate-buffer clear on vim/nano re-entry
- [x] **Display scaling & zoom** — DPI/viewport-adaptive font; Ctrl+wheel / Ctrl+± / Ctrl+0; Skia-based cell metrics
- [x] **I/O optimizations** — 16 KB SSH read buffer; 16 KB UI flush chunks; selection overlay uses cached grid
- [x] Unit tests: `TerminalRowSpanBuilderTests`, `TerminalDisplayScaleTests`, `TerminalFontMetricsTests`; updated `TerminalRenderPipelineTests`, `SshVtSessionTests`

### Done — `v1.0.6`

- [—] **SkiaSharp packaging fix** — superseded by v1.0.7 (wrong native path; release withdrawn)

### Done — `v1.0.7`

- [x] **SkiaSharp native path fix** — `x64\` / `x86\` / `arm64\` layout via official NativeAssets targets; WiX + ZIP corrected
- [x] **SSH render performance** — tail-follow scroll blit, row budget + deferred paint, batch DrawText, gentler catch-up, direct frame paint, VT parse worker off UI thread
- [x] **Withdrawn v1.0.5/v1.0.6** — documented in [WITHDRAWN-RELEASES.md](Docs/WITHDRAWN-RELEASES.md); updater skips GitHub pre-releases

### Planned

- [ ] **Tab strip polish** — icon/close/title alignment and sizing on Hi-DPI and mixed-DPI multi-monitor setups
- [ ] **Many tabs** — overflow behavior when the tab bar runs out of horizontal space (scroll, compact, or equivalent)
- [ ] **Tab titles** — consistent truncation, updates on connect/disconnect, and readable names on narrow tabs
- [ ] **Shell layout pass** — verify and fix toolbar, status bar, tab strip, and saved window layout after fullscreen, restore, and move between monitors
- [ ] **Dialogs & panels** — spot-check connection editor, favorites, options, and about on scaled displays; fix clipping, overlap, and anchor issues found in use

**References:** [VtNetCore](https://github.com/darrenstarr/VtNetCore), [VtNetCore.UWP](https://github.com/darrenstarr/VtNetCore.UWP), [xterm ctlseqs](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html)

