// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Plugins.SshNet.Rendering;

namespace Tests.SshNet
{
    [TestClass]
    public class TerminalRowSpanBuilderTests
    {
        [TestMethod]
        public void BuildBackgroundRuns_MergesAdjacentSameColor()
        {
            var grid = new TerminalCellGrid(4, 1);
            grid[0, 0] = MakeCell('a', Color.White, Color.Black);
            grid[0, 1] = MakeCell('b', Color.White, Color.Black);
            grid[0, 2] = MakeCell('c', Color.White, Color.Red);
            grid[0, 3] = MakeCell('d', Color.White, Color.Red);

            var runs = TerminalRowSpanBuilder.BuildBackgroundRuns(grid, 0);
            Assert.AreEqual(2, runs.Count);
            Assert.AreEqual(0, runs[0].StartColumn);
            Assert.AreEqual(2, runs[0].Length);
            Assert.AreEqual(Color.Black.ToArgb(), runs[0].Background.ToArgb());
            Assert.AreEqual(2, runs[1].StartColumn);
            Assert.AreEqual(2, runs[1].Length);
            Assert.AreEqual(Color.Red.ToArgb(), runs[1].Background.ToArgb());
        }

        [TestMethod]
        public void BuildTextSpans_GroupsMatchingStyle()
        {
            var grid = new TerminalCellGrid(4, 1);
            grid[0, 0] = MakeCell('a', Color.Cyan, Color.Black);
            grid[0, 1] = MakeCell('b', Color.Cyan, Color.Black);
            grid[0, 2] = MakeCell('c', Color.Yellow, Color.Black);
            grid[0, 3] = MakeCell(' ', Color.White, Color.Black);

            var spans = TerminalRowSpanBuilder.BuildTextSpans(grid, 0);
            Assert.AreEqual(2, spans.Count);
            Assert.AreEqual("ab", spans[0].Text);
            Assert.AreEqual(0, spans[0].StartColumn);
            Assert.AreEqual("c", spans[1].Text);
            Assert.AreEqual(2, spans[1].StartColumn);
        }

        private static TerminalCell MakeCell(char codePoint, Color fore, Color back)
        {
            return new TerminalCell
            {
                CodePoint = codePoint,
                Foreground = fore,
                Background = back
            };
        }
    }
}
