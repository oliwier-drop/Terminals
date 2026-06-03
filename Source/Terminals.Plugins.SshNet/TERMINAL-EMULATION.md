# SSH terminal emulation (SshNet plugin)

## Reference documentation

Interactive SSH shells expect a **VT100/xterm** terminal. PuTTY and Windows OpenSSH (`ssh.exe`) implement that model; this plugin uses the same escape-sequence model via [VtNetCore](https://github.com/darrenstarr/VtNetCore):

| Resource | URL |
|----------|-----|
| **xterm control sequences** (primary reference) | https://invisible-island.net/xterm/ctlseqs/ctlseqs.html |
| **VT100 user guide** | https://vt100.net/docs/vt100-ug/chapter3.html |
| **VtNetCore** (emulation engine) | https://github.com/darrenstarr/VtNetCore |

## Architecture

| Layer | Component | Role |
|-------|-----------|------|
| Transport | SSH.NET `ShellStream` | SSH session, PTY, read/write |
| Parser / buffer | `SshVtSession` → VtNetCore `DataConsumer` + `VirtualTerminalController` | xterm sequence handling, scrollback |
| UI | `SshTerminalControl` (WinForms, GDI+) | Cell-grid paint via `GetPageSpans`, keyboard via `GetKeySequence` |

## PuTTY vs SSH.NET plugin

| Approach | Terminal fidelity | Notes |
|----------|-------------------|--------|
| **PuTTY plugin** (`Terminals.Plugins.Putty`) | Same as PuTTY | Embeds `putty.exe`; reference for edge cases. |
| **SSH.NET plugin** (`Terminals.Plugins.SshNet`) | VtNetCore xterm | In-process; SSH.NET for crypto/auth; GDI+ renderer (no `RichTextBox`). |

For **identical** behavior to a specific PuTTY build or Windows OpenSSH, compare side-by-side and file an issue with the repro steps below.

## PTY size must match the UI

The server wraps lines and places the cursor using **PTY width/height** sent at connect and on resize. The UI measures columns from Consolas cell metrics and calls `CreateShellStream` / window-change with the same values. VtNetCore `ResizeView` runs on the same debounced resize (~200 ms) as `TryResizePty`.

## Reporting display issues

When something renders incorrectly, capture:

1. A screenshot of Terminals and of `ssh`/`putty` for the same host.
2. `Build/Output/Debug/Data/logs/CurrentLog.txt` (PTY size log line: `SSH: connected ... (PTY CxR)`).
3. Whether the app is **nano**, **vim**, **bash**, or **motd** (alternate screen `?1049`).

That helps distinguish VtNetCore/parser gaps from PTY size mismatch.
