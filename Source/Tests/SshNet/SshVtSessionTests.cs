// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System.Drawing;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Plugins.SshNet;
using Terminals.Plugins.SshNet.Rendering;

namespace Tests.SshNet
{
    [TestClass]
    public class SshVtSessionTests
    {
        [TestMethod]
        public void Push_PlainText_WritesCharacters()
        {
            var session = new SshVtSession();
            session.Push("hello\r\nworld");

            TerminalCellGrid grid = BuildGrid(session);
            Assert.AreEqual("hello", RowText(grid, 0, 5));
            Assert.AreEqual("world", RowText(grid, 1, 5));
        }

        [TestMethod]
        public void Push_ClearScreen_ErasesBuffer()
        {
            var session = new SshVtSession();
            session.Push("data\nstale\x1B[2Jprompt");

            string text = session.GetScreenTextForTest();
            Assert.IsTrue(text.Contains("prompt"));
            Assert.IsFalse(text.Contains("stale"));
        }

        [TestMethod]
        public void Push_CursorCommands_OverwriteAtExpectedPosition()
        {
            var session = new SshVtSession();
            session.Push("abc\r\n123\x1B[A\x1B[2GZ");

            TerminalCellGrid grid = BuildGrid(session);
            Assert.AreEqual("aZc", RowText(grid, 0, 3));
            Assert.AreEqual("123", RowText(grid, 1, 3));
        }

        [TestMethod]
        public void Push_OscTitle_DoesNotWriteTitleToScreen()
        {
            var session = new SshVtSession();
            session.Push("\x1B]0;title\x07user@host:$ ");

            string text = session.GetScreenTextForTest();
            Assert.IsFalse(text.Contains("title"));
            Assert.IsTrue(text.Contains("user@host:$"));
        }

        [TestMethod]
        public void Push_AlternateScreen_ReturnsToPrimaryPrompt()
        {
            var session = new SshVtSession();
            session.Push("\x1B[?1049h\x1B[2Jalternate\x1B[?1049l$ ");

            TerminalCellGrid grid = BuildGrid(session);
            Assert.AreEqual("$ ", RowText(grid, 0, 2));
        }

        [TestMethod]
        public void Resize_UpdatesVisibleDimensions()
        {
            var session = new SshVtSession();
            session.Resize(100, 40);

            Assert.AreEqual(100, session.Columns);
            Assert.AreEqual(40, session.Rows);
        }

        [TestMethod]
        public void Push_SgrColor_RendersColoredText()
        {
            var session = new SshVtSession();
            session.Push("\x1B[31mR\x1B[0mN");

            TerminalCellGrid grid = BuildGrid(session);
            AssertCell(grid, 0, 0, 'R', Color.FromArgb(0xCD, 0, 0));
            AssertCell(grid, 0, 1, 'N', Color.FromArgb(0xCD, 0xCD, 0xCD));
        }

        private static TerminalCellGrid BuildGrid(SshVtSession session)
        {
            return TerminalCellGridBuilder.Build(
                session.Controller,
                0,
                session.Rows,
                session.Columns);
        }

        private static void AssertCell(
            TerminalCellGrid grid,
            int row,
            int column,
            char expectedCodePoint,
            Color expectedForeground)
        {
            TerminalCell cell = grid[row, column];
            Assert.AreEqual(expectedCodePoint, cell.CodePoint);
            Assert.AreEqual(expectedForeground.ToArgb(), cell.Foreground.ToArgb());
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
