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
        public void FormatSha256_ProducesStablePrefix()
        {
            string formatted = SshHostKeyFingerprint.FormatSha256(new byte[] { 0, 1, 2 });
            Assert.IsTrue(formatted.StartsWith("SHA256:"));
        }
    }
}
