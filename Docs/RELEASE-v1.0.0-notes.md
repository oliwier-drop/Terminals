# Terminals (SSH.NET fork) — v1.0.0

**Not affiliated with [Terminals-Origin/Terminals](https://github.com/Terminals-Origin/Terminals).**

## Highlights

- **SSH** via **SSH.NET** plugin (`Plugins\SshNet`) — modern algorithms, password/key/keyboard-interactive auth, known-host verification
- **Telnet** via PuTTY plugin (legacy `putty.exe` bundled)
- RDP, VNC, VMRC, ICA, Web plugins (upstream)
- Tab UI: protocol icons on tabs, per-tab close (×)
- **Requires:** Windows 10/11, **.NET Framework 4.8**

## Upstream base

Fork based on **Terminals 4.0.1** (MS-CL). Fork version **1.0.0**.

## Licenses

- Upstream code: **MS-CL** — see [LICENSE.md](../LICENSE.md)
- Fork-authored code (SSH.NET plugin, etc.): **GPL-3.0** — see [FORK-AUTHORED.md](../FORK-AUTHORED.md)

## Known limitations (SSH.NET)

- No PuTTY Pageant integration
- No X11 forwarding in SSH.NET plugin
- For PuTTY-identical SSH behavior, use upstream Terminals with legacy PuTTY SSH (not this fork’s default SSH path)

## Install

1. Install [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48) if missing.
2. Run `TerminalsSetup_1.0.0.msi` (or portable ZIP).
3. On first SSH connect, confirm the server host key when prompted.

## Files

| Asset | Description |
|-------|-------------|
| `TerminalsSetup_1.0.0.msi` | Per-machine installer (WiX) |
| `Terminals_v1.0.0.zip` | Portable layout (same binaries as MSI payload) |
