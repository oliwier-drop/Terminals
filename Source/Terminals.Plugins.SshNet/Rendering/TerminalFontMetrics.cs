// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Terminals.Plugins.SshNet.Rendering
{
    internal sealed class TerminalFontMetrics : IDisposable
    {
        private readonly Font regularFont;
        private readonly Font boldFont;
        private readonly Font italicFont;
        private readonly Font boldItalicFont;

        internal TerminalFontMetrics(float fontPointSize, float dpiScale)
        {
            float size = fontPointSize * Math.Max(0.5f, dpiScale);
            this.regularFont = CreateFont(size, FontStyle.Regular);
            this.boldFont = CreateFont(size, FontStyle.Bold);
            this.italicFont = CreateFont(size, FontStyle.Italic);
            this.boldItalicFont = CreateFont(size, FontStyle.Bold | FontStyle.Italic);
            this.CellWidth = MeasureMonospaceCellWidth(this.regularFont);
            this.CellHeight = Math.Max(1, this.regularFont.Height);
        }

        internal int CellWidth { get; }

        internal int CellHeight { get; }

        internal Font GetFont(GlyphStyle style)
        {
            switch (style)
            {
                case GlyphStyle.Bold:
                    return this.boldFont;
                case GlyphStyle.Italic:
                    return this.italicFont;
                case GlyphStyle.BoldItalic:
                    return this.boldItalicFont;
                default:
                    return this.regularFont;
            }
        }

        internal static GlyphStyle FromCell(TerminalCell cell)
        {
            if (cell.Bold && cell.Italic)
                return GlyphStyle.BoldItalic;
            if (cell.Bold)
                return GlyphStyle.Bold;
            if (cell.Italic)
                return GlyphStyle.Italic;
            return GlyphStyle.Regular;
        }

        internal static int MeasureMonospaceCellWidth(Font font)
        {
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            int twoChars = TextRenderer.MeasureText("00", font, Size.Empty, flags).Width;
            int oneChar = TextRenderer.MeasureText("0", font, Size.Empty, flags).Width;
            int delta = twoChars - oneChar;
            if (delta > 0)
                return delta;

            int tenChars = TextRenderer.MeasureText("MMMMMMMMMM", font, Size.Empty, flags).Width;
            return Math.Max(1, tenChars / 10);
        }

        private static Font CreateFont(float size, FontStyle style)
        {
            try
            {
                return new Font("Consolas", size, style, GraphicsUnit.Point);
            }
            catch
            {
                return new Font(FontFamily.GenericMonospace, size, style, GraphicsUnit.Point);
            }
        }

        public void Dispose()
        {
            this.regularFont.Dispose();
            this.boldFont.Dispose();
            this.italicFont.Dispose();
            this.boldItalicFont.Dispose();
        }
    }
}
