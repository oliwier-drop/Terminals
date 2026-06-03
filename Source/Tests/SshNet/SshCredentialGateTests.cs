// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE-GPLv3.md and FORK-AUTHORED.md at the repository root.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Terminals.Common.Configuration;
using Terminals.Data;
using Terminals.Plugins.Putty;
using Terminals.Plugins.SshNet;

namespace Tests.SshNet
{
    [TestClass]
    public class SshCredentialGateTests
    {
        [TestMethod]
        public void TryPrepareSessionCredentials_PasswordAuthMissingPassword_RequiresPrompt()
        {
            var resolved = CreateResolved("dropadmin", null);
            SshSessionCredentials session;
            string error;
            bool ok = SshCredentialGate.TryPrepareSessionCredentials(
                resolved,
                new SshOptions { AuthMethod = AuthMethod.Password },
                null,
                "192.168.1.1",
                null,
                null,
                out session,
                out error);

            Assert.IsFalse(ok);
            Assert.IsNull(session);
            Assert.IsTrue(error.ToLowerInvariant().Contains("password"), error);
        }

        [TestMethod]
        public void TryPrepareSessionCredentials_EmptyUserName_ReturnsError()
        {
            var resolved = CreateResolved(string.Empty, "secret");
            SshSessionCredentials session;
            string error;
            bool ok = SshCredentialGate.TryPrepareSessionCredentials(
                resolved,
                new SshOptions { AuthMethod = AuthMethod.Password },
                null,
                "host",
                null,
                null,
                out session,
                out error);

            Assert.IsFalse(ok);
            Assert.IsTrue(error.ToLowerInvariant().Contains("user"), error);
        }

        [TestMethod]
        public void TryPrepareSessionCredentials_CompleteCredentials_Succeeds()
        {
            var resolved = CreateResolved("dropadmin", "secret");
            SshSessionCredentials session;
            string error;
            bool ok = SshCredentialGate.TryPrepareSessionCredentials(
                resolved,
                new SshOptions { AuthMethod = AuthMethod.Password },
                null,
                "host",
                null,
                null,
                out session,
                out error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual("dropadmin", session.UserName);
            Assert.AreEqual("secret", session.Password);
        }

        private static IGuardedSecurity CreateResolved(string userName, string password)
        {
            var mock = new Mock<IGuardedSecurity>();
            mock.Setup(c => c.UserName).Returns(userName);
            mock.Setup(c => c.Password).Returns(password);
            mock.Setup(c => c.Domain).Returns(string.Empty);
            return mock.Object;
        }
    }
}
