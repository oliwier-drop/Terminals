# SSH terminal emulation (SshNet plugin)

## Reference documentation

Interactive SSH shells expect a **VT100/xterm** terminal. PuTTY and Windows OpenSSH (`ssh.exe`) implement that model; this plugin follows the same escape-sequence tables:

| Resource | URL |
|----------|-----|
| **xterm control sequences** (primary reference) | https://invisible-island.net/xterm/ctlseqs/ctlseqs.html |
| **VT100 user guide** | https://vt100.net/docs/vt100-ug/chapter3.html |
| **ANSI X3.64** (historical) | https://www2.ccs.neu.edu/research/gpc/VonaUtils/vona/terminal/vtansi.htm |

Implementation lives in `AnsiTerminalScreen.cs`: fixed **rows × columns** viewport, scrollback, scroll regions, and a subset of CSI/ESC sequences.

## PuTTY vs SSH.NET plugin

| Approach | Terminal fidelity | Notes |
|----------|-------------------|--------|
| **PuTTY plugin** (`Terminals.Plugins.Putty`) | Same as PuTTY | Embeds `putty.exe`; full xterm/ANSI support. |
| **SSH.NET plugin** (`Terminals.Plugins.SshNet`) | Subset of xterm | In-process; no external binary; modern algorithms via SSH.NET. |

If you need **identical** behavior to PuTTY or `cmd`/`ssh`, use the **PuTTY** connection type for now. The SSH.NET plugin is intended for integrated sessions but does not yet implement every private mode, mouse reporting, or true-color SGR.

## PTY size must match the UI

The server wraps lines and places the cursor using **PTY width/height** sent at connect and on resize. The UI measures columns from the control font (Consolas) and calls `CreateShellStream` / window-change with the same values. If the window is resized, wait for the debounced resize (~200 ms) so the server receives the new geometry.

## Reporting display issues

When something renders “one line too low” or overwrites incorrectly, capture:

1. A screenshot of Terminals and of `ssh`/`putty` for the same host.
2. `Build/Output/Debug/Data/logs/CurrentLog.txt` (PTY size log line: `SSH: connected ... (PTY CxR)`).
3. Whether the app is **nano**, **vim**, **bash**, or **motd** (alternate screen `?1049`).

That helps distinguish missing escape sequences from PTY size mismatch.
