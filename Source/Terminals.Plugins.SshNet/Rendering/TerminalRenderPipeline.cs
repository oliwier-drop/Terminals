// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using VtNetCore.VirtualTerminal;

namespace Terminals.Plugins.SshNet.Rendering
{
    internal sealed class TerminalRenderPipeline : IDisposable
    {
        private readonly TerminalRowDiffer differ = new TerminalRowDiffer();
        private readonly TerminalRowBitmapCache rowCache = new TerminalRowBitmapCache();
        private TerminalFontMetrics fontMetrics;
        private TerminalGlyphAtlas glyphAtlas;
        private TerminalAtlasPainter painter;
        private TerminalCellGrid previousGrid;
        private float dpiScale = 1f;
        private Bitmap scrollScratch;
        private int scrollScratchWidth;
        private int scrollScratchHeight;

        internal int CellWidth
        {
            get { return this.glyphAtlas != null ? this.glyphAtlas.CellWidth : 8; }
        }

        internal int CellHeight
        {
            get { return this.glyphAtlas != null ? this.glyphAtlas.CellHeight : 16; }
        }

        internal void UpdateDpiScale(float scale)
        {
            if (scale < 0.5f)
                scale = 0.5f;
            if (scale > 4f)
                scale = 4f;

            if (Math.Abs(this.dpiScale - scale) < 0.01f && this.glyphAtlas != null)
                return;

            this.dpiScale = scale;
            this.RebuildFontAndAtlas();
        }

        internal void RebuildFontAndAtlas()
        {
            this.fontMetrics?.Dispose();
            this.glyphAtlas?.Dispose();
            this.fontMetrics = new TerminalFontMetrics(10f, this.dpiScale);
            this.glyphAtlas = new TerminalGlyphAtlas(this.fontMetrics);
            this.painter = new TerminalAtlasPainter(this.glyphAtlas);
            this.previousGrid = null;
        }

        internal TerminalCellGrid BuildGrid(VirtualTerminalController controller, int viewTopRow)
        {
            int columns = Math.Max(1, controller.VisibleColumns);
            int rows = Math.Max(1, controller.VisibleRows);
            return TerminalCellGridBuilder.Build(controller, viewTopRow, rows, columns);
        }

        internal IList<int> UpdateFrame(
            Graphics frameGraphics,
            VirtualTerminalController controller,
            int viewTopRow,
            int frameWidth,
            int frameHeight,
            TerminalRowDiffOptions diffOptions)
        {
            if (frameGraphics == null || controller == null || this.painter == null)
                return Array.Empty<int>();

            TerminalCellGrid grid = this.BuildGrid(controller, viewTopRow);
            IList<int> dirtyRows = this.differ.GetDirtyRows(this.previousGrid, grid, diffOptions);
            this.painter.ConfigureGraphics(frameGraphics);
            int rowHeight = this.CellHeight;
            foreach (int row in dirtyRows)
            {
                int y = row * rowHeight;
                this.painter.PaintRow(frameGraphics, grid, row, y);
            }

            this.previousGrid = grid.Clone();
            return dirtyRows;
        }

        internal void RebuildFullFrame(
            Graphics frameGraphics,
            VirtualTerminalController controller,
            int viewTopRow,
            int frameWidth)
        {
            if (frameGraphics == null || controller == null || this.painter == null)
                return;

            TerminalCellGrid grid = this.BuildGrid(controller, viewTopRow);
            var options = new TerminalRowDiffOptions { ForceFullRepaint = true };
            IList<int> allRows = this.differ.GetDirtyRows(null, grid, options);
            frameGraphics.Clear(Color.Black);
            this.painter.ConfigureGraphics(frameGraphics);
            int rowHeight = this.CellHeight;
            foreach (int row in allRows)
            {
                int y = row * rowHeight;
                this.painter.PaintRow(frameGraphics, grid, row, y);
            }
            this.previousGrid = grid.Clone();
        }

        internal void InvalidatePreviousGrid()
        {
            this.previousGrid = null;
        }

        internal void PaintSelection(
            Graphics target,
            TerminalCellGrid grid,
            TerminalCellPoint anchor,
            TerminalCellPoint end)
        {
            if (target == null || grid == null || this.painter == null)
                return;

            TerminalTextSelection.OrderSelectionPoints(anchor, end, out TerminalCellPoint start, out TerminalCellPoint stop);
            int cellW = this.CellWidth;
            int cellH = this.CellHeight;
            int rowStart = Math.Max(0, start.Row);
            int rowEnd = Math.Min(grid.Rows - 1, stop.Row);

            var previousHint = target.TextRenderingHint;
            target.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            for (int row = rowStart; row <= rowEnd; row++)
            {
                TerminalTextSelection.GetStreamLineColumnRange(start, stop, row, grid.Columns, out int colStart, out int colEnd);
                if (colEnd < colStart)
                    continue;

                colStart = Math.Max(0, colStart);
                colEnd = Math.Min(grid.Columns - 1, colEnd);
                for (int col = colStart; col <= colEnd; col++)
                {
                    TerminalCell cell = grid[row, col];
                    if (cell.Hidden)
                        continue;

                    TerminalCell styled = TerminalTextSelection.StyleForSelection(cell);
                    int x = col * cellW;
                    int y = row * cellH;
                    this.painter.PaintSelectionCell(target, styled, x, y);
                }
            }

            target.TextRenderingHint = previousHint;
        }

