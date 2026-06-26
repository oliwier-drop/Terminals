// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Collections.Generic;

namespace Terminals.Plugins.SshNet.Rendering
{
    internal sealed class TerminalCellGrid
    {
        private readonly TerminalCell[] cells;

        internal TerminalCellGrid(int columns, int rows)
        {
            if (columns < 1)
                columns = 1;
            if (rows < 1)
                rows = 1;

            this.Columns = columns;
            this.Rows = rows;
            this.cells = new TerminalCell[columns * rows];
            var empty = TerminalCell.Empty;
            for (int i = 0; i < this.cells.Length; i++)
                this.cells[i] = empty;
        }

        internal int Columns { get; }

        internal int Rows { get; }

        internal TerminalCell this[int row, int column]
        {
            get { return this.cells[(row * this.Columns) + column]; }
            set { this.cells[(row * this.Columns) + column] = value; }
        }

        internal ulong GetRowHash(int row)
        {
            ulong hash = 14695981039346656037UL;
            int offset = row * this.Columns;
            for (int col = 0; col < this.Columns; col++)
            {
                TerminalCell cell = this.cells[offset + col];
                hash ^= (ulong)cell.CodePoint;
                hash *= 1099511628211UL;
                hash ^= (ulong)(uint)cell.Foreground.ToArgb();
                hash *= 1099511628211UL;
                hash ^= (ulong)(uint)cell.Background.ToArgb();
                hash *= 1099511628211UL;
                hash ^= cell.Bold ? 1UL : 0UL;
                hash *= 1099511628211UL;
                hash ^= cell.Italic ? 1UL : 0UL;
                hash *= 1099511628211UL;
                hash ^= cell.Hidden ? 1UL : 0UL;
                hash *= 1099511628211UL;
            }

            return hash;
        }

        internal void CopyFrom(TerminalCellGrid source)
        {
            if (source == null
                || source.Columns != this.Columns
                || source.Rows != this.Rows)
            {
                return;
            }

            Array.Copy(source.cells, this.cells, this.cells.Length);
        }

        internal void CopyRowFrom(TerminalCellGrid source, int row)
        {
            if (source == null
                || source.Columns != this.Columns
                || source.Rows != this.Rows
                || row < 0
                || row >= this.Rows)
            {
                return;
            }

            int sourceOffset = row * this.Columns;
            int destOffset = sourceOffset;
            Array.Copy(source.cells, sourceOffset, this.cells, destOffset, this.Columns);
        }

        internal void CopyRowsFrom(TerminalCellGrid source, IList<int> rows)
        {
            if (source == null || rows == null || rows.Count == 0)
                return;

            for (int i = 0; i < rows.Count; i++)
                this.CopyRowFrom(source, rows[i]);
        }

        internal TerminalCellGrid Clone()
        {
            var clone = new TerminalCellGrid(this.Columns, this.Rows);
            Array.Copy(this.cells, clone.cells, this.cells.Length);
            return clone;
        }
    }
}
