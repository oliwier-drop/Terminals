// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Terminals.Plugins.SshNet
{
    internal static class VtNetColorHelper
    {
        internal static readonly Color DefaultForegroundColor = Color.FromArgb(0xC0, 0xC0, 0xC0);
        internal static readonly Color DefaultBackgroundColor = Color.Black;
        private static readonly Color DefaultForeground = DefaultForegroundColor;
        private static readonly Color DefaultBackground = DefaultBackgroundColor;
        private static readonly Dictionary<string, Color> Cache = new Dictionary<string, Color>(StringComparer.Ordinal);

        internal static Color ParseForeground(string hexOrName)
        {
            return ParseColor(hexOrName, DefaultForeground);
        }

        internal static Color ParseBackground(string hexOrName)
        {
            return ParseColor(hexOrName, DefaultBackground);
        }

        private static Color ParseColor(string value, Color fallback)
        {
            if (string.IsNullOrEmpty(value))
                return fallback;

            string key = value.Trim();
            Color cached;
            if (Cache.TryGetValue(key, out cached))
                return cached;

            Color parsed = ParseColorCore(key, fallback);
            Cache[key] = parsed;
            return parsed;
        }

        private static Color ParseColorCore(string trimmed, Color fallback)
        {
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                try
                {
                    string hex = trimmed.Substring(1);
                    if (hex.Length == 6)
                    {
                        int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                        int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                        int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                        return Color.FromArgb(r, g, b);
                    }
                }
                catch
                {
                    return fallback;
                }
            }

            return fallback;
        }
    }
}
