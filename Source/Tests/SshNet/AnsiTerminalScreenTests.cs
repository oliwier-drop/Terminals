using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Drawing;
using System.Reflection;
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
            object line = GetListItem(screen, "lines", row);
            object cell = GetListItem(line, "cells", column);
            object style = GetFieldValue(cell, "Style");

            Assert.AreEqual(foreColor, GetFieldValue(style, "ForeColor"));
            Assert.AreEqual(backColor, GetFieldValue(style, "BackColor"));
            Assert.AreEqual(bold, GetFieldValue(style, "Bold"));
        }

        private static object GetListItem(object instance, string fieldName, int index)
        {
            var list = (IList)GetFieldValue(instance, fieldName);
            return list[index];
        }

        private static object GetFieldValue(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Field not found: " + fieldName);
            return field.GetValue(instance);
        }
    }
}
