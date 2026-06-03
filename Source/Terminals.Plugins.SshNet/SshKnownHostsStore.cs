// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE-GPLv3.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
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

            string xml;
            try
            {
                xml = File.ReadAllText(this.storePath);
            }
            catch (Exception exception)
            {
                Logging.Error("Unable to read SSH known hosts store.", exception);
                return;
            }

            if (xml.IndexOf("<KnownHosts", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (this.TryLoadDocumentFormat())
                    return;
            }
            else if (xml.IndexOf("ArrayOfSshKnownHostEntry", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (this.TryLoadLegacyArrayFormat())
                    return;
            }
            else if (this.TryLoadDocumentFormat())
                return;

            Logging.Error("Unable to load SSH known hosts store: unrecognized file format.");
        }

        private bool TryLoadDocumentFormat()
        {
            try
            {
                var serializer = new XmlSerializer(typeof(SshKnownHostsDocument));
                using (var stream = File.OpenRead(this.storePath))
                {
                    var document = serializer.Deserialize(stream) as SshKnownHostsDocument;
                    if (document == null || document.Entries == null)
                        return false;

                    this.entries.AddRange(document.Entries);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool TryLoadLegacyArrayFormat()
        {
            try
            {
                var document = new XmlDocument();
                document.Load(this.storePath);
                XmlNodeList nodes = document.GetElementsByTagName("SshKnownHostEntry");
                if (nodes == null || nodes.Count == 0)
                    return false;

                foreach (XmlNode node in nodes)
                {
                    SshKnownHostEntry entry = ReadLegacyEntry(node);
                    if (entry != null)
                        this.entries.Add(entry);
                }

                if (this.entries.Count == 0)
                    return false;

                this.Save();
                Logging.Info("SSH known hosts store migrated from legacy XML format.");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static SshKnownHostEntry ReadLegacyEntry(XmlNode node)
        {
            if (node == null)
                return null;

            string host = GetChildInnerText(node, "Host");
            string hostKeyName = GetChildInnerText(node, "HostKeyName");
            string fingerprint = GetChildInnerText(node, "Fingerprint");
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(hostKeyName) || string.IsNullOrEmpty(fingerprint))
                return null;

            int port;
            if (!int.TryParse(GetChildInnerText(node, "Port"), out port))
                port = 22;

            return new SshKnownHostEntry
            {
                Host = host,
                Port = port,
                HostKeyName = hostKeyName,
                Fingerprint = fingerprint
            };
        }

        private static string GetChildInnerText(XmlNode parent, string localName)
        {
            XmlNode child = parent[localName];
            return child != null ? child.InnerText : null;
        }

        private void Save()
        {
            try
            {
                var document = new SshKnownHostsDocument { Entries = new List<SshKnownHostEntry>(this.entries) };
                var serializer = new XmlSerializer(typeof(SshKnownHostsDocument));
                using (var stream = File.Create(this.storePath))
                {
                    serializer.Serialize(stream, document);
                }
            }
            catch (Exception exception)
            {
                Logging.Error("Unable to save SSH known hosts store.", exception);
            }
        }
    }
}
