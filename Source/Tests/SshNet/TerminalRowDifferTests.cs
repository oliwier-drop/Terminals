// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Plugins.SshNet.Rendering;

namespace Tests.SshNet
{
    [TestClass]
    public class TerminalRowDifferTests
    {
        [TestMethod]
        public void GetDirtyRows_IdenticalGrids_ReturnsEmpty()
        {
            var grid = new TerminalCellGrid(10, 3);
            grid[0, 0] = new TerminalCell { CodePoint = 'x' };
            var previous = grid.Clone();
            var differ = new TerminalRowDiffer();
            var options = new TerminalRowDiffOptions();

            var dirty = differ.GetDirtyRows(previous, grid, options);
            Assert.AreEqual(0, dirty.Count);
        }

        [TestMethod]
        public void GetDirtyRows_SingleCellChange_ReturnsOneRow()
        {
            var previous = new TerminalCellGrid(10, 2);
            previous[1, 3] = new TerminalCell { CodePoint = 'a' };
            var current = previous.Clone();
            current[1, 3] = new TerminalCell { CodePoint = 'b' };

            var differ = new TerminalRowDiffer();
            var dirty = differ.GetDirtyRows(previous, current, new TerminalRowDiffOptions());
            Assert.AreEqual(1, dirty.Count);
            Assert.AreEqual(1, dirty[0]);
        }

        [TestMethod]
        public void GetDirtyRows_ForceFullRepaint_ReturnsAllRows()
        {
            var grid = new TerminalCellGrid(5, 4);
            var differ = new TerminalRowDiffer();
            var options = new TerminalRowDiffOptions { ForceFullRepaint = true };
            var dirty = differ.GetDirtyRows(null, grid, options);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, ToArray(dirty));
        }

        [TestMethod]
        public void GetDirtyRows_DimensionChange_ReturnsAllCurrentRows()
        {
            var previous = new TerminalCellGrid(5, 2);
            var current = new TerminalCellGrid(6, 3);
            var differ = new TerminalRowDiffer();

            var dirty = differ.GetDirtyRows(previous, current, new TerminalRowDiffOptions());

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, ToArray(dirty));
        }

        [TestMethod]
        public void GetDirtyRows_AtFullRepaintThreshold_ReturnsAllRows()
        {
            var previous = new TerminalCellGrid(5, 5);
            var current = previous.Clone();
            for (int row = 0; row < 4; row++)
                current[row, 0] = new TerminalCell { CodePoint = (char)('a' + row) };

            var differ = new TerminalRowDiffer();
            var dirty = differ.GetDirtyRows(previous, current, new TerminalRowDiffOptions());

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, ToArray(dirty));
        }

        private static int[] ToArray(System.Collections.Generic.IList<int> rows)
        {
            var result = new int[rows.Count];
            rows.CopyTo(result, 0);
            return result;
        }
    }
}
