// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Drawing;
using System.Text;
using Terminals.Plugins.SshNet.Rendering;
using VtNetCore.VirtualTerminal;

namespace Terminals.Plugins.SshNet
{
    internal struct TerminalCellPoint
    {
        internal int Row;
        internal int Column;

        internal TerminalCellPoint(int row, int column)
        {
            this.Row = row;
            this.Column = column;
        }
    }

    internal static class TerminalTextSelection
    {
        private static readonly Color SelectionTextColor = Color.Black;

        /// <summary>Stream selection: follows lines (anchor column → EOL → full lines → BOL → end column).</summary>
        internal static void OrderSelectionPoints(
            TerminalCellPoint anchor,
            TerminalCellPoint end,
            out TerminalCellPoint start,
            out TerminalCellPoint stop)
        {
            if (end.Row < anchor.Row
                || (end.Row == anchor.Row && end.Column < anchor.Column))
            {
                start = end;
                stop = anchor;
            }
            else
            {
                start = anchor;
                stop = end;
            }
        }

        internal static void GetStreamLineColumnRange(
            TerminalCellPoint start,
            TerminalCellPoint stop,
            int row,
            int columns,
            out int colStart,
            out int colEnd)
        {
            colStart = 0;
            colEnd = -1;
            if (columns <= 0 || row < start.Row || row > stop.Row)
                return;

            int lastCol = columns - 1;
            if (start.Row == stop.Row)
            {
                colStart = start.Column;
                colEnd = stop.Column;
                return;
            }

            if (row == start.Row)
            {
                colStart = start.Column;
                colEnd = lastCol;
                return;
            }

            if (row == stop.Row)
            {
                colStart = 0;
                colEnd = stop.Column;
                return;
            }

            colStart = 0;
            colEnd = lastCol;
        }

        internal static Rectangle GetSelectionPixelBounds(
            TerminalCellPoint anchor,
            TerminalCellPoint end,
            int columns,
            int cellWidth,
            int cellHeight)
        {
            if (columns <= 0 || cellWidth <= 0 || cellHeight <= 0)
                return Rectangle.Empty;

            OrderSelectionPoints(anchor, end, out TerminalCellPoint start, out TerminalCellPoint stop);
            int rowStart = start.Row;
            int rowEnd = stop.Row;
            int leftCol = columns;
            int rightCol = 0;
            for (int row = rowStart; row <= rowEnd; row++)
            {
                GetStreamLineColumnRange(start, stop, row, columns, out int colStart, out int colEnd);
                if (colEnd < colStart)
                    continue;

                leftCol = Math.Min(leftCol, colStart);
                rightCol = Math.Max(rightCol, colEnd);
            }

            if (rightCol < leftCol)
                return Rectangle.Empty;

            return new Rectangle(
                leftCol * cellWidth,
                rowStart * cellHeight,
                (rightCol - leftCol + 1) * cellWidth,
                (rowEnd - rowStart + 1) * cellHeight);
        }

        /// <summary>Selection uses original text color as background and black glyphs.</summary>
        internal static TerminalCell StyleForSelection(TerminalCell cell)
        {
            Color selectionBackground = ResolveSelectionBackground(cell);
            return new TerminalCell
            {
                CodePoint = cell.CodePoint,
                Foreground = SelectionTextColor,
                Background = selectionBackground,
                Bold = cell.Bold,
                Italic = cell.Italic,
                Hidden = false
            };
        }

        private static Color ResolveSelectionBackground(TerminalCell cell)
        {
            Color textColor = cell.Foreground;
            if (IsStrongEnoughSelectionBackground(textColor))
                return textColor;

            if (IsStrongEnoughSelectionBackground(cell.Background))
                return cell.Background;

            return VtNetColorHelper.DefaultForegroundColor;
        }

        private static bool IsStrongEnoughSelectionBackground(Color color)
        {
            return color.R + color.G + color.B >= 96;
        }

        internal static string ExtractText(
            VirtualTerminalController controller,
            int viewTopRow,
            int rows,
            int columns,
            TerminalCellPoint anchor,
            TerminalCellPoint end)
        {
            if (controller == null || rows <= 0 || columns <= 0)
                return string.Empty;

            TerminalCellGrid grid = TerminalCellGridBuilder.Build(controller, viewTopRow, rows, columns);
            return ExtractTextFromGrid(grid, anchor, end);
        }

        internal static string ExtractTextFromGrid(
            TerminalCellGrid grid,
            TerminalCellPoint anchor,
            TerminalCellPoint end)
        {
            if (grid == null)
                return string.Empty;

            OrderSelectionPoints(anchor, end, out TerminalCellPoint start, out TerminalCellPoint stop);
            int rowStart = Math.Max(0, start.Row);
            int rowEnd = Math.Min(grid.Rows - 1, stop.Row);
            if (rowEnd < rowStart)
                return string.Empty;

            var sb = new StringBuilder();
            for (int row = rowStart; row <= rowEnd; row++)
            {
                GetStreamLineColumnRange(start, stop, row, grid.Columns, out int colStart, out int colEnd);
                if (colEnd < colStart)
                    continue;

                colStart = Math.Max(0, colStart);
                colEnd = Math.Min(grid.Columns - 1, colEnd);
                if (row > rowStart)
                    sb.Append('\n');

                sb.Append(ExtractGridLineSlice(grid, row, colStart, colEnd));
            }

            return sb.ToString();
        }

        private static string ExtractGridLineSlice(TerminalCellGrid grid, int row, int startCol, int endCol)
        {
            var sb = new StringBuilder(endCol - startCol + 1);
            for (int col = startCol; col <= endCol; col++)
            {
                char codePoint = grid[row, col].CodePoint;
                if (!IsCopyableCharacter(codePoint))
                    continue;

                sb.Append(codePoint == '\t' ? ' ' : codePoint);
            }

            return TrimTrailingSpaces(sb.ToString());
        }

        private static bool IsCopyableCharacter(char codePoint)
        {
            return codePoint >= 0x20 && codePoint != 0x7F;
        }

        private static string TrimTrailingSpaces(string line)
        {
            if (string.IsNullOrEmpty(line))
                return line;

            int end = line.Length;
            while (end > 0 && line[end - 1] == ' ')
                end--;

            return end == line.Length ? line : line.Substring(0, end);
        }
    }
}
