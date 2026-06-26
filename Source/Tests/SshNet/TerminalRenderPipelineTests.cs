// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System.Collections.Generic;
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Plugins.SshNet;
using Terminals.Plugins.SshNet.Rendering;

namespace Tests.SshNet
{
    [TestClass]
    public class TerminalRenderPipelineTests
    {
        [TestMethod]
        public void ChunkRequiresFullRepaint_SgrColor_ReturnsFalse()
        {
            Assert.IsFalse(TerminalRenderPipeline.ChunkRequiresFullRepaint("\x1B[31mtext\x1B[0m"));
        }

        [TestMethod]
        public void ChunkRequiresFullRepaint_AlternateScreen_ReturnsTrue()
        {
            Assert.IsTrue(TerminalRenderPipeline.ChunkRequiresFullRepaint("\x1B[?1049h"));
        }

        [TestMethod]
        public void ChunkRequiresFullRepaint_ClearScreen_ReturnsTrue()
        {
            Assert.IsTrue(TerminalRenderPipeline.ChunkRequiresFullRepaint("\x1B[2J"));
        }

        [TestMethod]
        public void ChunkRequiresFullRepaint_AlternateScreen1047_ReturnsTrue()
        {
            Assert.IsTrue(TerminalRenderPipeline.ChunkRequiresFullRepaint("\x1B[?1047h"));
        }

        [TestMethod]
        public void ChunkRequiresFullRepaint_AlternateScreen47_ReturnsTrue()
        {
            Assert.IsTrue(TerminalRenderPipeline.ChunkRequiresFullRepaint("\x1B[?47h"));
        }

        [TestMethod]
        public void ChunkRequiresFullRepaint_EraseScrollback_ReturnsTrue()
        {
            Assert.IsTrue(TerminalRenderPipeline.ChunkRequiresFullRepaint("\x1B[3J"));
        }

        [TestMethod]
        public void UpdateFrame_LimitedBudget_PaintsAllDirtyRowsWithoutDeferral()
        {
            using (var pipeline = new TerminalRenderPipeline())
            using (var bitmap = new Bitmap(200, 128))
            {
                pipeline.UpdateDisplayScale(10f);
                var session = CreateSession("a\r\nb\r\nc\r\nd", 10, 4);
                pipeline.UpdateFrame(bitmap, session.Controller, 0, bitmap.Width, bitmap.Height, new TerminalRowDiffOptions());

                session.Push("\x1B[1;1H1\x1B[2;1H2\x1B[3;1H3\x1B[4;1H4");
                var deferred = new List<int>();
                IList<int> painted = pipeline.UpdateFrame(
                    bitmap,
                    session.Controller,
                    0,
                    bitmap.Width,
                    bitmap.Height,
                    new TerminalRowDiffOptions(),
                    maxRowsToPaint: 1,
                    deferredRows: deferred);

                Assert.AreEqual(0, deferred.Count);
                Assert.AreEqual(4, painted.Count);
            }
        }

        [TestMethod]
        public void RebuildFullFrame_AfterBulkLineOutput_PaintsEveryVisibleRow()
        {
            using (var pipeline = new TerminalRenderPipeline())
            {
                pipeline.UpdateDisplayScale(10f);
                int visibleRows = 5;
                int rowHeight = pipeline.CellHeight;
                using (var bitmap = new Bitmap(400, visibleRows * rowHeight))
                {
                    var session = new SshVtSession();
                    session.Resize(24, visibleRows);
                    for (int i = 0; i < 19; i++)
                        session.Push("line-" + i + "\r\n");
                    session.Push("line-19");

                    int viewTop = session.Controller.ViewPort.TopRow;
                    pipeline.RebuildFullFrame(bitmap, session.Controller, viewTop, bitmap.Width);

                    for (int row = 0; row < visibleRows; row++)
                        Assert.IsTrue(RowHasVisibleInk(bitmap, pipeline, row), "Row " + row + " should contain painted text.");
                }
            }
        }

