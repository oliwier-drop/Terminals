// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using VtNetCore.VirtualTerminal;

namespace Terminals.Plugins.SshNet.Rendering
{
    internal sealed class TerminalRenderPipeline : IDisposable
    {
        private const int DirectPaintRowThreshold = 8;

        private readonly TerminalRowDiffer differ = new TerminalRowDiffer();
        private readonly TerminalRowBitmapCache rowCache = new TerminalRowBitmapCache();
        private TerminalFontMetrics fontMetrics;
        private ITerminalPainter painter;
        private TerminalCellGrid previousGrid;
        private TerminalCellGrid workingGrid;
        private float fontPointSize = TerminalDisplayScale.BasePointSize;
        private Bitmap scrollScratch;
        private int scrollScratchWidth;
        private int scrollScratchHeight;

        internal int CellWidth
        {
            get { return this.painter != null ? this.painter.CellWidth : 8; }
        }

        internal int CellHeight
        {
            get { return this.painter != null ? this.painter.CellHeight : 16; }
        }

        internal TerminalCellGrid LastRenderedGrid
        {
            get { return this.workingGrid; }
        }

        internal void UpdateDisplayScale(float fontPointSize)
        {
            if (fontPointSize < TerminalDisplayScale.MinPointSize)
                fontPointSize = TerminalDisplayScale.MinPointSize;
            if (fontPointSize > TerminalDisplayScale.MaxPointSize)
                fontPointSize = TerminalDisplayScale.MaxPointSize;

            if (Math.Abs(this.fontPointSize - fontPointSize) < 0.05f && this.painter != null)
                return;

            this.fontPointSize = fontPointSize;
            this.RebuildFontAndPainter();
        }

        internal void RebuildFontAndPainter()
        {
            this.fontMetrics?.Dispose();
            (this.painter as IDisposable)?.Dispose();
            this.fontMetrics = new TerminalFontMetrics(this.fontPointSize);
            this.painter = new SkiaTerminalPainter(this.fontMetrics);
            this.previousGrid = null;
            this.workingGrid = null;
        }

        internal TerminalCellGrid BuildGrid(VirtualTerminalController controller, int viewTopRow)
        {
            int columns = Math.Max(1, controller.VisibleColumns);
            int rows = Math.Max(1, controller.VisibleRows);
            return TerminalCellGridBuilder.Build(controller, viewTopRow, rows, columns);
        }

        internal IList<int> UpdateFrame(
            Bitmap frameBitmap,
            VirtualTerminalController controller,
            int viewTopRow,
            int frameWidth,
            int frameHeight,
            TerminalRowDiffOptions diffOptions,
            int maxRowsToPaint = int.MaxValue,
            IList<int> deferredRows = null)
        {
            if (frameBitmap == null || controller == null || this.painter == null)
                return Array.Empty<int>();

            TerminalCellGrid grid = this.BuildGrid(controller, viewTopRow);
            this.workingGrid = grid;
            IList<int> dirtyRows = this.differ.GetDirtyRows(this.previousGrid, grid, diffOptions);
            IList<int> rowsToPaint = SplitRowsForBudget(dirtyRows, maxRowsToPaint, deferredRows);
            this.PaintRowsToFrame(frameBitmap, grid, rowsToPaint, frameWidth);
            this.StorePreviousRows(grid, rowsToPaint);
            return rowsToPaint;
        }

        internal IList<int> UpdateFrameWithScroll(
            Bitmap frameBitmap,
            VirtualTerminalController controller,
            int viewTopRow,
            int frameWidth,
            int frameHeight,
            TerminalRowDiffOptions diffOptions,
            int scrollDeltaRows,
            int maxRowsToPaint,
            IList<int> deferredRows)
        {
            if (frameBitmap == null || controller == null || this.painter == null)
                return Array.Empty<int>();

            int visibleRows = Math.Max(1, controller.VisibleRows);
            if (scrollDeltaRows != 0
                && !this.TryScrollBitmapOnly(frameBitmap, scrollDeltaRows, visibleRows, frameWidth))
            {
                this.RebuildFullFrame(frameBitmap, controller, viewTopRow, frameWidth);
                return null;
            }

            TerminalCellGrid grid = this.BuildGrid(controller, viewTopRow);
            this.workingGrid = grid;
            IList<int> dirtyRows = this.differ.GetDirtyRows(this.previousGrid, grid, diffOptions);
            IList<int> rowsToPaint = SplitRowsForBudget(dirtyRows, maxRowsToPaint, deferredRows);
            this.PaintRowsToFrame(frameBitmap, grid, rowsToPaint, frameWidth);
            this.StorePreviousRows(grid, rowsToPaint);
            return rowsToPaint;
        }

