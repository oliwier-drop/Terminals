// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System.Drawing;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Plugins.SshNet;
using Terminals.Plugins.SshNet.Rendering;

namespace Tests.SshNet
{
    [TestClass]
    public class TerminalCellGridBuilderTests
    {
        [TestMethod]
        public void Build_PlainText_PopulatesFirstRow()
        {
            var session = new SshVtSession();
            session.Push("hello");

            var grid = TerminalCellGridBuilder.Build(
                session.Controller,
                viewTopRow: 0,
                rows: session.Rows,
                columns: session.Columns);

            Assert.IsTrue(grid.Columns >= 5);
            Assert.AreEqual("hello", RowText(grid, 0, 5));
        }

        [TestMethod]
        public void Build_MultiLine_PopulatesTwoRows()
        {
            var session = new SshVtSession();
            session.Push("ab\r\ncd");

            var grid = TerminalCellGridBuilder.Build(
                session.Controller,
                0,
                session.Rows,
                session.Columns);

            Assert.AreEqual('a', grid[0, 0].CodePoint);
            Assert.AreEqual('b', grid[0, 1].CodePoint);
            Assert.AreEqual('c', grid[1, 0].CodePoint);
            Assert.AreEqual('d', grid[1, 1].CodePoint);
        }

        [TestMethod]
        public void Build_SgrColor_SetsForeground()
        {
            var session = new SshVtSession();
            session.Push("\x1B[31mR\x1B[0m");

            var grid = TerminalCellGridBuilder.Build(
                session.Controller,
                0,
                session.Rows,
                session.Columns);

            Color red = grid[0, 0].Foreground;
            Assert.AreEqual('R', grid[0, 0].CodePoint);
            Assert.AreEqual(Color.FromArgb(0xCD, 0, 0).ToArgb(), red.ToArgb());
        }

        [TestMethod]
        public void Build_Sgr256Color_SetsForeground()
        {
            var session = new SshVtSession();
            session.Push("\x1B[38;5;208mO\x1B[0m");

            var grid = TerminalCellGridBuilder.Build(
                session.Controller,
                0,
                session.Rows,
                session.Columns);

            Assert.AreEqual('O', grid[0, 0].CodePoint);
            Assert.AreNotEqual(
                VtNetColorHelper.DefaultForegroundColor.ToArgb(),
                grid[0, 0].Foreground.ToArgb());
        }

        [TestMethod]
        public void Build_SgrTrueColor_SetsForeground()
        {
            var session = new SshVtSession();
            session.Push("\x1B[38;2;255;128;0mT\x1B[0m");

            var grid = TerminalCellGridBuilder.Build(
                session.Controller,
                0,
                session.Rows,
                session.Columns);

            Assert.AreEqual('T', grid[0, 0].CodePoint);
            Color orange = grid[0, 0].Foreground;
            Assert.IsTrue(orange.R >= 200);
            Assert.IsTrue(orange.G >= 100);
            Assert.IsTrue(orange.B <= 80);
        }

        [TestMethod]
        public void Build_SgrBoldBlue_SetsForeground()
        {
            var session = new SshVtSession();
            session.Push("\x1B[1;34mB\x1B[0m");

            var grid = TerminalCellGridBuilder.Build(
                session.Controller,
                0,
                session.Rows,
                session.Columns);

            Assert.AreEqual('B', grid[0, 0].CodePoint);
            Assert.IsTrue(grid[0, 0].Bold);
            Assert.AreNotEqual(
                VtNetColorHelper.DefaultForegroundColor.ToArgb(),
                grid[0, 0].Foreground.ToArgb());
        }

        [TestMethod]
        public void Build_SgrReset_RestoresDefaultForeground()
        {
            var session = new SshVtSession();
            session.Push("\x1B[31mR\x1B[0mN");

            var grid = TerminalCellGridBuilder.Build(
                session.Controller,
                0,
                session.Rows,
                session.Columns);

            AssertCell(grid, 0, 0, 'R', Color.FromArgb(0xCD, 0, 0), null);
            AssertCell(grid, 0, 1, 'N', Color.FromArgb(0xCD, 0xCD, 0xCD), null);
        }

        [TestMethod]
        public void Build_Tab_ReplacesWithSpace()
        {
            var session = new SshVtSession();
            session.Push("a\tb");

            var grid = TerminalCellGridBuilder.Build(
                session.Controller,
                0,
                session.Rows,
                session.Columns);

            Assert.AreEqual('a', grid[0, 0].CodePoint);
            Assert.AreEqual(' ', grid[0, 1].CodePoint);
            Assert.AreEqual('b', grid[0, 8].CodePoint);
        }

        [TestMethod]
        public void Build_TextLongerThanColumns_TruncatesToGridWidth()
        {
            var session = new SshVtSession();
            session.Resize(4, 2);
            session.Push("abcdef");

            var grid = TerminalCellGridBuilder.Build(
                session.Controller,
                0,
                rows: 2,
                columns: 4);

            Assert.AreEqual(4, grid.Columns);
            Assert.AreEqual("abcd", RowText(grid, 0, 4));
        }

        [TestMethod]
        public void Build_Resize_MatchesSessionDimensions()
        {
            var session = new SshVtSession();
            session.Resize(60, 20);
            session.Push("x");

            var grid = TerminalCellGridBuilder.Build(
                session.Controller,
                0,
                session.Rows,
                session.Columns);

            Assert.AreEqual(60, grid.Columns);
            Assert.AreEqual(20, grid.Rows);
        }

        [TestMethod]
        public void GetRowHash_ChangesWhenCellChanges()
        {
            var grid = new TerminalCellGrid(10, 2);
            ulong hash0 = grid.GetRowHash(0);
            grid[0, 0] = new TerminalCell
            {
                CodePoint = 'Z',
                Foreground = VtNetColorHelper.DefaultForegroundColor,
                Background = VtNetColorHelper.DefaultBackgroundColor
            };
            ulong hash1 = grid.GetRowHash(0);
            Assert.AreNotEqual(hash0, hash1);
        }

        private static void AssertCell(
            TerminalCellGrid grid,
            int row,
            int column,
            char expectedCodePoint,
            Color? expectedForeground,
            Color? expectedBackground)
        {
            TerminalCell cell = grid[row, column];
            Assert.AreEqual(expectedCodePoint, cell.CodePoint);
            if (expectedForeground.HasValue)
                Assert.AreEqual(expectedForeground.Value.ToArgb(), cell.Foreground.ToArgb());
            if (expectedBackground.HasValue)
                Assert.AreEqual(expectedBackground.Value.ToArgb(), cell.Background.ToArgb());
        }

        private static string RowText(TerminalCellGrid grid, int row, int length)
        {
            var text = new StringBuilder(length);
            for (int column = 0; column < length; column++)
                text.Append(grid[row, column].CodePoint);
            return text.ToString();
        }
    }
}