        [TestMethod]
        public void PaintSelection_StreamRange_PaintsExpectedCellBackgrounds()
        {
            using (var pipeline = new TerminalRenderPipeline())
            using (var bitmap = new Bitmap(200, 64))
            {
                pipeline.UpdateDisplayScale(10f);
                var grid = new TerminalCellGrid(10, 3);
                for (int col = 0; col < 10; col++)
                {
                    grid[0, col] = MakeCell((char)('a' + col), Color.Cyan, Color.Black);
                    grid[1, col] = MakeCell((char)('0' + col), Color.White, Color.Black);
                    grid[2, col] = MakeCell((char)('A' + col), Color.Yellow, Color.Black);
                }

                pipeline.PaintSelection(
                    bitmap,
                    grid,
                    new TerminalCellPoint(0, 2),
                    new TerminalCellPoint(2, 5),
                    Point.Empty);

                AssertCellBackgroundNear(bitmap, pipeline, 0, 1, Color.Black);
                AssertCellBackgroundNear(bitmap, pipeline, 0, 2, Color.Cyan);
                AssertCellBackgroundNear(bitmap, pipeline, 0, 9, Color.Cyan);
                AssertCellBackgroundNear(bitmap, pipeline, 1, 0, Color.White);
                AssertCellBackgroundNear(bitmap, pipeline, 1, 9, Color.White);
                AssertCellBackgroundNear(bitmap, pipeline, 2, 0, Color.Yellow);
                AssertCellBackgroundNear(bitmap, pipeline, 2, 5, Color.Yellow);
                AssertCellBackgroundNear(bitmap, pipeline, 2, 6, Color.Black);
            }
        }

        [TestMethod]
        public void PaintSelection_ColoredText_FillsWithOriginalForegroundColor()
        {
            using (var pipeline = new TerminalRenderPipeline())
            using (var bitmap = new Bitmap(32, 24))
            {
                pipeline.UpdateDisplayScale(10f);
                var grid = new TerminalCellGrid(1, 1);
                grid[0, 0] = MakeCell('X', Color.Yellow, Color.Black);

                pipeline.PaintSelection(bitmap, grid, new TerminalCellPoint(0, 0), new TerminalCellPoint(0, 0), Point.Empty);

                int yellowPixels = CountPixelsNearColor(
                    bitmap,
                    new Rectangle(0, 0, pipeline.CellWidth, pipeline.CellHeight),
                    Color.Yellow,
                    tolerance: 48);
                Assert.IsTrue(yellowPixels > (pipeline.CellWidth * pipeline.CellHeight) / 3);
            }
        }

        [TestMethod]
        public void UpdateFrame_FirstFrameReturnsAllRows_SecondIdenticalFrameReturnsEmpty()
        {
            using (var pipeline = new TerminalRenderPipeline())
            using (var bitmap = new Bitmap(200, 96))
            {
                pipeline.UpdateDisplayScale(10f);
                var session = CreateSession("abc\r\ndef\r\nghi", 10, 3);

                IList<int> firstDirty = pipeline.UpdateFrame(
                    bitmap,
                    session.Controller,
                    0,
                    bitmap.Width,
                    bitmap.Height,
                    new TerminalRowDiffOptions());
                CollectionAssert.AreEqual(new[] { 0, 1, 2 }, ToArray(firstDirty));

                IList<int> secondDirty = pipeline.UpdateFrame(
                    bitmap,
                    session.Controller,
                    0,
                    bitmap.Width,
                    bitmap.Height,
                    new TerminalRowDiffOptions());
                Assert.AreEqual(0, secondDirty.Count);
            }
        }

        [TestMethod]
        public void UpdateFrame_SingleLineChangeReturnsOnlyChangedRow()
        {
            using (var pipeline = new TerminalRenderPipeline())
            using (var bitmap = new Bitmap(200, 96))
            {
                pipeline.UpdateDisplayScale(10f);
                var session = CreateSession("abc\r\ndef\r\nghi", 10, 3);
                pipeline.UpdateFrame(bitmap, session.Controller, 0, bitmap.Width, bitmap.Height, new TerminalRowDiffOptions());

                session.Push("\x1B[2;1HZ");
                IList<int> dirty = pipeline.UpdateFrame(
                    bitmap,
                    session.Controller,
                    0,
                    bitmap.Width,
                    bitmap.Height,
                    new TerminalRowDiffOptions());

                CollectionAssert.AreEqual(new[] { 1 }, ToArray(dirty));
            }
        }

        [TestMethod]
        public void RebuildFullFrame_ThenIdenticalUpdateFrameReturnsEmpty()
        {
            using (var pipeline = new TerminalRenderPipeline())
            using (var bitmap = new Bitmap(200, 96))
            {
                pipeline.UpdateDisplayScale(10f);
                var session = CreateSession("abc\r\ndef\r\nghi", 10, 3);

                pipeline.RebuildFullFrame(bitmap, session.Controller, 0, bitmap.Width);
                IList<int> dirty = pipeline.UpdateFrame(
                    bitmap,
                    session.Controller,
                    0,
                    bitmap.Width,
                    bitmap.Height,
                    new TerminalRowDiffOptions());

                Assert.AreEqual(0, dirty.Count);
            }
        }

