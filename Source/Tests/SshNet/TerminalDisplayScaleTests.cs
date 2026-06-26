// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Plugins.SshNet.Rendering;

namespace Tests.SshNet
{
    [TestClass]
    public class TerminalDisplayScaleTests
    {
        [TestMethod]
        public void ComputePointSize_FullHdViewport_IsLargerThanSmallLaptop()
        {
            float fullHd = TerminalDisplayScale.ComputePointSize(1f, 1900, 1000, 1f);
            float laptop = TerminalDisplayScale.ComputePointSize(1f, 1280, 720, 1f);
            Assert.IsTrue(fullHd > laptop);
        }

        [TestMethod]
        public void ComputePointSize_HighDpi_IncreasesPointSize()
        {
            float normal = TerminalDisplayScale.ComputePointSize(1f, 1600, 900, 1f);
            float highDpi = TerminalDisplayScale.ComputePointSize(2f, 1600, 900, 1f);
            Assert.IsTrue(highDpi > normal);
        }

        [TestMethod]
        public void ComputePointSize_UserZoom_ScalesWithinBounds()
        {
            float baseline = TerminalDisplayScale.ComputePointSize(1f, 1600, 900, 1f);
            float zoomedIn = TerminalDisplayScale.ComputePointSize(1f, 1600, 900, 1.25f);
            float zoomedOut = TerminalDisplayScale.ComputePointSize(1f, 1600, 900, 0.8f);
            Assert.IsTrue(zoomedIn > baseline);
            Assert.IsTrue(zoomedOut < baseline);
            Assert.IsTrue(zoomedIn <= TerminalDisplayScale.MaxPointSize);
            Assert.IsTrue(zoomedOut >= TerminalDisplayScale.MinPointSize);
        }

        [TestMethod]
        public void ComputePointSize_ExtremeInputs_ClampedToMinMax()
        {
            float tiny = TerminalDisplayScale.ComputePointSize(0.1f, 1, 1, 0.1f);
            float huge = TerminalDisplayScale.ComputePointSize(8f, 8000, 6000, 3f);
            Assert.AreEqual(TerminalDisplayScale.MinPointSize, tiny);
            Assert.AreEqual(TerminalDisplayScale.MaxPointSize, huge);
        }
    }
}
