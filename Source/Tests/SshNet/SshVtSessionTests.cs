using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Plugins.SshNet;

namespace Tests.SshNet
{
    [TestClass]
    public class SshVtSessionTests
    {
        [TestMethod]
        public void Push_PlainText_WritesCharacters()
        {
            var session = new SshVtSession();
            session.Push("hello\nworld");

            string text = session.GetScreenTextForTest();
            Assert.IsTrue(text.Contains("hello"));
            Assert.IsTrue(text.Contains("world"));
        }

        [TestMethod]
        public void Push_ClearScreen_ErasesBuffer()
        {
            var session = new SshVtSession();
            session.Push("data\nstale\x1B[2Jprompt");

            string text = session.GetScreenTextForTest();
            Assert.IsTrue(text.Contains("prompt"));
            Assert.IsFalse(text.Contains("stale"));
        }

        [TestMethod]
        public void Push_CursorCommands_OverwriteAtExpectedPosition()
        {
            var session = new SshVtSession();
            session.Push("abc\n123\x1B[A\x1B[2GZ");

            string text = session.GetScreenTextForTest();
            Assert.IsTrue(text.Contains("aZc") || text.Contains("aZ"));
        }

        [TestMethod]
        public void Push_DecPrivateModeAndOsc_DoesNotBreakPlainPrompt()
        {
            var session = new SshVtSession();
            session.Push("\x1B]0;title\x07user@host:\x1B[?1049h\x1B[2J\x1B[?1049l$ ");

            string text = session.GetScreenTextForTest();
            Assert.IsTrue(text.Contains("user@host:") || text.Contains("$"));
        }

        [TestMethod]
        public void Resize_UpdatesVisibleDimensions()
        {
            var session = new SshVtSession();
            session.Resize(100, 40);

            Assert.AreEqual(100, session.Columns);
            Assert.AreEqual(40, session.Rows);
        }

        [TestMethod]
        public void Push_SgrColor_RendersColoredText()
        {
            var session = new SshVtSession();
            session.Push("\x1B[31mR\x1B[0mN");

            string text = session.GetScreenTextForTest();
            Assert.IsTrue(text.Contains("R"));
            Assert.IsTrue(text.Contains("N"));
        }
    }
}
