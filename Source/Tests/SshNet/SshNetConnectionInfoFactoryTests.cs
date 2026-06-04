// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Renci.SshNet;
using Terminals.Common.Configuration;
using Terminals.Configuration;
using Terminals.Data;
using Terminals.Plugins.Putty;
using Terminals.Plugins.SshNet;

namespace Tests.SshNet
{
    [TestClass]
    public class SshNetConnectionInfoFactoryTests
    {
        [TestMethod]
        public void TryCreate_WithCompression_EnablesZlibAlgorithms()
        {
            var options = new SshOptions { EnableCompression = true, SshVersion = SshVersion.SshVersion2 };
            SshNetConnectionSetup setup;
            string error;

            bool created = SshNetConnectionInfoFactory.TryCreate("host", 22, CreateCredentials(), options, null, null, out setup, out error);

            Assert.IsTrue(created, error);
            Assert.IsTrue(setup.EnableCompression);
            Assert.IsTrue(setup.ConnectionInfo.CompressionAlgorithms.ContainsKey("zlib"));
            Assert.IsTrue(setup.ConnectionInfo.CompressionAlgorithms.ContainsKey("zlib@openssh.com"));
        }

        [TestMethod]
        public void TryCreate_MapsSessionFeaturesFromSshOptions()
        {
            var options = new SshOptions
            {
                X11Forwarding = true,
                EnablePagentAuthentication = true,
                EnablePagentForwarding = true,
                Verbose = true,
                SessionName = "lab",
                SshVersion = SshVersion.SshNegotiate
            };

            SshNetConnectionSetup setup;
            string error;
            bool created = SshNetConnectionInfoFactory.TryCreate("host", 22, CreateCredentials(), options, null, null, out setup, out error);

            Assert.IsTrue(created, error);
            Assert.IsTrue(setup.X11Forwarding);
            Assert.IsTrue(setup.EnablePagentAuthentication);
            Assert.IsTrue(setup.EnablePagentForwarding);
            Assert.IsTrue(setup.Verbose);
            Assert.AreEqual("lab", setup.SessionName);
            Assert.AreEqual("host", setup.Host);
            Assert.AreEqual(22, setup.Port);
        }

        [TestMethod]
        public void TryCreate_SshVersion1_ReturnsError()
        {
            var options = new SshOptions { SshVersion = SshVersion.SshVersion1 };
            SshNetConnectionSetup setup;
            string error;

            bool created = SshNetConnectionInfoFactory.TryCreate("host", 22, CreateCredentials(), options, null, null, out setup, out error);

            Assert.IsFalse(created);
            Assert.IsNull(setup);
            Assert.AreEqual(SshNetConnectionInfoFactory.SshVersion1NotSupported, error);
        }

        [TestMethod]
        public void TryCreate_WithPassword_RegistersPasswordAuthentication()
        {
            SshNetConnectionSetup setup;
            string error;
            bool created = SshNetConnectionInfoFactory.TryCreate("host", 22, CreateCredentials(), new SshOptions { AuthMethod = AuthMethod.Password }, null, null, out setup, out error);

            Assert.IsTrue(created, error);
            Assert.IsTrue(setup.ConnectionInfo.AuthenticationMethods.Count >= 1);
            Assert.IsInstanceOfType(
                setup.ConnectionInfo.AuthenticationMethods[setup.ConnectionInfo.AuthenticationMethods.Count - 1],
                typeof(PasswordAuthenticationMethod));
        }

        [TestMethod]
        public void TryCreate_EmptyUserName_ReturnsErrorForPasswordAuth()
        {
            SshNetConnectionSetup setup;
            string error;
            bool created = SshNetConnectionInfoFactory.TryCreate(
                "host",
                22,
                CreateCredentials(userName: string.Empty),
                new SshOptions { AuthMethod = AuthMethod.Password },
                null,
                null,
                out setup,
                out error);

            Assert.IsFalse(created);
            Assert.IsTrue(error.ToLowerInvariant().Contains("user"), error);
        }

        [TestMethod]
        public void TryCreate_WithoutPassword_ReturnsErrorForPasswordAuth()
        {
            SshNetConnectionSetup setup;
            string error;
            bool created = SshNetConnectionInfoFactory.TryCreate("host", 22, CreateCredentials(password: null), new SshOptions { AuthMethod = AuthMethod.Password }, null, null, out setup, out error);

            Assert.IsFalse(created);
            Assert.IsNull(setup);
            Assert.IsTrue(error.IndexOf("password", System.StringComparison.OrdinalIgnoreCase) >= 0, error);
        }

