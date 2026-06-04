// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Plugins.SshNet;

namespace Tests.SshNet
{
    [TestClass]
    public class SshKnownHostsStoreTests
    {
        [TestMethod]
        public void AddOrUpdate_ThenFind_ReturnsMatchingEntry()
        {
            string path = Path.Combine(Path.GetTempPath(), "terminals-knownhosts-" + Guid.NewGuid() + ".xml");
            try
            {
                var store = new SshKnownHostsStore(path);
                byte[] fingerprint = { 1, 2, 3, 4 };

                store.AddOrUpdate("server", 22, "ssh-rsa", fingerprint);

                SshKnownHostEntry entry = store.Find("server", 22, "ssh-rsa");
                Assert.IsNotNull(entry);
                Assert.IsTrue(entry.Matches("server", 22, "ssh-rsa", fingerprint));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [TestMethod]
        public void AddOrUpdate_SameHostPortAndKey_ReplacesFingerprint()
        {
            string path = Path.Combine(Path.GetTempPath(), "terminals-knownhosts-" + Guid.NewGuid() + ".xml");
            try
            {
                var store = new SshKnownHostsStore(path);
                byte[] oldFingerprint = { 1, 2, 3, 4 };
                byte[] newFingerprint = { 5, 6, 7, 8 };

                store.AddOrUpdate("server", 22, "ssh-rsa", oldFingerprint);
                store.AddOrUpdate("server", 22, "ssh-rsa", newFingerprint);

                SshKnownHostEntry entry = store.Find("server", 22, "ssh-rsa");
                Assert.IsNotNull(entry);
                Assert.IsFalse(entry.Matches("server", 22, "ssh-rsa", oldFingerprint));
                Assert.IsTrue(entry.Matches("server", 22, "ssh-rsa", newFingerprint));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [TestMethod]
        public void Find_DifferentPortOrHostKeyName_DoesNotMatchEntry()
        {
            string path = Path.Combine(Path.GetTempPath(), "terminals-knownhosts-" + Guid.NewGuid() + ".xml");
            try
            {
                var store = new SshKnownHostsStore(path);
                byte[] fingerprint = { 1, 2, 3, 4 };

                store.AddOrUpdate("server", 22, "ssh-rsa", fingerprint);

                Assert.IsNull(store.Find("server", 2222, "ssh-rsa"));
                Assert.IsNull(store.Find("server", 22, "ssh-ed25519"));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [TestMethod]
        public void SaveLoad_RoundTrip_PersistsEntriesWithDocumentFormat()
        {
            string path = Path.Combine(Path.GetTempPath(), "terminals-knownhosts-" + Guid.NewGuid() + ".xml");
            try
            {
                byte[] fingerprint = { 9, 8, 7, 6 };
                var store = new SshKnownHostsStore(path);
                store.AddOrUpdate("host.example", 2222, "ssh-ed25519", fingerprint);

                var reloaded = new SshKnownHostsStore(path);
                SshKnownHostEntry entry = reloaded.Find("host.example", 2222, "ssh-ed25519");
                Assert.IsNotNull(entry);
                Assert.IsTrue(entry.Matches("host.example", 2222, "ssh-ed25519", fingerprint));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [TestMethod]
        public void Load_LegacyArrayFormat_MigratesToDocumentFormat()
        {
            string path = Path.Combine(Path.GetTempPath(), "terminals-knownhosts-" + Guid.NewGuid() + ".xml");
            try
            {
                byte[] fingerprint = { 4, 5, 6 };
                string legacyXml =
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<ArrayOfSshKnownHostEntry xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">" +
                    "<SshKnownHostEntry><Host>legacy.host</Host><Port>22</Port><HostKeyName>ssh-rsa</HostKeyName><Fingerprint>" +
                    Convert.ToBase64String(fingerprint) +
                    "</Fingerprint></SshKnownHostEntry></ArrayOfSshKnownHostEntry>";
                File.WriteAllText(path, legacyXml);

                var store = new SshKnownHostsStore(path);
                SshKnownHostEntry entry = store.Find("legacy.host", 22, "ssh-rsa");
                Assert.IsNotNull(entry);
                Assert.IsTrue(entry.Matches("legacy.host", 22, "ssh-rsa", fingerprint));

                string migrated = File.ReadAllText(path);
                Assert.IsTrue(migrated.Contains("<KnownHosts"));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [TestMethod]
        public void Load_EmptyFile_DoesNotThrowAndReturnsEmptyStore()
        {
            string path = Path.Combine(Path.GetTempPath(), "terminals-knownhosts-" + Guid.NewGuid() + ".xml");
            try
            {
                File.WriteAllText(path, string.Empty);

                var store = new SshKnownHostsStore(path);

                Assert.IsNull(store.Find("server", 22, "ssh-rsa"));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [TestMethod]
        public void Load_CorruptDocument_DoesNotThrowAndReturnsEmptyStore()
        {
            string path = Path.Combine(Path.GetTempPath(), "terminals-knownhosts-" + Guid.NewGuid() + ".xml");
            try
            {
                File.WriteAllText(path, "<KnownHosts><Entry>");

                var store = new SshKnownHostsStore(path);

                Assert.IsNull(store.Find("server", 22, "ssh-rsa"));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [TestMethod]
        public void FormatSha256_ProducesStablePrefix()
        {
            string formatted = SshHostKeyFingerprint.FormatSha256(new byte[] { 0, 1, 2 });
            Assert.IsTrue(formatted.StartsWith("SHA256:"));
        }
    }
}