        [TestMethod]
        public void TryScrollFrame_WithPreviousGridAndSmallDelta_ReturnsTrue()
        {
            using (var pipeline = new TerminalRenderPipeline())
            using (var bitmap = new Bitmap(200, 96))
            {
                pipeline.UpdateDisplayScale(10f);
                var session = CreateSession("one\r\ntwo\r\nthree", 10, 3);
                pipeline.UpdateFrame(bitmap, session.Controller, 0, bitmap.Width, bitmap.Height, new TerminalRowDiffOptions());

                Assert.IsTrue(pipeline.TryScrollFrame(bitmap, session.Controller, 0, 1, bitmap.Width));
            }
        }

        [TestMethod]
        public void TryScrollFrame_DeltaAtLeastVisibleRows_ReturnsFalse()
        {
            using (var pipeline = new TerminalRenderPipeline())
            using (var bitmap = new Bitmap(200, 96))
            {
                pipeline.UpdateDisplayScale(10f);
                var session = CreateSession("one\r\ntwo\r\nthree", 10, 3);
                pipeline.UpdateFrame(bitmap, session.Controller, 0, bitmap.Width, bitmap.Height, new TerminalRowDiffOptions());

                Assert.IsFalse(pipeline.TryScrollFrame(bitmap, session.Controller, 0, 3, bitmap.Width));
            }
        }

        [TestMethod]
        public void TryScrollFrame_WithoutPreviousGrid_ReturnsFalse()
        {
            using (var pipeline = new TerminalRenderPipeline())
            using (var bitmap = new Bitmap(200, 96))
            {
                pipeline.UpdateDisplayScale(10f);
                var session = CreateSession("one\r\ntwo\r\nthree", 10, 3);

                Assert.IsFalse(pipeline.TryScrollFrame(bitmap, session.Controller, 0, 1, bitmap.Width));
            }
        }

        [TestMethod]
        public void TryScrollFrame_ZeroDelta_ReturnsTrue()
        {
            using (var pipeline = new TerminalRenderPipeline())
            using (var bitmap = new Bitmap(200, 96))
            {
                pipeline.UpdateDisplayScale(10f);
                var session = CreateSession("one\r\ntwo\r\nthree", 10, 3);

                Assert.IsTrue(pipeline.TryScrollFrame(bitmap, session.Controller, 0, 0, bitmap.Width));
            }
        }

        private static TerminalCell MakeCell(char codePoint, Color fore, Color back)
        {
            return new TerminalCell
            {
                CodePoint = codePoint,
                Foreground = fore,
                Background = back
            };
        }

        private static SshVtSession CreateSession(string text, int columns, int rows)
        {
            var session = new SshVtSession();
            session.Resize(columns, rows);
            session.Push(text);
            return session;
        }

        private static int[] ToArray(IList<int> rows)
        {
            var result = new int[rows.Count];
            rows.CopyTo(result, 0);
            return result;
        }

        private static void AssertCellBackgroundNear(
            Bitmap bitmap,
            TerminalRenderPipeline pipeline,
            int row,
            int column,
            Color expected)
        {
            Color sample = bitmap.GetPixel(
                (column * pipeline.CellWidth) + 1,
                (row * pipeline.CellHeight) + 1);
            AssertColorNear(expected, sample, 48);
        }

        private static int CountPixelsNearColor(Bitmap bitmap, Rectangle area, Color expected, int tolerance)
        {
            int count = 0;
            for (int y = area.Top; y < area.Bottom; y++)
            {
                for (int x = area.Left; x < area.Right; x++)
                {
                    if (ColorNear(expected, bitmap.GetPixel(x, y), tolerance))
                        count++;
                }
            }

            return count;
        }

        private static void AssertColorNear(Color expected, Color actual, int tolerance)
        {
            Assert.IsTrue(
                ColorNear(expected, actual, tolerance),
                string.Format(
                    "Expected near {0}, but was {1}.",
                    expected,
                    actual));
        }

        private static bool ColorNear(Color expected, Color actual, int tolerance)
        {
            return System.Math.Abs(expected.R - actual.R) <= tolerance
                && System.Math.Abs(expected.G - actual.G) <= tolerance
                && System.Math.Abs(expected.B - actual.B) <= tolerance;
        }

        private static bool RowHasVisibleInk(Bitmap bitmap, TerminalRenderPipeline pipeline, int row)
        {
            int y = (row * pipeline.CellHeight) + (pipeline.CellHeight / 2);
            for (int x = 0; x < bitmap.Width; x += pipeline.CellWidth)
            {
                Color sample = bitmap.GetPixel(x, y);
                if (sample.R > 24 || sample.G > 24 || sample.B > 24)
                    return true;
            }

            return false;
        }
    }
}