        internal IList<int> PaintDeferredRows(
            Bitmap frameBitmap,
            VirtualTerminalController controller,
            int viewTopRow,
            int frameWidth,
            int maxRowsToPaint,
            IList<int> deferredRows)
        {
            if (frameBitmap == null || controller == null || this.painter == null || deferredRows == null || deferredRows.Count == 0)
                return Array.Empty<int>();

            TerminalCellGrid grid = this.BuildGrid(controller, viewTopRow);
            this.workingGrid = grid;
            IList<int> rowsToPaint = TakeRows(deferredRows, maxRowsToPaint);
            this.PaintRowsToFrame(frameBitmap, grid, rowsToPaint, frameWidth);
            this.StorePreviousRows(grid, rowsToPaint);
            return rowsToPaint;
        }

        private void PaintRowsToFrame(Bitmap frameBitmap, TerminalCellGrid grid, IList<int> rowsToPaint, int frameWidth)
        {
            if (rowsToPaint == null || rowsToPaint.Count == 0)
                return;

            int rowHeight = this.CellHeight;
            int rowWidth = Math.Max(1, frameWidth);
            bool useDirectPaint = rowsToPaint.Count <= DirectPaintRowThreshold;

            if (useDirectPaint)
            {
                foreach (int row in rowsToPaint)
                {
                    int y = row * rowHeight;
                    var region = new Rectangle(0, y, rowWidth, rowHeight);
                    SkiaBitmapBridge.PaintRegion(frameBitmap, region, canvas =>
                    {
                        this.painter.ConfigureCanvas(canvas);
                        this.painter.PaintRow(canvas, grid, row, y);
                    });
                }

                return;
            }

            this.rowCache.EnsureSize(rowWidth, rowHeight, grid.Rows);

            foreach (int row in rowsToPaint)
                this.rowCache.PaintRow(row, grid, this.painter);

            this.rowCache.BlitToFrame(frameBitmap, rowsToPaint, rowHeight);
        }

        private static IList<int> SplitRowsForBudget(IList<int> dirtyRows, int maxRowsToPaint, IList<int> deferredRows)
        {
            if (dirtyRows == null || dirtyRows.Count == 0)
                return Array.Empty<int>();

            if (maxRowsToPaint <= 0 || maxRowsToPaint == int.MaxValue)
                return dirtyRows;

            if (dirtyRows.Count <= maxRowsToPaint)
                return dirtyRows;

            var painted = new List<int>(maxRowsToPaint);
            for (int i = 0; i < maxRowsToPaint; i++)
                painted.Add(dirtyRows[i]);

            if (deferredRows != null)
            {
                for (int i = maxRowsToPaint; i < dirtyRows.Count; i++)
                    deferredRows.Add(dirtyRows[i]);
            }

            return painted;
        }

        private static IList<int> TakeRows(IList<int> sourceRows, int maxRowsToPaint)
        {
            if (sourceRows == null || sourceRows.Count == 0)
                return Array.Empty<int>();

            int count = Math.Min(sourceRows.Count, maxRowsToPaint);
            var painted = new List<int>(count);
            for (int i = 0; i < count; i++)
                painted.Add(sourceRows[i]);

            for (int i = count - 1; i >= 0; i--)
                sourceRows.RemoveAt(0);

            return painted;
        }

