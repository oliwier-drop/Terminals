# Fork roadmap

Single place for **planned and completed work** on this Terminals fork (SSH.NET plugin and related changes). Upstream documentation remains under [Docs/](Docs/).

**Status:** `[x]` done · `[ ]` planned · `[~]` in progress · `[—]` superseded

**Licensing:** fork-authored code is **GPL-3.0** — see [FORK-AUTHORED.md](FORK-AUTHORED.md) and `SPDX-License-Identifier` headers in source files.

---

## SSH.NET plugin (`Terminals.Plugins.SshNet`)

### Done

- [x] SSH.NET **2024.2.0** transport (auth, known hosts, deferred connect, `ShellStream`)
- [x] **Connection profiles** — Server vs Network device (algorithm sets + PTY behavior; Extreme EXOS `rsa-sha2-256`)
- [x] Retarget host + plugin + tests to **.NET Framework 4.8**
- [x] **VtNetCore** 1.0.30 — replace `AnsiTerminalScreen` + `RichTextBox` with `SshVtSession` + GDI+ `SshTerminalControl`
- [x] PTY sizing aligned with UI (Consolas cell metrics, debounced resize, `IPostConnectTerminalSync`)
- [x] Keyboard: arrows, function keys, Ctrl/Alt letters (`GetKeySequence`), paste (Ctrl+V / Shift+Insert)
- [x] Render performance (baseline): frame bitmap cache, 16 ms coalesce, monospace grid paint, color parse cache
- [x] Render performance (large screens): partial repaint band around cursor for local echo; full repaint on ANSI / scroll / resize
- [x] Unit tests: `SshVtSessionTests` (replaces `AnsiTerminalScreenTests`)
- [x] **Phase 3 — modern renderer** — cell grid, glyph atlas, row diff invalidation, Hi-DPI (`Rendering/`, `TerminalRenderPipeline`)
- [x] Unit tests: `TerminalCellGridBuilderTests`, `TerminalGlyphAtlasTests`, `TerminalRowDifferTests`

### Superseded — incremental GDI+ optimizations

The items below were planned as intermediate wins on the current `TextRenderer.DrawText` path. **They are deferred** in favor of the modern renderer (phase 3): they add complexity that would be discarded on migration and do not fix **O(columns)** cost per keystroke on wide / 4K displays.

- [—] **Dirty rows (1–2 lines)** — on typing, repaint only previous + current cursor row
- [—] **Per-row bitmap cache** — invalidate and redraw changed rows only; scroll shifts cache
- [—] **Background frame build** — build `frameCache` off UI thread; swap on UI thread

### Done — modern terminal renderer (phase 3)

**Goal:** responsive SSH terminal on **every screen** — laptop panels, ultrawide, and 4K fullscreen — with stable typing latency regardless of column count.

**Implementation:** `Source/Terminals.Plugins.SshNet/Rendering/` — `TerminalCellGridBuilder`, `TerminalGlyphAtlas`, `TerminalAtlasPainter`, `TerminalRowDiffer`, `TerminalRowBitmapCache`, orchestrated by `TerminalRenderPipeline` and `SshTerminalControl`.

