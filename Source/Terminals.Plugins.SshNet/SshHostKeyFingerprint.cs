// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Security.Cryptography;
using System.Text;

namespace Terminals.Plugins.SshNet
{
    internal static class SshHostKeyFingerprint
    {
        internal static string FormatSha256(byte[] fingerprint)
        {
            if (fingerprint == null || fingerprint.Length == 0)
                return string.Empty;

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(fingerprint);
                var builder = new StringBuilder("SHA256:");
                for (int i = 0; i < hash.Length; i++)
                {
                    if (i > 0)
                        builder.Append(':');
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
