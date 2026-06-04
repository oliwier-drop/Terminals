# Licenses for this repository

This fork combines two licenses:

| Code | License | Details |
|------|---------|---------|
| **Original Terminals** (upstream) | [Microsoft Shared Source Community License (MS-CL)](#microsoft-shared-source-community-license-ms-cl) | Most of the tree — core app, RDP/VNC/Telnet, legacy PuTTY SSH plugin, shared libraries, etc. |
| **Fork-authored code** (this fork) | [GNU General Public License v3.0 (GPL-3.0)](#gnu-general-public-license-v30--fork-authored-code) | SSH.NET plugin, related tests, and integration listed in [FORK-AUTHORED.md](FORK-AUTHORED.md) |

When you distribute combined works that include both GPL-3.0 fork code and MS-CL upstream code, you must comply with **both** applicable licenses and provide corresponding source for GPL-covered components. See [README.md](README.md#license-mixed) and [NOTICE](NOTICE).

---

## GNU General Public License v3.0 — fork-authored code

Copyright (c) oliwier-drop and contributors (fork-specific changes).

The following parts of **this repository** are licensed under the **GNU General Public License v3.0** (GPL-3.0).

**Authoritative path list:** [FORK-AUTHORED.md](FORK-AUTHORED.md)  
**Short notice for distributions:** [NOTICE](NOTICE)

Summary:

- `Source/Terminals.Plugins.SshNet/` (entire plugin)
- `Source/Tests/SshNet/` (tests for the plugin)
- New integration files listed in FORK-AUTHORED.md (Common interfaces, credential UI, fork docs)
- Source files marked with `SPDX-License-Identifier: GPL-3.0-or-later` in the header
- Fork-specific **changes** inside MS-CL upstream files: see FORK-AUTHORED.md (“modified for integration”); original files remain MS-CL

You may use, modify, and redistribute this code under the terms of GPL-3.0. The full license text is available at:

**https://www.gnu.org/licenses/gpl-3.0.html**

---

## Microsoft Shared Source Community License (MS-CL)

Published: October 18, 2005

This license governs use of the accompanying **upstream Terminals** software. If you use the software, you accept this license. If you do not accept the license, do not use the software.

### 1. Definitions

The terms "reproduce," "reproduction" and "distribution" have the same meaning here as under U.S. copyright law.  
"You" means the licensee of the software.  
"Larger work" means the combination of the software and any additions or modifications to the software.  
"Licensed patents" means any Licensor patent claims which read directly on the software as distributed by the Licensor under this license.

### 2. Grant of Rights

**(A) Copyright Grant** — Subject to the terms of this license, including the license conditions and limitations in section 3, the Licensor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce the software, prepare derivative works of the software and distribute the software or any derivative works that you create.

**(B) Patent Grant** — Subject to the terms of this license, including the license conditions and limitations in section 3, the Licensor grants you a non-exclusive, worldwide, royalty-free patent license under licensed patents to make, have made, use, practice, sell, and offer for sale, and/or otherwise dispose of the software or derivative works of the software.

### 3. Conditions and Limitations

**(A) Reciprocal Grants** — Your rights to reproduce and distribute the software (or any part of the software), or to create and distribute derivative works of the software, are conditioned on your licensing the software or any larger work you create under the following terms:

1. If you distribute the larger work as a series of files, you must grant all recipients the copyright and patent licenses in sections 2(A) & 2(B) for any file that contains code from the software. You must also provide recipients the source code to any such files that contain code from the software along with a copy of this license. Any other files which are entirely your own work and which do not contain any code from the software may be licensed under any terms you choose.

2. If you distribute the larger work as a single file, then you must grant all recipients the rights set out in sections 2(A) & 2(B) for the entire larger work. You must also provide recipients the source code to the larger work along with a copy of this license.

**(B) No Trademark License** — This license does not grant you any rights to use the Licensor’s name, logo, or trademarks.

**(C)** If you distribute the software in source code form you may do so only under this license (i.e., you must include a complete copy of this license with your distribution), and if you distribute the software solely in compiled or object code form you may only do so under a license that complies with this license.

**(D)** If you begin patent litigation against the Licensor over patents that you think may apply to the software (including a cross-claim or counterclaim in a lawsuit), your license to the software ends automatically.

**(E)** The software is licensed "as-is." You bear the risk of using it. The Licensor gives no express warranties, guarantees or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent permitted under your local laws, the Licensor excludes the implied warranties of merchantability, fitness for a particular purpose and non-infringement.
