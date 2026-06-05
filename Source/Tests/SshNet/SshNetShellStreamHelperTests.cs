// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Plugins.SshNet;

namespace Tests.SshNet
{
    [TestClass]
    public class SshNetShellStreamHelperTests
    {
        [TestMethod]
        public void GetTerminalType_Server_ReturnsXterm256Color()
        {
            Assert.AreEqual("xterm-256color", SshNetShellStreamHelper.GetTerminalType(SshConnectionProfile.Server));
        }

        [TestMethod]
        public void GetTerminalType_NetworkDevice_ReturnsVt100()
        {
            Assert.AreEqual("vt100", SshNetShellStreamHelper.GetTerminalType(SshConnectionProfile.NetworkDevice));
        }

        [TestMethod]
        public void GetInitialShellWaitTimeout_NetworkDevice_IsZero()
        {
            Assert.AreEqual(TimeSpan.Zero, SshNetShellStreamHelper.GetInitialShellWaitTimeout(SshConnectionProfile.NetworkDevice));
        }

        [TestMethod]
        public void GetInitialShellWaitTimeout_Server_IsFiveSeconds()
        {
            Assert.AreEqual(SshNetShellStreamHelper.InitialShellWaitTimeout,
                SshNetShellStreamHelper.GetInitialShellWaitTimeout(SshConnectionProfile.Server));
        }
    }
}
