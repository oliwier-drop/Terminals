// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Plugins.SshNet.Rendering;

namespace Tests.SshNet
{
    [TestClass]
    public class TerminalGlyphAtlasTests
    {
        [TestMethod]
        public void Atlas_ContainsAsciiLetters_WithPositiveCellSize()
        {
            using (var metrics = new TerminalFontMetrics(10f, 1f))
            using (var atlas = new TerminalGlyphAtlas(metrics))
            {
                Assert.IsTrue(atlas.CellWidth > 0);
                Assert.IsTrue(atlas.CellHeight > 0);
                Assert.IsTrue(atlas.TryGetGlyphRect('A', GlyphStyle.Regular, out var rectA));
                Assert.IsTrue(rectA.Width > 0 && rectA.Height > 0);
                Assert.IsTrue(RectContainsVisiblePixels(atlas.AtlasBitmap, rectA));
                Assert.IsTrue(atlas.TryGetGlyphRect('█', GlyphStyle.Bold, out var rectBlock));
                Assert.IsTrue(rectBlock.Width > 0);
                Assert.IsTrue(RectContainsVisiblePixels(atlas.AtlasBitmap, rectBlock));
            }
        }

        [TestMethod]
        public void Atlas_LazyGlyph_AllocatesBeyondAscii()
        {
            using (var metrics = new TerminalFontMetrics(10f, 1f))
            using (var atlas = new TerminalGlyphAtlas(metrics))
            {
                Assert.IsTrue(atlas.TryGetGlyphRect('ą', GlyphStyle.Regular, out var rect));
                Assert.IsTrue(rect.Width > 0);
                Assert.IsTrue(atlas.TryGetGlyphRect('ą', GlyphStyle.Regular, out var rectAgain));
                Assert.AreEqual(rect, rectAgain);
            }
        }

        [TestMethod]
        public void Atlas_Styles_UseSeparateGlyphRects()
        {
            using (var metrics = new TerminalFontMetrics(10f, 1f))
            using (var atlas = new TerminalGlyphAtlas(metrics))
            {
                Assert.IsTrue(atlas.TryGetGlyphRect('A', GlyphStyle.Regular, out var regular));
                Assert.IsTrue(atlas.TryGetGlyphRect('A', GlyphStyle.Bold, out var bold));
                Assert.IsTrue(atlas.TryGetGlyphRect('A', GlyphStyle.Italic, out var italic));

                Assert.AreNotEqual(regular, bold);
                Assert.AreNotEqual(regular, italic);
                Assert.AreNotEqual(bold, italic);
            }
        }

        [TestMethod]
        public void Atlas_ManyDynamicGlyphs_CachesAllocatedRects()
        {
            using (var metrics = new TerminalFontMetrics(10f, 1f))
            using (var atlas = new TerminalGlyphAtlas(metrics))
            {
                for (int i = 0; i < 64; i++)
                {
                    char codePoint = (char)(0x0400 + i);
                    Assert.IsTrue(atlas.TryGetGlyphRect(codePoint, GlyphStyle.Regular, out var rect));
                    Assert.IsTrue(rect.Width > 0);
                    Assert.IsTrue(atlas.TryGetGlyphRect(codePoint, GlyphStyle.Regular, out var rectAgain));
                    Assert.AreEqual(rect, rectAgain);
                }
            }
        }

        private static bool RectContainsVisiblePixels(Bitmap bitmap, Rectangle rect)
        {
            for (int y = rect.Top; y < rect.Bottom; y++)
            {
                for (int x = rect.Left; x < rect.Right; x++)
                {
                    if (bitmap.GetPixel(x, y).A > 0)
                        return true;
                }
            }

            return false;
        }
    }
}
