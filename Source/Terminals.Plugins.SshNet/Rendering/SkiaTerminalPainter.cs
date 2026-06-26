// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;

namespace Terminals.Plugins.SshNet.Rendering
{
    internal sealed class SkiaTerminalPainter : ITerminalPainter, IDisposable
    {
        private readonly TerminalFontMetrics metrics;
        private readonly SKTypeface regularTypeface;
        private readonly SKTypeface boldTypeface;
        private readonly SKTypeface italicTypeface;
        private readonly SKTypeface boldItalicTypeface;
        private readonly Dictionary<int, SKPaint> fillPaintCache = new Dictionary<int, SKPaint>();
        private readonly Dictionary<long, SKPaint> textPaintCache = new Dictionary<long, SKPaint>();
        private float textBaseline;

        internal SkiaTerminalPainter(TerminalFontMetrics metrics)
        {
            this.metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            this.regularTypeface = CreateTypeface(SKFontStyle.Normal);
            this.boldTypeface = CreateTypeface(SKFontStyle.Bold);
            this.italicTypeface = CreateTypeface(SKFontStyle.Italic);
            this.boldItalicTypeface = CreateTypeface(SKFontStyle.BoldItalic);
            this.textBaseline = TerminalFontMetrics.ComputeTextBaseline(this.metrics.FontSize, this.CellHeight);
        }

        public int CellWidth
        {
            get { return this.metrics.CellWidth; }
        }

        public int CellHeight
        {
            get { return this.metrics.CellHeight; }
        }

        public void ConfigureCanvas(SKCanvas canvas)
        {
            if (canvas == null)
                return;

            canvas.Clear(SKColors.Black);
        }

        public void PaintRow(SKCanvas canvas, TerminalCellGrid grid, int gridRow, int destinationY)
        {
            if (canvas == null || grid == null)
                return;

            int cellW = this.CellWidth;
            int cellH = this.CellHeight;
            int rowWidth = grid.Columns * cellW;

            IList<TerminalBackgroundRun> backgroundRuns = TerminalRowSpanBuilder.BuildBackgroundRuns(grid, gridRow);
            foreach (TerminalBackgroundRun run in backgroundRuns)
            {
                if (run.Length <= 0)
                    continue;

                SKPaint paint = this.GetFillPaint(run.Background);
                canvas.DrawRect(
                    run.StartColumn * cellW,
                    destinationY,
                    run.Length * cellW,
                    cellH,
                    paint);
            }

            IList<TerminalTextSpan> textSpans = TerminalRowSpanBuilder.BuildTextSpans(grid, gridRow);
            foreach (TerminalTextSpan span in textSpans)
            {
                if (span.Length <= 0 || string.IsNullOrEmpty(span.Text))
                    continue;

                int x = span.StartColumn * cellW;
                if (span.UseDirectText)
                {
                    this.PaintDirectTextSpan(canvas, span, x, destinationY);
                    continue;
                }

                using (var font = this.CreateFont(span))
                {
                    SKPaint paint = this.GetTextPaint(span.Foreground);
                    for (int i = 0; i < span.Length; i++)
                    {
                        char codePoint = span.Text[i];
                        if (codePoint == ' ')
                            continue;

                        int cellX = x + (i * cellW);
                        canvas.DrawText(
                            codePoint.ToString(),
                            cellX,
                            destinationY + this.textBaseline,
                            font,
                            paint);
                    }
                }
            }
        }

        public void PaintSelectionCell(SKCanvas canvas, TerminalCell cell, int x, int y)
        {
            if (canvas == null)
                return;

            int cellW = this.CellWidth;
            int cellH = this.CellHeight;
            canvas.DrawRect(x, y, cellW, cellH, this.GetFillPaint(cell.Background));

            if (cell.CodePoint == ' ' || cell.Hidden)
                return;

            using (var font = this.CreateFont(cell.Bold, cell.Italic))
            {
                SKPaint paint = this.GetTextPaint(cell.Foreground);
                canvas.DrawText(
                    cell.CodePoint.ToString(),
                    x,
                    y + this.textBaseline,
                    font,
                    paint);
            }
        }

        public void Dispose()
        {
            foreach (SKPaint paint in this.fillPaintCache.Values)
                paint.Dispose();
            this.fillPaintCache.Clear();

            foreach (SKPaint paint in this.textPaintCache.Values)
                paint.Dispose();
            this.textPaintCache.Clear();

            this.regularTypeface.Dispose();
            this.boldTypeface.Dispose();
            this.italicTypeface.Dispose();
            this.boldItalicTypeface.Dispose();
        }

        private void PaintDirectTextSpan(SKCanvas canvas, TerminalTextSpan span, int x, int destinationY)
        {
            int cellW = this.CellWidth;
            int cellH = this.CellHeight;
            for (int i = 0; i < span.Length; i++)
            {
                char codePoint = span.Text[i];
                if (codePoint == ' ')
                    continue;

                int cellX = x + (i * cellW);
                canvas.DrawRect(cellX, destinationY, cellW, cellH, this.GetFillPaint(span.Background));
                using (var font = this.CreateFont(span.Bold, span.Italic))
                {
                    SKPaint paint = this.GetTextPaint(span.Foreground);
                    canvas.DrawText(
                        codePoint.ToString(),
                        cellX,
                        destinationY + this.textBaseline,
                        font,
                        paint);
                }
            }
        }

        private SKFont CreateFont(TerminalTextSpan span)
        {
            return this.CreateFont(span.Bold, span.Italic);
        }

        private SKFont CreateFont(TerminalCell cell)
        {
            return this.CreateFont(cell.Bold, cell.Italic);
        }

        private SKFont CreateFont(bool bold, bool italic)
        {
            SKTypeface typeface;
            if (bold && italic)
                typeface = this.boldItalicTypeface;
            else if (bold)
                typeface = this.boldTypeface;
            else if (italic)
                typeface = this.italicTypeface;
            else
                typeface = this.regularTypeface;

            float fontSize = this.metrics.FontSize;
            return new SKFont(typeface, fontSize)
            {
                Edging = SKFontEdging.Antialias,
                Subpixel = true
            };
        }

        private SKPaint GetFillPaint(Color color)
        {
            int key = color.ToArgb();
            SKPaint cached;
            if (this.fillPaintCache.TryGetValue(key, out cached))
                return cached;

            cached = new SKPaint
            {
                Color = ToSkColor(color),
                Style = SKPaintStyle.Fill,
                IsAntialias = false
            };
            this.fillPaintCache[key] = cached;
            return cached;
        }

        private SKPaint GetTextPaint(Color color)
        {
            long key = ((long)color.ToArgb() << 32) | 1L;
            SKPaint cached;
            if (this.textPaintCache.TryGetValue(key, out cached))
                return cached;

            cached = new SKPaint
            {
                Color = ToSkColor(color),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            this.textPaintCache[key] = cached;
            return cached;
        }

        private static SKTypeface CreateTypeface(SKFontStyle style)
        {
            SKTypeface typeface = SKTypeface.FromFamilyName("Consolas", style);
            if (typeface == null || typeface.FamilyName == null)
                typeface = SKTypeface.FromFamilyName("Courier New", style);
            if (typeface == null)
                typeface = SKTypeface.FromFamilyName(SKTypeface.Default.FamilyName, style);
            return typeface;
        }

        private static SKColor ToSkColor(Color color)
        {
            return new SKColor(color.R, color.G, color.B, color.A);
        }
    }
}
