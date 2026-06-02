# Roadmap: PuTTY-grade terminal via VtNetCore (phase 2)

This document tracks work **after** the interactive shell milestone (commits on `master`). Do not mix with the initial SSH.NET auth/integration commits.

## Goal

Keep **Renci.SshNet** for SSH transport (algorithms, keys, known hosts) but replace the in-house `AnsiTerminalScreen` + `RichTextBox` UI with a maintained **VT100/xterm** library and a cell-grid renderer.

## Prerequisites

| Item | Notes |
|------|--------|
| Target framework | Raise `Terminals.Plugins.SshNet` (and likely host app) from **.NET 4.0** to **.NET Framework 4.7.2+** / **4.8** |
| Library | Evaluate [VtNetCore](https://github.com/darrenstarr/VtNetCore) or [XTerm.NET](https://github.com/tomlm/XTerm.NET) |
| Reference | [xterm ctlseqs](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html) |

## Implementation steps

1. Create branch `feature/ssh-vtnetcore`.
2. Retarget `Terminals.Plugins.SshNet.csproj`; restore NuGet package.
3. Adapter: `ShellStream` read → parser `Push(bytes)`; keyboard → `ShellStream.Write`.
4. Replace `SshTerminalControl` with GDI+ (or library) cell renderer — no full-document `RichTextBox.Text` refresh.
5. PTY resize: keep `IPostConnectTerminalSync` / `TryResizePty` wired to `VirtualTerminalController.Resize`.
6. Manual test matrix: bash prompt, `ls --color`, `nano`, `vim`, window resize.
7. Update [TERMINAL-EMULATION.md](TERMINAL-EMULATION.md) when migration is complete.

## Until phase 2 ships

- **PuTTY SSH** connection type = PuTTY-identical terminal.
- **SSH.NET** = modern SSH in-process with **partial** xterm (see TERMINAL-EMULATION.md).
