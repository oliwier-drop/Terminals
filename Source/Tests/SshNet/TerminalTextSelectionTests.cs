// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Plugins.SshNet;
using Terminals.Plugins.SshNet.Rendering;

namespace Tests.SshNet
{
    [TestClass]
    public class TerminalTextSelectionTests
    {
        [TestMethod]
        public void ExtractText_SingleLine_ReturnsSlice()
        {
            var session = new SshVtSession();
            session.Push("hello world");

            string text = TerminalTextSelection.ExtractText(
                session.Controller,
                viewTopRow: 0,
                rows: session.Rows,
                columns: session.Columns,
                new TerminalCellPoint(0, 0),
                new TerminalCellPoint(0, 4));

            Assert.AreEqual("hello", text);
        }

        [TestMethod]
        public void ExtractText_MultiLine_IncludesNewline()
        {
            var session = new SshVtSession();
            session.Push("ab\r\ncd");

            string text = TerminalTextSelection.ExtractText(
                session.Controller,
                0,
                session.Rows,
                session.Columns,
                new TerminalCellPoint(0, 0),
                new TerminalCellPoint(1, 1));

            Assert.AreEqual("ab\ncd", text);
        }

        [TestMethod]
        public void ExtractText_MultiLine_StreamSelectionFollowsLines()
        {
            var session = new SshVtSession();
            session.Push("abcdef\r\nghijkl");

            string text = TerminalTextSelection.ExtractText(
                session.Controller,
                0,
                session.Rows,
                session.Columns,
                new TerminalCellPoint(0, 2),
                new TerminalCellPoint(1, 4));

            Assert.AreEqual("cdef\nghijk", text);
        }

        [TestMethod]
        public void GetStreamLineColumnRange_MiddleRow_IsFullWidth()
        {
            var start = new TerminalCellPoint(0, 5);
            var stop = new TerminalCellPoint(2, 3);
            TerminalTextSelection.GetStreamLineColumnRange(start, stop, 1, 80, out int colStart, out int colEnd);

            Assert.AreEqual(0, colStart);
            Assert.AreEqual(79, colEnd);
        }

        [TestMethod]
        public void StyleForSelection_DefaultTerminal_UsesTextColorAsBackground()
        {
            var cell = Terminals.Plugins.SshNet.Rendering.TerminalCell.Empty;

            var styled = TerminalTextSelection.StyleForSelection(cell);

            Assert.AreEqual(System.Drawing.Color.Black.ToArgb(), styled.Foreground.ToArgb());
            Assert.AreEqual(
                Terminals.Plugins.SshNet.VtNetColorHelper.DefaultForegroundColor.ToArgb(),
                styled.Background.ToArgb());
        }

        [TestMethod]
        public void StyleForSelection_YellowOnBlack_UsesYellowBackgroundBlackText()
        {
            var cell = new Terminals.Plugins.SshNet.Rendering.TerminalCell
            {
                CodePoint = 'X',
                Foreground = System.Drawing.Color.Yellow,
                Background = System.Drawing.Color.Black
            };

            var styled = TerminalTextSelection.StyleForSelection(cell);

            Assert.AreEqual(System.Drawing.Color.Black.ToArgb(), styled.Foreground.ToArgb());
            Assert.AreEqual(System.Drawing.Color.Yellow.ToArgb(), styled.Background.ToArgb());
        }

        [TestMethod]
        public void OrderSelectionPoints_BackwardDrag_SwapsToDocumentOrder()
        {
            TerminalTextSelection.OrderSelectionPoints(
                new TerminalCellPoint(2, 10),
                new TerminalCellPoint(0, 2),
                out TerminalCellPoint start,
                out TerminalCellPoint stop);

            Assert.AreEqual(0, start.Row);
            Assert.AreEqual(2, start.Column);
            Assert.AreEqual(2, stop.Row);
            Assert.AreEqual(10, stop.Column);
        }

        [TestMethod]
        public void GetStreamLineColumnRange_FirstRow_StartsAtAnchorColumn()
        {
            var start = new TerminalCellPoint(1, 4);
            var stop = new TerminalCellPoint(3, 7);
            TerminalTextSelection.GetStreamLineColumnRange(start, stop, 1, 40, out int colStart, out int colEnd);

            Assert.AreEqual(4, colStart);
            Assert.AreEqual(39, colEnd);
        }

