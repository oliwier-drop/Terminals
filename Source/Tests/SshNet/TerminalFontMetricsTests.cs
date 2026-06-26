// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Plugins.SshNet.Rendering;

namespace Tests.SshNet
{
    [TestClass]
    public class TerminalFontMetricsTests
    {
        [TestMethod]
        public void Constructor_SkiaMetrics_ProducesPositiveCellSize()
        {
            using (var metrics = new TerminalFontMetrics(10f))
            {
                Assert.AreEqual(10f, metrics.FontSize);
                Assert.IsTrue(metrics.CellWidth >= 4);
                Assert.IsTrue(metrics.CellHeight >= 8);
            }
        }

        [TestMethod]
        public void Constructor_LargerPointSize_IncreasesCellDimensions()
        {
            using (var small = new TerminalFontMetrics(9f))
            using (var large = new TerminalFontMetrics(14f))
            {
                Assert.IsTrue(large.CellWidth >= small.CellWidth);
                Assert.IsTrue(large.CellHeight >= small.CellHeight);
            }
        }

        [TestMethod]
        public void ComputeTextBaseline_IsWithinCellHeight()
        {
            using (var metrics = new TerminalFontMetrics(12f))
            {
                float baseline = TerminalFontMetrics.ComputeTextBaseline(metrics.FontSize, metrics.CellHeight);
                Assert.IsTrue(baseline > 0f);
                Assert.IsTrue(baseline < metrics.CellHeight);
            }
        }
    }
}
