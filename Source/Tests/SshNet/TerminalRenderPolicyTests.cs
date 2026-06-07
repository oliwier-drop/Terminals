// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Plugins.SshNet.Rendering;

namespace Tests.SshNet
{
    [TestClass]
    public class TerminalRenderPolicyTests
    {
        [TestMethod]
        public void ShouldUseDirectTextRender_BlackOnWhite_ReturnsTrue()
        {
            var cell = new TerminalCell
            {
                Foreground = Color.Black,
                Background = Color.White
            };

            Assert.IsTrue(TerminalRenderPolicy.ShouldUseDirectTextRender(cell));
        }

        [TestMethod]
        public void ShouldUseDirectTextRender_GrayOnBlack_ReturnsFalse()
        {
            var cell = TerminalCell.Empty;

            Assert.IsFalse(TerminalRenderPolicy.ShouldUseDirectTextRender(cell));
        }

        [TestMethod]
        public void ShouldUseDirectTextRender_YellowOnBlack_ReturnsFalse()
        {
            var cell = new TerminalCell
            {
                Foreground = Color.Yellow,
                Background = Color.Black
            };

            Assert.IsFalse(TerminalRenderPolicy.ShouldUseDirectTextRender(cell));
        }

        [TestMethod]
        public void ShouldUseDirectTextRender_WhiteOnBlack_ReturnsFalse()
        {
            var cell = new TerminalCell
            {
                Foreground = Color.White,
                Background = Color.Black
            };

            Assert.IsFalse(TerminalRenderPolicy.ShouldUseDirectTextRender(cell));
        }
    }
}