        internal void RebuildFullFrame(
            Bitmap frameBitmap,
            VirtualTerminalController controller,
            int viewTopRow,
            int frameWidth)
        {
            if (frameBitmap == null || controller == null || this.painter == null)
                return;

            TerminalCellGrid grid = this.BuildGrid(controller, viewTopRow);
            this.workingGrid = grid;
            var options = new TerminalRowDiffOptions { ForceFullRepaint = true };
            IList<int> allRows = this.differ.GetDirtyRows(null, grid, options);
            int rowHeight = this.CellHeight;
            int rowWidth = Math.Max(1, frameWidth);
            this.rowCache.EnsureSize(rowWidth, rowHeight, grid.Rows);

            foreach (int row in allRows)
                this.rowCache.PaintRow(row, grid, this.painter);

            SkiaBitmapBridge.PaintFull(frameBitmap, canvas => this.painter.ConfigureCanvas(canvas));
            this.rowCache.BlitToFrame(frameBitmap, allRows, rowHeight);
            this.StorePreviousGrid(grid);
        }

        internal void InvalidatePreviousGrid()
        {
            this.previousGrid = null;
            this.workingGrid = null;
        }

        internal void InvalidateRowCache()
        {
            this.rowCache.Reset();
        }

        internal void ClearFrame(Bitmap frameBitmap)
        {
            if (frameBitmap == null)
                return;

            SkiaBitmapBridge.PaintFull(frameBitmap, canvas => canvas.Clear(SkiaSharp.SKColors.Black));
        }

        internal void PaintSelection(
            Bitmap targetBitmap,
            TerminalCellGrid grid,
            TerminalCellPoint anchor,
            TerminalCellPoint end,
            Point origin)
        {
            if (targetBitmap == null || grid == null || this.painter == null)
                return;

            TerminalTextSelection.OrderSelectionPoints(anchor, end, out TerminalCellPoint start, out TerminalCellPoint stop);
            int cellW = this.CellWidth;
            int cellH = this.CellHeight;
            int rowStart = Math.Max(0, start.Row);
            int rowEnd = Math.Min(grid.Rows - 1, stop.Row);

            for (int row = rowStart; row <= rowEnd; row++)
            {
                TerminalTextSelection.GetStreamLineColumnRange(start, stop, row, grid.Columns, out int colStart, out int colEnd);
                if (colEnd < colStart)
                    continue;

                colStart = Math.Max(0, colStart);
                colEnd = Math.Min(grid.Columns - 1, colEnd);
                if (colEnd < colStart)
                    continue;

                int x = origin.X + (colStart * cellW);
                int y = origin.Y + (row * cellH);
                int width = ((colEnd - colStart) + 1) * cellW;
                var region = new Rectangle(x, y, width, cellH);
                SkiaBitmapBridge.PaintRegion(targetBitmap, region, canvas =>
                {
                    for (int col = colStart; col <= colEnd; col++)
                    {
                        TerminalCell cell = grid[row, col];
                        if (cell.Hidden)
                            continue;

                        TerminalCell styled = TerminalTextSelection.StyleForSelection(cell);
                        this.painter.PaintSelectionCell(
                            canvas,
                            styled,
                            origin.X + (col * cellW),
                            origin.Y + (row * cellH));
                    }
                });
            }
        }

