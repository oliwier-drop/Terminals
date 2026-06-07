// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System.Drawing;

namespace Terminals.Plugins.SshNet.Rendering
{
    internal static class TerminalRenderPolicy
    {
        /// <summary>Light background + darker text (nano bars, reverse video) needs ClearType path.</summary>
        internal static bool ShouldUseDirectTextRender(TerminalCell cell)
        {
            int bgLuma = cell.Background.R + cell.Background.G + cell.Background.B;
            int fgLuma = cell.Foreground.R + cell.Foreground.G + cell.Foreground.B;
            return bgLuma >= 384 && fgLuma + 80 < bgLuma;
        }
    }
}
