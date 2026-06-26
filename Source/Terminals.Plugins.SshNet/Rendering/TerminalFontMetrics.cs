// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Drawing;
using System.Windows.Forms;
using SkiaSharp;

namespace Terminals.Plugins.SshNet.Rendering
{
    internal sealed class TerminalFontMetrics : IDisposable
    {
        private readonly Font regularFont;
        private readonly Font boldFont;
        private readonly Font italicFont;
        private readonly Font boldItalicFont;

        internal TerminalFontMetrics(float fontPointSize)
        {
            if (fontPointSize < 1f)
                fontPointSize = 1f;

            this.FontSize = fontPointSize;
            this.regularFont = CreateFont(this.FontSize, FontStyle.Regular);
            this.boldFont = CreateFont(this.FontSize, FontStyle.Bold);
            this.italicFont = CreateFont(this.FontSize, FontStyle.Italic);
            this.boldItalicFont = CreateFont(this.FontSize, FontStyle.Bold | FontStyle.Italic);

            int cellWidth;
            int cellHeight;
            MeasureCellSizeFromSkia(this.FontSize, out cellWidth, out cellHeight);
            this.CellWidth = cellWidth;
            this.CellHeight = cellHeight;
        }

        internal float FontSize { get; }

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

        internal static float ComputeTextBaseline(float fontSize, int cellHeight)
        {
            using (var typeface = CreateSkiaTypeface(SKFontStyle.Normal))
            using (var font = new SKFont(typeface, fontSize))
            {
                SKFontMetrics fontMetrics;
                font.GetFontMetrics(out fontMetrics);
                return -fontMetrics.Ascent;
            }
        }

        private static void MeasureCellSizeFromSkia(float fontSize, out int cellWidth, out int cellHeight)
        {
            using (var typeface = CreateSkiaTypeface(SKFontStyle.Normal))
            using (var font = new SKFont(typeface, fontSize))
            {
                SKFontMetrics fontMetrics;
                font.GetFontMetrics(out fontMetrics);
                cellHeight = Math.Max(1, (int)Math.Ceiling(fontMetrics.Descent - fontMetrics.Ascent));

                float width = MeasureMonospaceAdvance(font, "00");
                if (width <= 0f)
                    width = MeasureMonospaceAdvance(font, "MM");
                if (width <= 0f)
                    width = MeasureMonospaceAdvance(font, "@@");

                cellWidth = Math.Max(1, (int)Math.Ceiling(width));
            }
        }

        private static float MeasureMonospaceAdvance(SKFont font, string sample)
        {
            if (string.IsNullOrEmpty(sample) || sample.Length < 2)
                return 0f;

            var glyphs = new ushort[sample.Length];
            font.GetGlyphs(sample, glyphs);
            var widths = new float[sample.Length];
            using (var paint = new SKPaint())
                font.GetGlyphWidths(glyphs, widths, null, paint);

            float total = 0f;
            for (int i = 0; i < widths.Length; i++)
                total += widths[i];
            return total / sample.Length;
        }

        private static SKTypeface CreateSkiaTypeface(SKFontStyle style)
        {
            SKTypeface typeface = SKTypeface.FromFamilyName("Consolas", style);
            if (typeface == null || typeface.FamilyName == null)
                typeface = SKTypeface.FromFamilyName("Courier New", style);
            if (typeface == null)
                typeface = SKTypeface.FromFamilyName(SKTypeface.Default.FamilyName, style);
            return typeface;
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
