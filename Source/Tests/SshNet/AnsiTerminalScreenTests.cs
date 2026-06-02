using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using Terminals.Plugins.SshNet;

namespace Tests.SshNet
{
    [TestClass]
    public class AnsiTerminalScreenTests
    {
        [TestMethod]
        public void Feed_PlainText_WritesCharacters()
        {
            var screen = new AnsiTerminalScreen();
            screen.Feed("hello\nworld");

            Assert.AreEqual("hello\nworld\n", screen.RenderPlainTextForTest());
        }

        [TestMethod]
        public void Feed_ClearScreen_ErasesBuffer()
        {
            var screen = new AnsiTerminalScreen();
            screen.Feed("data\nstale\x1B[2Jprompt");

            Assert.AreEqual("prompt\n", screen.RenderPlainTextForTest());
        }

        [TestMethod]
        public void Feed_CursorCommands_OverwriteAtExpectedPosition()
        {
            var screen = new AnsiTerminalScreen();
            screen.Feed("abc\n123\x1B[A\x1B[2GZ");

            Assert.AreEqual("aZc\n123\n", screen.RenderPlainTextForTest());
        }

        [TestMethod]
        public void Feed_DecPrivateModeAndOsc_DoesNotBreakPlainPrompt()
        {
            var screen = new AnsiTerminalScreen();
            screen.Feed("\x1B]0;title\x07user@host:\x1B[?1049h\x1B[2J\x1B[?1049l$ ");

            Assert.IsTrue(screen.RenderPlainTextForTest().Contains("user@host:"));
            Assert.IsTrue(screen.RenderPlainTextForTest().Contains("$"));
        }

        [TestMethod]
        public void Feed_TerminalWidth_WrapsAtColumnBoundary()
        {
            var screen = new AnsiTerminalScreen { TerminalWidth = 5 };
            screen.Feed("123456");

            Assert.AreEqual("12345\n6\n", screen.RenderPlainTextForTest());
        }

        [TestMethod]
        public void Feed_TrailingBlankLines_AreTrimmedBelowCursor()
        {
            var screen = new AnsiTerminalScreen();
            screen.Feed("prompt\n\n\n");

            Assert.AreEqual("prompt\n", screen.RenderPlainTextForTest());
        }

        [TestMethod]
        public void Feed_SgrColor_AppliesAndResetsColors()
        {
            var screen = new AnsiTerminalScreen();
            screen.Feed("\x1B[31mR\x1B[0mN");

            Assert.AreEqual("RN\n", screen.RenderPlainTextForTest());
            AssertCellStyle(screen, 0, 0, Color.IndianRed, Color.Black, false);
            AssertCellStyle(screen, 0, 1, Color.Gainsboro, Color.Black, false);
        }

        private static void AssertCellStyle(AnsiTerminalScreen screen, int row, int column, Color foreColor, Color backColor, bool bold)
        {
            AnsiStyle style;
            Assert.IsTrue(screen.TryGetCellStyleForTest(row, column, out style));
            Assert.AreEqual(foreColor, style.ForeColor);
            Assert.AreEqual(backColor, style.BackColor);
            Assert.AreEqual(bold, style.Bold);
        }
    }
}
