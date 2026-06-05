# Fork-authored code (GPL-3.0)

**Author / maintainer:** [Oliwier Drop](https://github.com/oliwier-drop) and contributors  
**License:** [GNU GPL v3.0](LICENSE.md#gnu-general-public-license-v30--fork-authored-code) — open source, copyleft  
**Upstream base:** [Terminals-Origin/Terminals](https://github.com/Terminals-Origin/Terminals) — [MS-CL](LICENSE.md)

Everything listed below is **fork-authored** (written for this SSH.NET / VtNetCore work), not from upstream Terminals. Other paths in the repo are upstream MS-CL unless you changed them locally without listing them here.

**How files are marked in the tree**

| Marker | Meaning |
|--------|---------|
| `SPDX-License-Identifier: GPL-3.0-or-later` in the first lines of a `.cs` file | GPL-3.0 fork code |
| This document | Full path list and scope |
| [NOTICE](NOTICE) | Short attribution for distributions |

---

## GPL-3.0 — entire directories (100% fork)

| Path | Description |
|------|-------------|
| `Source/Terminals.Plugins.SshNet/` | SSH.NET plugin (transport, VtNetCore UI, credentials, known hosts) |
| `Source/Tests/SshNet/` | Unit tests for the plugin |

Includes all `.cs`, `.csproj`, `packages.config`, and `Properties/` under those folders.

---

## GPL-3.0 — root & docs (fork)

| File | Description |
|------|-------------|
| `LICENSE.md` | MS-CL (upstream) and GPL-3.0 (fork-authored) |
| `FORK-AUTHORED.md` | This file |
| `NOTICE` | Attribution notice |
| `ROADMAP.md` | Fork feature roadmap |
| `README.md` | Fork overview, license, build/runtime requirements |

---

## GPL-3.0 — shared libraries (new files)

| File | Description |
|------|-------------|
| `Source/Terminals.Common/Configuration/ConnectionCredentialPromptResult.cs` | Credential prompt result type |
| `Source/Terminals.Common/Configuration/ICredentialPromptConsumer.cs` | Credential prompt consumer |
| `Source/Terminals.Common/Configuration/ICredentialPromptService.cs` | Credential prompt service |
| `Source/Terminals.Common/Connections/IDeferredConnection.cs` | Async connect contract |
| `Source/Terminals.Common/Connections/IPostConnectTerminalSync.cs` | Post-layout PTY sync |

---

## GPL-3.0 — host app integration (fork)

| File | Description |
|------|-------------|
| `Source/Terminals/Credentials/CredentialPromptService.cs` | Credential UI service |
| `Source/Terminals/Credentials/SshConnectCredentialForm.cs` | SSH connect credential form |
| `Source/Terminals/Credentials/SshConnectCredentialForm.Designer.cs` | Designer for above |

---

## MS-CL upstream — modified for SSH.NET integration

These files **derive from upstream** Terminals (MS-CL). Only your **changes** in git history are fork work; the original file remains MS-CL. When distributing, comply with both licenses (see [README](README.md#license-mixed)).

| Path | Fork change (summary) |
|------|------------------------|
| `Source/Terminals/Connections/PluginsLoader.cs` | Load `Terminals.Plugins.SshNet` |
| `Source/Terminals/Connections/ConnectionManager.cs` | Deferred connect, post-connect sync |
| `Source/Terminals/Forms/ConnectionsUiFactory.cs` | SSH connection wiring; assign protocol icon on new terminal tabs |
| `Source/Terminals/Data/Favorites/FavoriteIcons.cs` | `GetConnectionIcon` — same icon source as favorites tree, used on tabs |
| `Source/Terminals/Forms/TabControlRemover.cs` | Close confirmation uses the tab being closed, not the selected connection |
| `Source/TabControl/TabControl.cs` | Per-tab close button (browser-style); tab protocol icon rendering and layout |
| `Source/TabControl/TabControlItem.cs` | `TabIcon`, `CloseGlyphRect`; title hit-test excludes close glyph |
| `Source/TabControl/TabControlCloseButton.cs` | Draw close glyph at arbitrary tab rectangle (hover per tab) |
| `Source/Terminals/Forms/Controls/TerminalTabsSelectionControler.cs` | Tab / focus |
| `Source/Terminals/Forms/PopupTerminal.cs` | Popup terminal |
| `Source/Terminals/ForkBranding.cs` | Fork display name / version strings |
| `Source/Terminals/ProgramInfo.cs` | About/title version from informational version |
| `Source/Terminals/Forms/AboutForm.cs` | About dialog fork branding |
| `Source/Terminals/Terminals.csproj` | References, .NET 4.8 |
| `Source/Directory.Build.props` | Unified **.NET Framework 4.8**; WiX targets path for Build Tools |
| `Source/**/**.csproj` (solution) | Retarget v2.0 / v4.0 projects to **v4.8** |
| `Source/TerminalsSetup/` | MSI: SshNet plugin, fork product name/version |
| `Source/Terminals.Common/Connections/KnownConnectionConstants.cs` | SSH.NET connection id |
| `Source/Terminals.Common/Terminals.Common.csproj` | New compile items |
| `Source/Terminals.Plugins.Putty/` | Telnet-only after SSH decoupling; SSH types moved to SshNet |
| `Source/Tests/Tests.csproj` | SshNet tests, references |
| `Source/Tests/Connections/*.cs` | Plugin loader / manager tests updates |
| `.gitignore` | Build output, packages |

If you add new fork files, add them here and put the GPL header in the source file.

---

## Third-party libraries (not your copyright)

| Component | License |
|-----------|---------|
| [SSH.NET](https://www.nuget.org/packages/SSH.NET) | MIT / project license |
| [VtNetCore](https://www.nuget.org/packages/VtNetCore) | Check NuGet package |
| Upstream Terminals | MS-CL |
