// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Terminals.Plugins.SshNet.Rendering
{
    internal sealed class TerminalGlyphAtlas : IDisposable
    {
        private const int PreRasterFirst = 0x20;
        private const int PreRasterLast = 0x7E;
        private const int StylesPerChar = 4;
        private const int MaxDynamicGlyphs = 512;

        private readonly TerminalFontMetrics metrics;
        private readonly Dictionary<GlyphKey, Rectangle> glyphRects = new Dictionary<GlyphKey, Rectangle>();
        private readonly Queue<Rectangle> freeDynamicSlots = new Queue<Rectangle>();
        private Bitmap atlasBitmap;
        private int dynamicGlyphsUsed;

        internal TerminalGlyphAtlas(TerminalFontMetrics metrics)
        {
            this.metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            this.Rebuild();
        }

        internal int CellWidth
        {
            get { return this.metrics.CellWidth; }
        }

        internal int CellHeight
        {
            get { return this.metrics.CellHeight; }
        }

        internal Bitmap AtlasBitmap
        {
            get { return this.atlasBitmap; }
        }

        internal TerminalFontMetrics Metrics
        {
            get { return this.metrics; }
        }

        internal bool TryGetGlyphRect(char codePoint, GlyphStyle style, out Rectangle sourceRect)
        {
            var key = new GlyphKey(codePoint, style);
            if (this.glyphRects.TryGetValue(key, out sourceRect))
                return true;

            if (!this.TryAllocateDynamicGlyph(codePoint, style, out sourceRect))
            {
                sourceRect = this.glyphRects[new GlyphKey('?', GlyphStyle.Regular)];
                return sourceRect.Width > 0;
            }

            this.glyphRects[key] = sourceRect;
            return true;
        }

        internal void Rebuild()
        {
            this.glyphRects.Clear();
            this.freeDynamicSlots.Clear();
            this.dynamicGlyphsUsed = 0;

            if (this.atlasBitmap != null)
            {
                this.atlasBitmap.Dispose();
                this.atlasBitmap = null;
            }

            int cellW = this.metrics.CellWidth;
            int cellH = this.metrics.CellHeight;
            int preCount = (PreRasterLast - PreRasterFirst + 1) * StylesPerChar;
            int cols = 32;
            int preRows = (preCount + cols - 1) / cols;
            int dynamicRows = (MaxDynamicGlyphs + cols - 1) / cols;
            int rows = preRows + dynamicRows;
            int width = cols * cellW;
            int height = rows * cellH;
            if (width < 1 || height < 1)
                return;

            this.atlasBitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(this.atlasBitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                int index = 0;
                for (int code = PreRasterFirst; code <= PreRasterLast; code++)
                {
                    for (int styleIndex = 0; styleIndex < StylesPerChar; styleIndex++)
                    {
                        var style = (GlyphStyle)styleIndex;
                        var rect = this.CellRect(index++, cols, cellW, cellH);
                        this.RasterGlyph(graphics, (char)code, style, rect);
                        this.glyphRects[new GlyphKey((char)code, style)] = rect;
                    }
                }

                for (int dynamicIndex = 0; dynamicIndex < MaxDynamicGlyphs; dynamicIndex++)
                {
                    int atlasIndex = preCount + dynamicIndex;
                    this.freeDynamicSlots.Enqueue(this.CellRect(atlasIndex, cols, cellW, cellH));
                }
            }

        }

        private bool TryAllocateDynamicGlyph(char codePoint, GlyphStyle style, out Rectangle sourceRect)
        {
            sourceRect = Rectangle.Empty;
            if (this.atlasBitmap == null || this.freeDynamicSlots.Count == 0)
                return false;

            sourceRect = this.freeDynamicSlots.Dequeue();
            this.dynamicGlyphsUsed++;
            using (Graphics graphics = Graphics.FromImage(this.atlasBitmap))
            {
                graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                graphics.FillRectangle(Brushes.Transparent, sourceRect);
                this.RasterGlyph(graphics, codePoint, style, sourceRect);
            }

            return true;
        }

        private void RasterGlyph(Graphics graphics, char codePoint, GlyphStyle style, Rectangle cellRect)
        {
            Font font = this.metrics.GetFont(style);
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            graphics.FillRectangle(Brushes.Transparent, cellRect);
            TextRenderer.DrawText(
                graphics,
                codePoint.ToString(),
                font,
                cellRect,
                Color.White,
                Color.Transparent,
                flags);
        }

        private Rectangle CellRect(int index, int cols, int cellW, int cellH)
        {
            int col = index % cols;
            int row = index / cols;
            return new Rectangle(col * cellW, row * cellH, cellW, cellH);
        }

        public void Dispose()
        {
            if (this.atlasBitmap != null)
            {
                this.atlasBitmap.Dispose();
                this.atlasBitmap = null;
            }
        }

        private struct GlyphKey : IEquatable<GlyphKey>
        {
            private readonly char codePoint;
            private readonly GlyphStyle style;

            internal GlyphKey(char codePoint, GlyphStyle style)
            {
                this.codePoint = codePoint;
                this.style = style;
            }

            public bool Equals(GlyphKey other)
            {
                return this.codePoint == other.codePoint && this.style == other.style;
            }

            public override bool Equals(object obj)
            {
                return obj is GlyphKey other && this.Equals(other);
            }

            public override int GetHashCode()
            {
                return ((int)this.codePoint * 397) ^ (int)this.style;
            }
        }
    }
}
