// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Renci.SshNet.Common;
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

        [TestMethod]
        public void CreateTerminalModes_Server_EnablesCanonicalEchoModes()
        {
            var modes = SshNetShellStreamHelper.CreateTerminalModes(SshConnectionProfile.Server);

            Assert.AreEqual(1u, modes[TerminalModes.ECHO]);
            Assert.AreEqual(1u, modes[TerminalModes.ICANON]);
            Assert.AreEqual(1u, modes[TerminalModes.ISIG]);
            Assert.AreEqual(127u, modes[TerminalModes.VERASE]);
        }

        [TestMethod]
        public void CreateTerminalModes_NetworkDevice_UsesMinimalCompatModes()
        {
            var modes = SshNetShellStreamHelper.CreateTerminalModes(SshConnectionProfile.NetworkDevice);

            Assert.AreEqual(1u, modes[TerminalModes.ICRNL]);
            Assert.AreEqual(1u, modes[TerminalModes.ONLCR]);
            Assert.IsFalse(modes.ContainsKey(TerminalModes.ECHO));
            Assert.IsFalse(modes.ContainsKey(TerminalModes.ICANON));
        }
    }
}