        internal void PaintTerminalRows(
            Graphics target,
            TerminalCellGrid grid,
            int rowStart,
            int rowEnd)
        {
            if (target == null || grid == null || this.painter == null)
                return;

            this.painter.ConfigureGraphics(target);
            int rowHeight = this.CellHeight;
            rowStart = Math.Max(0, rowStart);
            rowEnd = Math.Min(grid.Rows - 1, rowEnd);
            for (int row = rowStart; row <= rowEnd; row++)
            {
                int y = row * rowHeight;
                this.painter.PaintRow(target, grid, row, y);
            }
        }

        /// <summary>
        /// Scroll viewport by shifting the frame bitmap and painting only exposed rows.
        /// Returns false when a full rebuild is required.
        /// </summary>
        internal bool TryScrollFrame(
            Graphics frameGraphics,
            Bitmap frameBitmap,
            VirtualTerminalController controller,
            int viewTopRow,
            int scrollDeltaRows,
            int frameWidth)
        {
            if (frameGraphics == null
                || frameBitmap == null
                || controller == null
                || this.painter == null
                || scrollDeltaRows == 0)
            {
                return scrollDeltaRows == 0;
            }

            int visibleRows = Math.Max(1, controller.VisibleRows);
            if (this.previousGrid == null || Math.Abs(scrollDeltaRows) >= visibleRows)
                return false;

            int rowH = this.CellHeight;
            int rowW = Math.Max(1, frameWidth);
            int contentH = visibleRows * rowH;
            int pixelDelta = scrollDeltaRows * rowH;
            if (Math.Abs(pixelDelta) >= contentH)
                return false;

            TerminalCellGrid grid = this.BuildGrid(controller, viewTopRow);
            this.EnsureScrollScratch(rowW, contentH);
            this.painter.ConfigureGraphics(frameGraphics);

            if (scrollDeltaRows > 0)
            {
                int copyH = contentH - pixelDelta;
                using (Graphics scratchG = Graphics.FromImage(this.scrollScratch))
                {
                    scratchG.DrawImage(
                        frameBitmap,
                        new Rectangle(0, 0, rowW, copyH),
                        new Rectangle(0, pixelDelta, rowW, copyH),
                        GraphicsUnit.Pixel);
                }

                frameGraphics.DrawImage(this.scrollScratch, 0, 0);
                for (int row = visibleRows - scrollDeltaRows; row < visibleRows; row++)
                    this.painter.PaintRow(frameGraphics, grid, row, row * rowH);
            }
            else
            {
                int rowsEntered = -scrollDeltaRows;
                pixelDelta = rowsEntered * rowH;
                int copyH = contentH - pixelDelta;
                using (Graphics scratchG = Graphics.FromImage(this.scrollScratch))
                {
                    scratchG.DrawImage(
                        frameBitmap,
                        new Rectangle(0, pixelDelta, rowW, copyH),
                        new Rectangle(0, 0, rowW, copyH),
                        GraphicsUnit.Pixel);
                }

                frameGraphics.DrawImage(this.scrollScratch, 0, 0);
                for (int row = 0; row < rowsEntered; row++)
                    this.painter.PaintRow(frameGraphics, grid, row, row * rowH);
            }

            this.previousGrid = grid.Clone();
            return true;
        }

        private void EnsureScrollScratch(int width, int height)
        {
            if (this.scrollScratch != null
                && this.scrollScratchWidth == width
                && this.scrollScratchHeight == height)
            {
                return;
            }

            if (this.scrollScratch != null)
                this.scrollScratch.Dispose();

            this.scrollScratch = new Bitmap(width, height);
            this.scrollScratchWidth = width;
            this.scrollScratchHeight = height;
        }

        internal static bool ChunkRequiresFullRepaint(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
                return false;

            // Only buffer-wide changes — SGR (\x1b[31m etc.) uses per-row diff.
            return chunk.IndexOf("\x1b[?1049", StringComparison.Ordinal) >= 0
                || chunk.IndexOf("\x1b[2J", StringComparison.Ordinal) >= 0
                || chunk.IndexOf("\x1b[3J", StringComparison.Ordinal) >= 0;
        }

        public void Dispose()
        {
            this.rowCache.Dispose();
            if (this.scrollScratch != null)
            {
                this.scrollScratch.Dispose();
                this.scrollScratch = null;
            }

            this.glyphAtlas?.Dispose();
            this.fontMetrics?.Dispose();
            this.glyphAtlas = null;
            this.fontMetrics = null;
            this.painter = null;
            this.previousGrid = null;
        }
    }
}
