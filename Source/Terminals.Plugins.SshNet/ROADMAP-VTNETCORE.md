# VtNetCore migration (phase 2) — completed

Phase 2 replaced the in-house `AnsiTerminalScreen` + `RichTextBox` stack with **VtNetCore** and a GDI+ cell renderer.

## What shipped

- .NET Framework **4.8** for `Terminals`, `Terminals.Plugins.SshNet`, and `Tests`
- NuGet **VtNetCore** 1.0.30
- `SshVtSession` adapter (`DataConsumer.Push`, `VirtualTerminalController.ResizeView`)
- `SshTerminalControl` — WinForms `UserControl` with `GetPageSpans` painting
- PTY resize unchanged (`TryResizePty`, debounce, `IPostConnectTerminalSync`)
- Expanded keyboard mapping (arrows, Home/End, PgUp/PgDn, F1–F12, Ctrl+letter via VtNetCore)

## Manual verification checklist

| Scenario | Expected |
|----------|----------|
| bash prompt | Correct cursor and line wrap |
| `ls --color` | SGR colors visible |
| `nano` / `vim` | Alternate screen (`?1049`) usable |
| Window resize | PTY log matches UI after ~200 ms debounce |

## Reference

See [TERMINAL-EMULATION.md](TERMINAL-EMULATION.md) for the current architecture.
