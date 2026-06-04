// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System.Drawing;

namespace Terminals.Plugins.SshNet.Rendering
{
    internal struct TerminalCell
    {
        internal char CodePoint;
        internal Color Foreground;
        internal Color Background;
        internal bool Bold;
        internal bool Italic;
        internal bool Hidden;

        internal static TerminalCell Empty
        {
            get
            {
                return new TerminalCell
                {
                    CodePoint = ' ',
                    Foreground = VtNetColorHelper.DefaultForegroundColor,
                    Background = VtNetColorHelper.DefaultBackgroundColor,
                    Bold = false,
                    Italic = false,
                    Hidden = false
                };
            }
        }

        internal bool EqualsCell(TerminalCell other)
        {
            return this.CodePoint == other.CodePoint
                && this.Foreground.ToArgb() == other.Foreground.ToArgb()
                && this.Background.ToArgb() == other.Background.ToArgb()
                && this.Bold == other.Bold
                && this.Italic == other.Italic
                && this.Hidden == other.Hidden;
        }
    }
}