        [TestMethod]
        public void GetStreamLineColumnRange_LastRow_EndsAtStopColumn()
        {
            var start = new TerminalCellPoint(1, 4);
            var stop = new TerminalCellPoint(3, 7);
            TerminalTextSelection.GetStreamLineColumnRange(start, stop, 3, 40, out int colStart, out int colEnd);

            Assert.AreEqual(0, colStart);
            Assert.AreEqual(7, colEnd);
        }

        [TestMethod]
        public void GetStreamLineColumnRange_SingleRow_UsesBothColumns()
        {
            var start = new TerminalCellPoint(2, 3);
            var stop = new TerminalCellPoint(2, 9);
            TerminalTextSelection.GetStreamLineColumnRange(start, stop, 2, 40, out int colStart, out int colEnd);

            Assert.AreEqual(3, colStart);
            Assert.AreEqual(9, colEnd);
        }

        [TestMethod]
        public void GetSelectionPixelBounds_MultiLineStream_SpansFromLeftmostToRightmost()
        {
            var anchor = new TerminalCellPoint(0, 5);
            var end = new TerminalCellPoint(2, 3);
            Rectangle bounds = TerminalTextSelection.GetSelectionPixelBounds(
                anchor,
                end,
                columns: 80,
                cellWidth: 10,
                cellHeight: 16);

            Assert.AreEqual(0, bounds.X);
            Assert.AreEqual(0, bounds.Y);
            Assert.AreEqual(800, bounds.Width);
            Assert.AreEqual(48, bounds.Height);
        }

        [TestMethod]
        public void ExtractTextFromGrid_BackwardSelection_MatchesForwardStream()
        {
            var grid = new TerminalCellGrid(8, 2);
            FillRow(grid, 0, "abcdefgh");
            FillRow(grid, 1, "ijklmnop");

            string forward = TerminalTextSelection.ExtractTextFromGrid(
                grid,
                new TerminalCellPoint(0, 2),
                new TerminalCellPoint(1, 4));
            string backward = TerminalTextSelection.ExtractTextFromGrid(
                grid,
                new TerminalCellPoint(1, 4),
                new TerminalCellPoint(0, 2));

            Assert.AreEqual("cdefgh\nijklm", forward);
            Assert.AreEqual(forward, backward);
        }

        [TestMethod]
        public void ExtractTextFromGrid_SkipsControlCharacters()
        {
            var grid = new TerminalCellGrid(4, 1);
            grid[0, 0] = new TerminalCell { CodePoint = 'a' };
            grid[0, 1] = new TerminalCell { CodePoint = '\x1b' };
            grid[0, 2] = new TerminalCell { CodePoint = 'b' };
            grid[0, 3] = new TerminalCell { CodePoint = 'c' };

            string text = TerminalTextSelection.ExtractTextFromGrid(
                grid,
                new TerminalCellPoint(0, 0),
                new TerminalCellPoint(0, 3));

            Assert.AreEqual("abc", text);
        }

        [TestMethod]
        public void StyleForSelection_DarkForeground_UsesDefaultGrayBackground()
        {
            var cell = new TerminalCell
            {
                CodePoint = 'z',
                Foreground = Color.FromArgb(8, 8, 8),
                Background = Color.Black
            };

            var styled = TerminalTextSelection.StyleForSelection(cell);

            Assert.AreEqual(Color.Black.ToArgb(), styled.Foreground.ToArgb());
            Assert.AreEqual(
                VtNetColorHelper.DefaultForegroundColor.ToArgb(),
                styled.Background.ToArgb());
        }

        private static void FillRow(TerminalCellGrid grid, int row, string text)
        {
            for (int i = 0; i < text.Length && i < grid.Columns; i++)
            {
                grid[row, i] = new TerminalCell
                {
                    CodePoint = text[i],
                    Foreground = VtNetColorHelper.DefaultForegroundColor,
                    Background = VtNetColorHelper.DefaultBackgroundColor
                };
            }
        }
    }
}

