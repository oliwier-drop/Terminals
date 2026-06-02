using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Terminals.Plugins.SshNet
{
    internal sealed class SshKnownHostsStore
    {
        private readonly string storePath;
        private readonly List<SshKnownHostEntry> entries = new List<SshKnownHostEntry>();
        private readonly object sync = new object();

        internal SshKnownHostsStore(string storePath)
        {
            this.storePath = storePath;
            this.Load();
        }

        internal static SshKnownHostsStore CreateDefault()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Terminals");
            Directory.CreateDirectory(folder);
            return new SshKnownHostsStore(Path.Combine(folder, "SshKnownHosts.xml"));
        }

        internal SshKnownHostEntry Find(string host, int port, string hostKeyName)
        {
            lock (this.sync)
            {
                return this.entries.FirstOrDefault(
                    e => string.Equals(e.Host, host, StringComparison.OrdinalIgnoreCase)
                        && e.Port == port
                        && string.Equals(e.HostKeyName, hostKeyName, StringComparison.OrdinalIgnoreCase));
            }
        }

        internal void AddOrUpdate(string host, int port, string hostKeyName, byte[] fingerprint)
        {
            if (fingerprint == null || fingerprint.Length == 0)
                return;

            lock (this.sync)
            {
                SshKnownHostEntry existing = this.Find(host, port, hostKeyName);
                string encoded = Convert.ToBase64String(fingerprint);
                if (existing != null)
                    existing.Fingerprint = encoded;
                else
                {
                    this.entries.Add(new SshKnownHostEntry
                    {
                        Host = host,
                        Port = port,
                        HostKeyName = hostKeyName,
                        Fingerprint = encoded
                    });
                }

                this.Save();
            }
        }

        private void Load()
        {
            if (!File.Exists(this.storePath))
                return;

            try
            {
                var serializer = new XmlSerializer(typeof(List<SshKnownHostEntry>));
                using (var stream = File.OpenRead(this.storePath))
                {
                    var loaded = serializer.Deserialize(stream) as List<SshKnownHostEntry>;
                    if (loaded != null)
                        this.entries.AddRange(loaded);
                }
            }
            catch (Exception exception)
            {
                Logging.Error("Unable to load SSH known hosts store.", exception);
            }
        }

        private void Save()
        {
            try
            {
                var serializer = new XmlSerializer(typeof(List<SshKnownHostEntry>));
                using (var stream = File.Create(this.storePath))
                {
                    serializer.Serialize(stream, this.entries);
                }
            }
            catch (Exception exception)
            {
                Logging.Error("Unable to save SSH known hosts store.", exception);
            }
        }
    }
}
