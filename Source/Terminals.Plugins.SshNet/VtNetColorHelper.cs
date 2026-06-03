using System;
using System.Drawing;

namespace Terminals.Plugins.SshNet
{
    internal static class VtNetColorHelper
    {
        private static readonly Color DefaultForeground = Color.FromArgb(0xC0, 0xC0, 0xC0);
        private static readonly Color DefaultBackground = Color.Black;

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

            string trimmed = value.Trim();
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
