using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Renci.SshNet;
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

            bool created = SshNetConnectionInfoFactory.TryCreate("host", 22, CreateCredentials(), options, out setup, out error);

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
            bool created = SshNetConnectionInfoFactory.TryCreate("host", 22, CreateCredentials(), options, out setup, out error);

            Assert.IsTrue(created, error);
            Assert.IsTrue(setup.X11Forwarding);
            Assert.IsTrue(setup.EnablePagentAuthentication);
            Assert.IsTrue(setup.EnablePagentForwarding);
            Assert.IsTrue(setup.Verbose);
            Assert.AreEqual("lab", setup.SessionName);
        }

        [TestMethod]
        public void TryCreate_SshVersion1_ReturnsError()
        {
            var options = new SshOptions { SshVersion = SshVersion.SshVersion1 };
            SshNetConnectionSetup setup;
            string error;

            bool created = SshNetConnectionInfoFactory.TryCreate("host", 22, CreateCredentials(), options, out setup, out error);

            Assert.IsFalse(created);
            Assert.IsNull(setup);
            Assert.AreEqual(SshNetConnectionInfoFactory.SshVersion1NotSupported, error);
        }

        [TestMethod]
        public void TryCreate_WithPassword_RegistersPasswordAuthentication()
        {
            SshNetConnectionSetup setup;
            string error;
            bool created = SshNetConnectionInfoFactory.TryCreate("host", 22, CreateCredentials(), new SshOptions(), out setup, out error);

            Assert.IsTrue(created, error);
            Assert.AreEqual(1, setup.ConnectionInfo.AuthenticationMethods.Count);
            Assert.IsInstanceOfType(setup.ConnectionInfo.AuthenticationMethods[0], typeof(PasswordAuthenticationMethod));
            Assert.AreEqual("host", setup.ConnectionInfo.Host);
            Assert.AreEqual(22, setup.ConnectionInfo.Port);
            Assert.AreEqual("user", setup.ConnectionInfo.Username);
        }

        [TestMethod]
        public void TryCreate_WithoutPassword_RegistersNoneAuthentication()
        {
            SshNetConnectionSetup setup;
            string error;
            bool created = SshNetConnectionInfoFactory.TryCreate("host", 22, CreateCredentials(password: null), new SshOptions(), out setup, out error);

            Assert.IsTrue(created, error);
            Assert.AreEqual(1, setup.ConnectionInfo.AuthenticationMethods.Count);
            Assert.IsInstanceOfType(setup.ConnectionInfo.AuthenticationMethods[0], typeof(NoneAuthenticationMethod));
        }

        private static IGuardedSecurity CreateCredentials(string password = "password")
        {
            var credentials = new Mock<IGuardedSecurity>();
            credentials.Setup(c => c.UserName).Returns("user");
            credentials.Setup(c => c.Password).Returns(password);
            return credentials.Object;
        }
    }
}