        [TestMethod]
        public void TryCreate_PublicKeyFromKeyFile_RegistersPrivateKeyAuthentication()
        {
            string keyPath = CreateTemporaryRsaKey();
            try
            {
                var options = new SshOptions
                {
                    AuthMethod = AuthMethod.PublicKey,
                    KeyFile = keyPath
                };

                SshNetConnectionSetup setup;
                string error;
                bool created = SshNetConnectionInfoFactory.TryCreate("host", 22, CreateCredentials(password: null), options, null, null, out setup, out error);

                Assert.IsTrue(created, error);
                Assert.AreEqual(1, setup.ConnectionInfo.AuthenticationMethods.Count);
                Assert.IsInstanceOfType(setup.ConnectionInfo.AuthenticationMethods[0], typeof(PrivateKeyAuthenticationMethod));
            }
            finally
            {
                File.Delete(keyPath);
            }
        }

        [TestMethod]
        public void TryCreate_PublicKeyFromKeyTag_RegistersPrivateKeyAuthentication()
        {
            var keys = new KeysSection();
            keys.AddKey("lab", TestKeyMaterial.RsaPrivateKeyPem);

            var options = new SshOptions
            {
                AuthMethod = AuthMethod.PublicKey,
                KeyTag = "lab"
            };

            SshNetConnectionSetup setup;
            string error;
            bool created = SshNetConnectionInfoFactory.TryCreate("host", 22, CreateCredentials(password: null), options, keys, null, out setup, out error);

            Assert.IsTrue(created, error);
            Assert.IsInstanceOfType(setup.ConnectionInfo.AuthenticationMethods[0], typeof(PrivateKeyAuthenticationMethod));
        }

        [TestMethod]
        public void TryCreate_KeyboardInteractive_RegistersKeyboardInteractiveAuthentication()
        {
            var options = new SshOptions { AuthMethod = AuthMethod.KeyboardInteractive };
            SshNetConnectionSetup setup;
            string error;

            bool created = SshNetConnectionInfoFactory.TryCreate("host", 22, CreateCredentials(), options, null, null, out setup, out error);

            Assert.IsTrue(created, error);
            Assert.IsInstanceOfType(setup.ConnectionInfo.AuthenticationMethods[0], typeof(KeyboardInteractiveAuthenticationMethod));
        }

        [TestMethod]
        public void TryCreate_HostAuth_IncludesPasswordAndKeyboardMethods()
        {
            SshNetConnectionSetup setup;
            string error;
            bool created = SshNetConnectionInfoFactory.TryCreate("host", 22, CreateCredentials(), new SshOptions { AuthMethod = AuthMethod.Host }, null, null, out setup, out error);

            Assert.IsTrue(created, error);
            Assert.AreEqual(2, setup.ConnectionInfo.AuthenticationMethods.Count);
            Assert.IsInstanceOfType(setup.ConnectionInfo.AuthenticationMethods[0], typeof(KeyboardInteractiveAuthenticationMethod));
            Assert.IsInstanceOfType(setup.ConnectionInfo.AuthenticationMethods[1], typeof(PasswordAuthenticationMethod));
        }

        [TestMethod]
        public void TryCreate_PublicKeyMissingKeyFile_ReturnsError()
        {
            var options = new SshOptions
            {
                AuthMethod = AuthMethod.PublicKey,
                KeyFile = Path.Combine(Path.GetTempPath(), "missing-terminals-test-key.pem")
            };

            SshNetConnectionSetup setup;
            string error;
            bool created = SshNetConnectionInfoFactory.TryCreate("host", 22, CreateCredentials(password: null), options, null, null, out setup, out error);

            Assert.IsFalse(created);
            Assert.IsNull(setup);
            Assert.IsTrue(error.IndexOf("not found", System.StringComparison.OrdinalIgnoreCase) >= 0, error);
        }

        [TestMethod]
        public void TryCreate_PublicKeyMissingKeyTag_ReturnsError()
        {
            var keys = new KeysSection();
            var options = new SshOptions
            {
                AuthMethod = AuthMethod.PublicKey,
                KeyTag = "missing"
            };

            SshNetConnectionSetup setup;
            string error;
            bool created = SshNetConnectionInfoFactory.TryCreate("host", 22, CreateCredentials(password: null), options, keys, null, out setup, out error);

            Assert.IsFalse(created);
            Assert.IsNull(setup);
            Assert.IsTrue(error.IndexOf("key tag", System.StringComparison.OrdinalIgnoreCase) >= 0, error);
        }

        private static string CreateTemporaryRsaKey()
        {
            string path = Path.Combine(Path.GetTempPath(), "terminals-test-" + Path.GetRandomFileName() + ".pem");
            File.WriteAllText(path, TestKeyMaterial.RsaPrivateKeyPem);
            return path;
        }

        private static IGuardedSecurity CreateCredentials(string password = "password", string userName = "user")
        {
            var credentials = new Mock<IGuardedSecurity>();
            credentials.Setup(c => c.UserName).Returns(userName);
            credentials.Setup(c => c.Password).Returns(password);
            return credentials.Object;
        }
    }
}