        internal bool TryScrollBitmapOnly(
            Bitmap frameBitmap,
            int scrollDeltaRows,
            int visibleRows,
            int frameWidth)
        {
            if (frameBitmap == null || scrollDeltaRows == 0)
                return scrollDeltaRows == 0;

            if (this.previousGrid == null || Math.Abs(scrollDeltaRows) >= visibleRows)
                return false;

            int rowH = this.CellHeight;
            int rowW = Math.Max(1, frameWidth);
            int contentH = visibleRows * rowH;
            int pixelDelta = scrollDeltaRows * rowH;
            if (Math.Abs(pixelDelta) >= contentH)
                return false;

            this.EnsureScrollScratch(rowW, contentH);

            using (var scratchGraphics = Graphics.FromImage(this.scrollScratch))
            {
                if (scrollDeltaRows > 0)
                {
                    int copyH = contentH - pixelDelta;
                    scratchGraphics.DrawImage(
                        frameBitmap,
                        new Rectangle(0, 0, rowW, copyH),
                        new Rectangle(0, pixelDelta, rowW, copyH),
                        GraphicsUnit.Pixel);

                    using (var frameGraphics = Graphics.FromImage(frameBitmap))
                        frameGraphics.DrawImage(this.scrollScratch, 0, 0);
                }
                else
                {
                    int rowsEntered = -scrollDeltaRows;
                    pixelDelta = rowsEntered * rowH;
                    int copyH = contentH - pixelDelta;
                    scratchGraphics.DrawImage(
                        frameBitmap,
                        new Rectangle(0, pixelDelta, rowW, copyH),
                        new Rectangle(0, 0, rowW, copyH),
                        GraphicsUnit.Pixel);

                    using (var frameGraphics = Graphics.FromImage(frameBitmap))
                        frameGraphics.DrawImage(this.scrollScratch, 0, 0);
                }
            }

            return true;
        }

        internal bool TryScrollFrame(
            Bitmap frameBitmap,
            VirtualTerminalController controller,
            int viewTopRow,
            int scrollDeltaRows,
            int frameWidth)
        {
            if (frameBitmap == null
                || controller == null
                || this.painter == null
                || scrollDeltaRows == 0)
            {
                return scrollDeltaRows == 0;
            }

            int visibleRows = Math.Max(1, controller.VisibleRows);
            if (!this.TryScrollBitmapOnly(frameBitmap, scrollDeltaRows, visibleRows, frameWidth))
                return false;

            TerminalCellGrid grid = this.BuildGrid(controller, viewTopRow);
            this.workingGrid = grid;
            int rowH = this.CellHeight;
            int rowW = Math.Max(1, frameWidth);
            this.rowCache.EnsureSize(rowW, rowH, visibleRows);

            var exposedRows = new List<int>();
            if (scrollDeltaRows > 0)
            {
                for (int row = visibleRows - scrollDeltaRows; row < visibleRows; row++)
                {
                    this.rowCache.PaintRow(row, grid, this.painter);
                    exposedRows.Add(row);
                }
            }
            else
            {
                int rowsEntered = -scrollDeltaRows;
                for (int row = 0; row < rowsEntered; row++)
                {
                    this.rowCache.PaintRow(row, grid, this.painter);
                    exposedRows.Add(row);
                }
            }

            this.rowCache.BlitToFrame(frameBitmap, exposedRows, rowH);
            this.StorePreviousGrid(grid);
            return true;
        }

        private void StorePreviousRows(TerminalCellGrid grid, IList<int> paintedRows)
        {
            if (grid == null)
                return;

            if (paintedRows == null || paintedRows.Count == 0)
                return;

            if (this.previousGrid == null
                || this.previousGrid.Columns != grid.Columns
                || this.previousGrid.Rows != grid.Rows)
            {
                this.previousGrid = grid.Clone();
                return;
            }

            this.previousGrid.CopyRowsFrom(grid, paintedRows);
        }

        private void StorePreviousGrid(TerminalCellGrid grid)
        {
            if (this.previousGrid == null
                || this.previousGrid.Columns != grid.Columns
                || this.previousGrid.Rows != grid.Rows)
            {
                this.previousGrid = grid.Clone();
                return;
            }

            this.previousGrid.CopyFrom(grid);
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

            this.scrollScratch = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            this.scrollScratchWidth = width;
            this.scrollScratchHeight = height;
        }

        internal static bool ChunkRequiresFullRepaint(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
                return false;

            return chunk.IndexOf("\x1b[?1049", StringComparison.Ordinal) >= 0
                || chunk.IndexOf("\x1b[?1047", StringComparison.Ordinal) >= 0
                || chunk.IndexOf("\x1b[?47", StringComparison.Ordinal) >= 0
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

            (this.painter as IDisposable)?.Dispose();
            this.fontMetrics?.Dispose();
            this.painter = null;
            this.fontMetrics = null;
            this.previousGrid = null;
            this.workingGrid = null;
        }
    }
}
