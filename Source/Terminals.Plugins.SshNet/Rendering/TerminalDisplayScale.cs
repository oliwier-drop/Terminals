// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;

namespace Terminals.Plugins.SshNet.Rendering
{
    /// <summary>
    /// Maps monitor DPI and terminal viewport size to a font point size for Skia rendering.
    /// </summary>
    internal static class TerminalDisplayScale
    {
        internal const float BasePointSize = 10f;
        internal const float MinPointSize = 8f;
        internal const float MaxPointSize = 22f;
        internal const float MinUserZoom = 0.75f;
        internal const float MaxUserZoom = 1.5f;

        private const float ReferenceWidth = 1600f;
        private const float ReferenceHeight = 900f;
        private const float MinViewportFactor = 0.82f;
        private const float MaxViewportFactor = 1.28f;

        internal static float ComputePointSize(float dpiScale, int viewportWidth, int viewportHeight, float userZoom)
        {
            if (dpiScale < 0.5f)
                dpiScale = 0.5f;
            if (dpiScale > 4f)
                dpiScale = 4f;
            if (viewportWidth < 1)
                viewportWidth = 1;
            if (viewportHeight < 1)
                viewportHeight = 1;
            if (userZoom < MinUserZoom)
                userZoom = MinUserZoom;
            if (userZoom > MaxUserZoom)
                userZoom = MaxUserZoom;

            float widthFactor = viewportWidth / ReferenceWidth;
            float heightFactor = viewportHeight / ReferenceHeight;
            float viewportFactor = (float)Math.Sqrt(widthFactor * heightFactor);
            viewportFactor = Clamp(viewportFactor, MinViewportFactor, MaxViewportFactor);

            float pointSize = BasePointSize * dpiScale * viewportFactor * userZoom;
            return Clamp(pointSize, MinPointSize, MaxPointSize);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
