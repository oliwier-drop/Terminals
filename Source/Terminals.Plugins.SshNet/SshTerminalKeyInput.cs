// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE-GPLv3.md and FORK-AUTHORED.md at the repository root.
using System.Text;
using System.Windows.Forms;
using VtNetCore.VirtualTerminal;

namespace Terminals.Plugins.SshNet
{
    internal static class SshTerminalKeyInput
    {
        internal static bool TryGetSequence(
            VirtualTerminalController controller,
            Keys keyCode,
            bool control,
            bool shift,
            out byte[] sequence)
        {
            sequence = null;
            if (controller == null)
                return false;

            string keyName = MapKeyName(keyCode);
            if (keyName == null)
                return false;

            sequence = controller.GetKeySequence(keyName, control, shift);
            return sequence != null && sequence.Length > 0;
        }

        internal static bool TryGetLetterSequence(
            VirtualTerminalController controller,
            char keyChar,
            bool control,
            bool shift,
            out byte[] sequence)
        {
            sequence = null;
            if (controller == null || keyChar == '\0')
                return false;

            if (keyChar >= 'a' && keyChar <= 'z')
                keyChar = char.ToUpperInvariant(keyChar);
            else if (keyChar < 'A' || keyChar > 'Z')
                return false;

            sequence = controller.GetKeySequence(keyChar.ToString(), control, shift);
            return sequence != null && sequence.Length > 0;
        }

        internal static bool TryGetModifierKeySequence(
            VirtualTerminalController controller,
            Keys keyCode,
            bool control,
            bool shift,
            bool alt,
            out byte[] sequence)
        {
            sequence = null;
            if (controller == null || (!control && !alt))
                return false;

            string keyName = MapLetterOrDigitKeyName(keyCode);
            if (keyName == null)
                return false;

            sequence = controller.GetKeySequence(keyName, control, shift);
            return sequence != null && sequence.Length > 0;
        }

        internal static bool TrySendFromKeyEvent(
            VirtualTerminalController controller,
            Keys keyCode,
            bool control,
            bool shift,
            bool alt,
            out string toSend)
        {
            toSend = null;
            byte[] sequence;
            if (TryGetSequence(controller, keyCode, control, shift, out sequence)
                || TryGetModifierKeySequence(controller, keyCode, control, shift, alt, out sequence))
            {
                toSend = BytesToSendString(sequence);
                return !string.IsNullOrEmpty(toSend);
            }

            return false;
        }

        internal static string BytesToSendString(byte[] sequence)
        {
            if (sequence == null || sequence.Length == 0)
                return null;

            return Encoding.UTF8.GetString(sequence);
        }

        private static string MapLetterOrDigitKeyName(Keys keyCode)
        {
            if (keyCode >= Keys.A && keyCode <= Keys.Z)
                return ((char)('A' + (keyCode - Keys.A))).ToString();
            if (keyCode >= Keys.D0 && keyCode <= Keys.D9)
                return ((char)('0' + (keyCode - Keys.D0))).ToString();
            if (keyCode >= Keys.NumPad0 && keyCode <= Keys.NumPad9)
                return ((char)('0' + (keyCode - Keys.NumPad0))).ToString();
            return null;
        }

        private static string MapKeyName(Keys keyCode)
        {
            switch (keyCode)
            {
                case Keys.Back: return "Back";
                case Keys.Tab: return "Tab";
                case Keys.Return: return "Enter";
                case Keys.Escape: return "Escape";
                case Keys.Left: return "Left";
                case Keys.Right: return "Right";
                case Keys.Up: return "Up";
                case Keys.Down: return "Down";
                case Keys.Home: return "Home";
                case Keys.End: return "End";
                case Keys.Insert: return "Insert";
                case Keys.Delete: return "Delete";
                case Keys.PageUp: return "PageUp";
                case Keys.PageDown: return "PageDown";
                case Keys.F1: return "F1";
                case Keys.F2: return "F2";
                case Keys.F3: return "F3";
                case Keys.F4: return "F4";
                case Keys.F5: return "F5";
                case Keys.F6: return "F6";
                case Keys.F7: return "F7";
                case Keys.F8: return "F8";
                case Keys.F9: return "F9";
                case Keys.F10: return "F10";
                case Keys.F11: return "F11";
                case Keys.F12: return "F12";
                default: return null;
            }
        }
    }
}
