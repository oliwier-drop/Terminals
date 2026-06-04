// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System.Collections.Generic;
using VtNetCore.VirtualTerminal;
using VtNetCore.VirtualTerminal.Layout;

namespace Terminals.Plugins.SshNet.Rendering
{
    internal static class TerminalCellGridBuilder
    {
        internal static TerminalCellGrid Build(
            VirtualTerminalController controller,
            int viewTopRow,
            int rows,
            int columns)
        {
            if (controller == null)
                return new TerminalCellGrid(columns, rows);

            if (columns < 1)
                columns = 1;
            if (rows < 1)
                rows = 1;

            var grid = new TerminalCellGrid(columns, rows);
            List<LayoutRow> layoutRows = controller.GetPageSpans(viewTopRow, rows, columns, null);
            if (layoutRows == null || layoutRows.Count == 0)
                return grid;

            int rowCount = System.Math.Min(rows, layoutRows.Count);
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                LayoutRow layoutRow = layoutRows[rowIndex];
                FillRow(grid, rowIndex, columns, layoutRow);
            }

            return grid;
        }

        private static void FillRow(TerminalCellGrid grid, int rowIndex, int columns, LayoutRow layoutRow)
        {
            int column = 0;
            if (layoutRow == null || layoutRow.Spans == null)
            {
                PadRow(grid, rowIndex, column, columns, TerminalCell.Empty);
                return;
            }

            foreach (LayoutSpan span in layoutRow.Spans)
            {
                if (span == null || span.Hidden)
                    continue;

                string text = span.Text ?? string.Empty;
                if (text.Length == 0)
                    continue;

                var cell = new TerminalCell
                {
                    Foreground = VtNetColorHelper.ParseForeground(span.ForgroundColor),
                    Background = VtNetColorHelper.ParseBackground(span.BackgroundColor),
                    Bold = span.Bold,
                    Italic = span.Italic,
                    Hidden = false
                };

                for (int i = 0; i < text.Length && column < columns; i++)
                {
                    char codePoint = text[i] == '\t' ? ' ' : text[i];
                    cell.CodePoint = codePoint;
                    grid[rowIndex, column] = cell;
                    column++;
                }
            }

            PadRow(grid, rowIndex, column, columns, TerminalCell.Empty);
        }

        private static void PadRow(
            TerminalCellGrid grid,
            int rowIndex,
            int startColumn,
            int columns,
            TerminalCell padCell)
        {
            for (int col = startColumn; col < columns; col++)
                grid[rowIndex, col] = padCell;
        }
    }
}
