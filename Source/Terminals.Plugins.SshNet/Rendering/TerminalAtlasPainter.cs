// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Terminals.Plugins.SshNet.Rendering
{
    internal sealed class TerminalAtlasPainter
    {
        private readonly TerminalGlyphAtlas atlas;
        private readonly int defaultBackgroundArgb = VtNetColorHelper.DefaultBackgroundColor.ToArgb();
        private readonly Dictionary<int, ImageAttributes> colorAttributesCache = new Dictionary<int, ImageAttributes>();

        internal TerminalAtlasPainter(TerminalGlyphAtlas atlas)
        {
            this.atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
        }

        internal int CellWidth
        {
            get { return this.atlas.CellWidth; }
        }

        internal int CellHeight
        {
            get { return this.atlas.CellHeight; }
        }

        internal void ConfigureGraphics(Graphics target)
        {
            if (target == null)
                return;

            target.InterpolationMode = InterpolationMode.NearestNeighbor;
            target.PixelOffsetMode = PixelOffsetMode.Half;
            target.CompositingMode = CompositingMode.SourceOver;
            target.CompositingQuality = CompositingQuality.HighSpeed;
        }

        internal void PaintCell(Graphics target, TerminalCell cell, int x, int y)
        {
            this.PaintCell(target, cell, x, y, forceBackgroundFill: false);
        }

        /// <summary>Crisp ClearType glyphs (selection and light-bg / dark-fg UI).</summary>
        internal void PaintSelectionCell(Graphics target, TerminalCell cell, int x, int y)
        {
            this.PaintDirectTextCell(target, cell, x, y);
        }

        private void PaintDirectTextCell(Graphics target, TerminalCell cell, int x, int y)
        {
            if (target == null)
                return;

            int cellW = this.CellWidth;
            int cellH = this.CellHeight;
            var cellRect = new Rectangle(x, y, cellW, cellH);
            using (var brush = new SolidBrush(cell.Background))
                target.FillRectangle(brush, cellRect);

            char codePoint = cell.CodePoint;
            if (codePoint == ' ' || cell.Hidden)
                return;

            TerminalFontMetrics metrics = this.atlas.Metrics;
            if (metrics == null)
                return;

            var previousHint = target.TextRenderingHint;
            target.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            const TextFormatFlags flags = TextFormatFlags.NoPadding
                | TextFormatFlags.NoPrefix
                | TextFormatFlags.SingleLine;
            Font font = metrics.GetFont(TerminalFontMetrics.FromCell(cell));
            TextRenderer.DrawText(
                target,
                codePoint.ToString(),
                font,
                cellRect,
                cell.Foreground,
                cell.Background,
                flags);
            target.TextRenderingHint = previousHint;
        }

        internal void PaintCell(Graphics target, TerminalCell cell, int x, int y, bool forceBackgroundFill)
        {
            if (target == null)
                return;

            Bitmap atlasBitmap = this.atlas.AtlasBitmap;
            if (atlasBitmap == null)
                return;

            this.ConfigureGraphics(target);
            this.DrawCell(target, atlasBitmap, cell, x, y, forceBackgroundFill);
        }

        internal void PaintRow(Graphics target, TerminalCellGrid grid, int gridRow, int destinationY)
        {
            if (target == null || grid == null)
                return;

            Bitmap atlasBitmap = this.atlas.AtlasBitmap;
            if (atlasBitmap == null)
                return;

            this.ConfigureGraphics(target);
            int columns = grid.Columns;
            int cellW = this.CellWidth;
            int cellH = this.CellHeight;
            target.FillRectangle(Brushes.Black, 0, destinationY, columns * cellW, cellH);
            for (int col = 0; col < columns; col++)
            {
                TerminalCell cell = grid[gridRow, col];
                if (cell.Hidden)
                    continue;

                int x = col * cellW;
                this.DrawCell(target, atlasBitmap, cell, x, destinationY, forceBackgroundFill: false);
            }
        }

        private void DrawCell(
            Graphics target,
            Bitmap atlasBitmap,
            TerminalCell cell,
            int x,
            int y,
            bool forceBackgroundFill)
        {
            int cellW = this.CellWidth;
            int cellH = this.CellHeight;
            var cellRect = new Rectangle(x, y, cellW, cellH);

            if (TerminalRenderPolicy.ShouldUseDirectTextRender(cell))
            {
                this.PaintDirectTextCell(target, cell, x, y);
                return;
            }

            if (forceBackgroundFill || cell.Background.ToArgb() != this.defaultBackgroundArgb)
            {
                using (var brush = new SolidBrush(cell.Background))
                    target.FillRectangle(brush, cellRect);
            }

            GlyphStyle style = TerminalFontMetrics.FromCell(cell);
            if (!this.atlas.TryGetGlyphRect(cell.CodePoint, style, out Rectangle sourceRect))
                return;

            Color drawForeground = NormalizeGlyphForeground(cell.Foreground);
            ImageAttributes attributes = this.GetOrCreateColorAttributes(drawForeground);
            target.DrawImage(
                atlasBitmap,
                cellRect,
                sourceRect.X,
                sourceRect.Y,
                sourceRect.Width,
                sourceRect.Height,
                GraphicsUnit.Pixel,
                attributes);
        }

        private ImageAttributes GetOrCreateColorAttributes(Color foreColor)
        {
            int key = foreColor.ToArgb();
            ImageAttributes cached;
            if (this.colorAttributesCache.TryGetValue(key, out cached))
                return cached;

            cached = CreateTintAttributes(foreColor);
            this.colorAttributesCache[key] = cached;
            return cached;
        }

        private static Color NormalizeGlyphForeground(Color foreColor)
        {
            if (foreColor.R + foreColor.G + foreColor.B <= 8)
                return Color.FromArgb(32, 32, 32);

            return foreColor;
        }

        private static ImageAttributes CreateTintAttributes(Color foreColor)
        {
            float r = foreColor.R / 255f;
            float g = foreColor.G / 255f;
            float b = foreColor.B / 255f;
            var attributes = new ImageAttributes();
            var matrix = new ColorMatrix(new[]
            {
                new[] { r, 0f, 0f, 0f, 0f },
                new[] { 0f, g, 0f, 0f, 0f },
                new[] { 0f, 0f, b, 0f, 0f },
                new[] { 0f, 0f, 0f, 1f, 0f },
                new[] { 0f, 0f, 0f, 0f, 1f }
            });
            attributes.SetColorMatrix(matrix);
            attributes.SetColorKey(Color.Black, Color.FromArgb(48, 48, 48));
            return attributes;
        }
    }
}
