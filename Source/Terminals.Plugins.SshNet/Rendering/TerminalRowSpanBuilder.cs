// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Terminals.Plugins.SshNet.Rendering
{
    internal struct TerminalBackgroundRun
    {
        internal int StartColumn;
        internal int Length;
        internal Color Background;
    }

    internal struct TerminalTextSpan
    {
        internal int StartColumn;
        internal int Length;
        internal Color Foreground;
        internal Color Background;
        internal bool Bold;
        internal bool Italic;
        internal bool UseDirectText;
        internal string Text;
    }

    internal static class TerminalRowSpanBuilder
    {
        internal static IList<TerminalBackgroundRun> BuildBackgroundRuns(TerminalCellGrid grid, int gridRow)
        {
            var runs = new List<TerminalBackgroundRun>();
            if (grid == null)
                return runs;

            int columns = grid.Columns;
            if (columns <= 0)
                return runs;

            int runStart = 0;
            Color runColor = grid[gridRow, 0].Background;
            for (int col = 1; col <= columns; col++)
            {
                Color nextColor = col < columns ? grid[gridRow, col].Background : Color.Empty;
                if (col == columns || nextColor.ToArgb() != runColor.ToArgb())
                {
                    runs.Add(new TerminalBackgroundRun
                    {
                        StartColumn = runStart,
                        Length = col - runStart,
                        Background = runColor
                    });
                    if (col < columns)
                    {
                        runStart = col;
                        runColor = nextColor;
                    }
                }
            }

            return runs;
        }

        internal static IList<TerminalTextSpan> BuildTextSpans(TerminalCellGrid grid, int gridRow)
        {
            var spans = new List<TerminalTextSpan>();
            if (grid == null)
                return spans;

            int columns = grid.Columns;
            if (columns <= 0)
                return spans;

            int spanStart = -1;
            TerminalCell spanCell = TerminalCell.Empty;
            var text = new StringBuilder();

            for (int col = 0; col <= columns; col++)
            {
                bool endSpan = col == columns;
                TerminalCell cell = endSpan ? TerminalCell.Empty : grid[gridRow, col];
                bool drawable = !endSpan && !cell.Hidden && cell.CodePoint != ' ';
                bool sameStyle = drawable
                    && spanStart >= 0
                    && cell.CodePoint != ' '
                    && cell.Foreground.ToArgb() == spanCell.Foreground.ToArgb()
                    && cell.Background.ToArgb() == spanCell.Background.ToArgb()
                    && cell.Bold == spanCell.Bold
                    && cell.Italic == spanCell.Italic
                    && TerminalRenderPolicy.ShouldUseDirectTextRender(cell)
                        == TerminalRenderPolicy.ShouldUseDirectTextRender(spanCell);

                if (!endSpan && drawable && (spanStart < 0 || sameStyle))
                {
                    if (spanStart < 0)
                    {
                        spanStart = col;
                        spanCell = cell;
                        text.Length = 0;
                    }

                    text.Append(cell.CodePoint);
                    continue;
                }

                if (spanStart >= 0)
                {
                    spans.Add(new TerminalTextSpan
                    {
                        StartColumn = spanStart,
                        Length = text.Length,
                        Foreground = spanCell.Foreground,
                        Background = spanCell.Background,
                        Bold = spanCell.Bold,
                        Italic = spanCell.Italic,
                        UseDirectText = TerminalRenderPolicy.ShouldUseDirectTextRender(spanCell),
                        Text = text.ToString()
                    });
                    spanStart = -1;
                    text.Length = 0;
                }

                if (!endSpan && drawable)
                {
                    spanStart = col;
                    spanCell = cell;
                    text.Length = 0;
                    text.Append(cell.CodePoint);
                }
            }

            return spans;
        }
    }
}
