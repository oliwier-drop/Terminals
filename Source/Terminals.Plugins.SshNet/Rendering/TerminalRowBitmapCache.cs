// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Terminals.Plugins.SshNet.Rendering
{
    internal sealed class TerminalRowBitmapCache : IDisposable
    {
        private Bitmap[] rowBitmaps;
        private int rowWidth;
        private int rowHeight;

        internal void EnsureSize(int width, int rowHeight, int rowCount)
        {
            if (width < 1)
                width = 1;
            if (rowHeight < 1)
                rowHeight = 1;
            if (rowCount < 1)
                rowCount = 1;

            bool sizeChanged = this.rowBitmaps == null
                || this.rowBitmaps.Length != rowCount
                || this.rowWidth != width
                || this.rowHeight != rowHeight;

            if (!sizeChanged)
                return;

            this.DisposeRows();
            this.rowWidth = width;
            this.rowHeight = rowHeight;
            this.rowBitmaps = new Bitmap[rowCount];
            for (int i = 0; i < rowCount; i++)
            {
                this.rowBitmaps[i] = new Bitmap(width, rowHeight);
                using (Graphics g = Graphics.FromImage(this.rowBitmaps[i]))
                    g.Clear(Color.Black);
            }
        }

        internal void PaintRow(int rowIndex, TerminalCellGrid grid, TerminalAtlasPainter painter)
        {
            if (this.rowBitmaps == null
                || rowIndex < 0
                || rowIndex >= this.rowBitmaps.Length
                || grid == null
                || painter == null)
            {
                return;
            }

            Bitmap rowBitmap = this.rowBitmaps[rowIndex];
            using (Graphics graphics = Graphics.FromImage(rowBitmap))
            {
                graphics.Clear(Color.Black);
                painter.PaintRow(graphics, grid, rowIndex, 0);
            }
        }

        internal void BlitToFrame(Graphics frameGraphics, IList<int> dirtyRows)
        {
            if (frameGraphics == null || this.rowBitmaps == null || dirtyRows == null)
                return;

            foreach (int row in dirtyRows)
            {
                if (row < 0 || row >= this.rowBitmaps.Length)
                    continue;

                int y = row * this.rowHeight;
                frameGraphics.DrawImage(this.rowBitmaps[row], 0, y);
            }
        }

        internal void BlitAllRows(Graphics frameGraphics)
        {
            if (frameGraphics == null || this.rowBitmaps == null)
                return;

            for (int row = 0; row < this.rowBitmaps.Length; row++)
            {
                int y = row * this.rowHeight;
                frameGraphics.DrawImage(this.rowBitmaps[row], 0, y);
            }
        }

        public void Dispose()
        {
            this.DisposeRows();
        }

        private void DisposeRows()
        {
            if (this.rowBitmaps == null)
                return;

            for (int i = 0; i < this.rowBitmaps.Length; i++)
            {
                if (this.rowBitmaps[i] != null)
                {
                    this.rowBitmaps[i].Dispose();
                    this.rowBitmaps[i] = null;
                }
            }

            this.rowBitmaps = null;
        }
    }
}
