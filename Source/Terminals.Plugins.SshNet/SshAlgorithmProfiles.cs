// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Collections.Generic;
using Renci.SshNet;
namespace Terminals.Plugins.SshNet
{
    /// <summary>
    /// Applies SSH algorithm offer lists for Server vs Network device profiles.
    /// </summary>
    internal static class SshAlgorithmProfiles
    {
        private static readonly string[] NetworkDeviceHostKeys =
        {
            "rsa-sha2-256",
            "rsa-sha2-512",
            "ssh-rsa"
        };

        private static readonly string[] NetworkDeviceKeyExchange =
        {
            "diffie-hellman-group16-sha512",
            "diffie-hellman-group14-sha256",
            "diffie-hellman-group14-sha1"
        };

        private static readonly string[] NetworkDeviceCiphers =
        {
            "aes128-ctr",
            "aes192-ctr",
            "aes256-ctr"
        };

        private static readonly string[] NetworkDeviceMacs =
        {
            "hmac-sha2-256-etm@openssh.com",
            "hmac-sha2-512-etm@openssh.com",
            "hmac-sha1-etm@openssh.com",
            "hmac-sha2-256",
            "hmac-sha2-512",
            "hmac-sha1"
        };

        internal static void Apply(ConnectionInfo connectionInfo, SshConnectionProfile profile)
        {
            if (connectionInfo == null)
                throw new ArgumentNullException(nameof(connectionInfo));

            if (profile == SshConnectionProfile.Server)
                return;

            var defaults = CreateDefaultsTemplate();
            FilterDictionary(connectionInfo.HostKeyAlgorithms, defaults.HostKeyAlgorithms, NetworkDeviceHostKeys);
            FilterDictionary(connectionInfo.KeyExchangeAlgorithms, defaults.KeyExchangeAlgorithms, NetworkDeviceKeyExchange);
            FilterDictionary(connectionInfo.Encryptions, defaults.Encryptions, NetworkDeviceCiphers);
            FilterDictionary(connectionInfo.HmacAlgorithms, defaults.HmacAlgorithms, NetworkDeviceMacs);

            connectionInfo.CompressionAlgorithms.Clear();
            connectionInfo.CompressionAlgorithms.Add("none", null);
        }

        private static ConnectionInfo CreateDefaultsTemplate()
        {
            return new ConnectionInfo(
                "template",
                22,
                "user",
                new PasswordAuthenticationMethod("user", "password"));
        }

        private static void FilterDictionary<TValue>(
            IDictionary<string, TValue> target,
            IDictionary<string, TValue> defaults,
            string[] allowedInOrder)
        {
            target.Clear();
            foreach (string name in allowedInOrder)
            {
                TValue value;
                if (defaults.TryGetValue(name, out value))
                    target.Add(name, value);
            }
        }
    }
}