- [x] **Cell grid** — `VirtualTerminalController` → dense `TerminalCellGrid` via `GetPageSpans`
- [x] **Glyph atlas** — pre-raster ASCII + lazy cache; Consolas regular/bold/italic/bold-italic
- [x] **Invalidation** — row-hash diff; repaint dirty rows only; full repaint on scroll / ESC / resize
- [x] **Hi-DPI** — `OnDpiChanged`, atlas rebuild per `DeviceDpi`; host `app.manifest` PerMonitorV2
- **References:** [VtNetCore.UWP](https://github.com/darrenstarr/VtNetCore.UWP) (paint pattern), [xterm ctlseqs](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html), Windows Terminal glyph pipeline

### Planned — post v1.0.2 (resize & rendering)

**Goal:** window resize should feel responsive (closer to standalone PuTTY) while keeping stable PTY signaling and typing latency on large displays.

**Context:** today `SshTerminalControl` debounces resize at **200 ms** and applies UI geometry, full frame repaint, and `TerminalResized` → PTY in one step (`OnResizeDebounceTick`). Perceived lag vs. desktop PuTTY is mostly this coupling, not SSH transport.

**Priority:**

1. [ ] **Local resize UI** — split UI from PTY:
   - **Immediate (on `Resize` / `Layout`):** recalc cell metrics, `SyncSessionGeometry`, `RebuildFrameCache`, `Invalidate`
   - **Debounced (e.g. 100–150 ms, separate timer):** `TerminalResized` → `TrySendWindowChange` only
2. [ ] **SSH.NET 2025.x** — upgrade when convenient; replace reflection-based PTY resize (`TrySendWindowChange` on `_channel`) with public `ChangeWindowSize` API; retest network-device algorithm profiles
3. [ ] **DirectWrite or SkiaSharp atlas** (optional, if profiling shows GDI+ still limits after local resize) — faster full-frame repaint on resize / scroll / 4K; `VirtualTerminalController` + cell grid unchanged

### Planned — UX (optional, after or parallel to phase 3)

- [ ] **Local echo (type-ahead)** — show typed characters immediately before server echo returns (easier once cell-level invalidation exists)

### Architecture

| Layer | Component |
|-------|-----------|
| Transport | SSH.NET `ShellStream`, PTY resize |
| Parser / buffer | `SshVtSession` → VtNetCore `DataConsumer` + `VirtualTerminalController` |
| UI | `SshTerminalControl` → `TerminalRenderPipeline` (cell grid + glyph atlas + row cache) |

**PuTTY plugin** remains the option for PuTTY-identical behavior (embedded `putty.exe`). **SSH.NET plugin** is in-process VtNetCore + modern cell renderer.

**References:** [VtNetCore](https://github.com/darrenstarr/VtNetCore)

---

## Not in scope (this fork)

- PuTTY Pageant / X11 in SSH.NET plugin
- Replacing upstream RDP/VNC/Telnet stacks
- Further incremental `DrawText` / span-cache tuning (superseded by phase 3)

---

## Releases (wdrożone)

Szczegóły instalatorów: [Docs/RELEASE-v1.0.0-notes.md](Docs/RELEASE-v1.0.0-notes.md), [v1.0.1](Docs/RELEASE-v1.0.1-notes.md), [v1.0.2](Docs/RELEASE-v1.0.2-notes.md).

### v1.0.0 — pierwsza publiczna wersja forka

Baza: **Terminals 4.0.1** (MS-CL). Wymagania: Windows 10/11, **.NET Framework 4.8**. Artefakty: `TerminalsSetup_1.0.0.msi`, `Terminals_v1.0.0.zip`.

**SSH.NET plugin (nowy, GPL-3.0):**

- Transport **SSH.NET 2020.0.2** — hasło, klucz prywatny, keyboard-interactive; weryfikacja known hosts (bez auto-trust)
- Odłączenie SSH od pluginu PuTTY — **PuTTY zostaje tylko dla Telnet**
- **VtNetCore** 1.0.30 zamiast `AnsiTerminalScreen` + `RichTextBox` (`SshVtSession`, `SshTerminalControl`)
- Klawiatura: strzałki, F-keys, Ctrl/Alt, wklejanie; skróty dla `nano` / `vim`
- PTY dopasowany do UI (metryki Consolas, debounced resize 200 ms, `IPostConnectTerminalSync`)
- Wydajność renderowania: cache klatek, coalesce 16 ms; optymalizacje na dużych ekranach
- **Faza 3 — renderer:** siatka komórek, glyph atlas, row-diff invalidation, Hi-DPI (PerMonitorV2), zaznaczanie tekstu w strumieniu
- Testy jednostkowe pluginu SSH (sesja, known hosts, renderer, selekcja)

**UI / packaging forka:**

- Ikony protokołu na zakładkach, zamykanie zakładki (×)
- Branding forka w aplikacji i MSI

**Bez zmian (upstream):** RDP, VNC, VMRC, ICA, Web i pozostałe protokoły.

### v1.0.1 — stabilność Hi-DPI i jakość sesji SSH

Patch po v1.0.0. Artefakty: `TerminalsSetup_1.0.1.msi`, `Terminals_v1.0.1.zip`.

**UI / layout:**

- Naprawa widoczności toolbara po fullscreen i zapisie layoutu PerMonitorV2
- Skalowanie okien Settings, New Connection i edycji połączeń na monitorach Hi-DPI

**SSH:**

- PTY **`xterm-256color`** — kolory SGR (`ls --color`, `ip -c a`)
- Handshake SSH poza wątkiem UI; aplikacja zamykalna podczas connect
- Lepszy feedback connect (timeout 30 s, status, czyszczenie ekranu przed MOTD)

**Instalator:** metadane wydawcy **Oliwier Drop**; opcjonalne podpisywanie Authenticode ([CODE_SIGNING.md](Docs/CODE_SIGNING.md)).

### v1.0.2 — profile połączeń i nowoczesne algorytmy SSH *(wdrożone)*

Patch po v1.0.1. Artefakty: `TerminalsSetup_1.0.2.msi`, `Terminals_v1.0.2.zip`.

**SSH:**

- Upgrade **SSH.NET 2024.2.0** + zależności runtime (`System.Memory`, `System.Buffers`, …) — `rsa-sha2-256` / `rsa-sha2-512`, ETM MACs
- **Profile połączenia** (panel SSH Options):
  - **Server** — domyślny; `xterm-256color`, pełny zestaw algorytmów, opcjonalna kompresja
  - **Network device** — przełączniki/routery (np. Extreme EXOS); ograniczone KEX/cipher/MAC, PTY `vt100`, bez czekania na MOTD
- Log connect: negocjowane host-key i KEX
- Testy profili i algorytmów; dokumentacja release

**Planowane po v1.0.2:** local resize UI, SSH.NET 2025.x (publiczne PTY resize), opcjonalnie DirectWrite/Skia — sekcja wyżej.

