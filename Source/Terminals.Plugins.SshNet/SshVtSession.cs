// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Reflection;
using System.Text;
using VtNetCore.VirtualTerminal;
using VtNetCore.VirtualTerminal.Enums;
using VtNetCore.VirtualTerminal.Model;
using VtNetCore.XTermParser;

namespace Terminals.Plugins.SshNet
{
    /// <summary>Wraps VtNetCore parser and virtual terminal buffer for one SSH session.</summary>
    internal sealed class SshVtSession
    {
        private static readonly FieldInfo ActiveBufferField = typeof(VirtualTerminalController).GetField(
            "<ActiveBuffer>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo AlternativeBufferField = typeof(VirtualTerminalController).GetField(
            "alternativeBuffer",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly string[] AlternateEnterSequences =
        {
            "\x1b[?1049h",
            "\x1b[?1047h",
            "\x1b[?47h",
        };

        private static readonly string[] AlternateLeaveSequences =
        {
            "\x1b[?1049l",
            "\x1b[?1047l",
            "\x1b[?47l",
        };

        private readonly VirtualTerminalController controller;
        private readonly DataConsumer consumer;
        private bool alternateScreenActive;

        internal SshVtSession()
        {
            this.controller = new VirtualTerminalController();
            this.controller.MaximumHistoryLines = 5000;
            this.controller.ResizeView(80, 24);
            this.consumer = new DataConsumer(this.controller);
        }

        internal VirtualTerminalController Controller
        {
            get { return this.controller; }
        }

        internal int Columns
        {
            get { return this.controller.VisibleColumns; }
        }

        internal int Rows
        {
            get { return this.controller.VisibleRows; }
        }

        internal bool IsAlternateScreenActive
        {
            get { return this.alternateScreenActive; }
        }

        internal void Resize(int columns, int rows)
        {
            if (columns < 1)
                columns = 80;
            if (rows < 1)
                rows = 24;

            this.controller.ResizeView(columns, rows);
            this.RefreshAlternateScreenFlag();
        }

        internal void Push(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            while (text.Length > 0)
            {
                int enterAt = FindEarliestSequence(text, AlternateEnterSequences, out int enterLength);
                int leaveAt = FindEarliestSequence(text, AlternateLeaveSequences, out int leaveLength);

                int nextAt;
                int sequenceLength;
                bool isEnter;
                if (enterAt < 0 && leaveAt < 0)
                {
                    this.PushBytes(Encoding.UTF8.GetBytes(text));
                    return;
                }

                if (enterAt >= 0 && (leaveAt < 0 || enterAt <= leaveAt))
                {
                    nextAt = enterAt;
                    sequenceLength = enterLength;
                    isEnter = true;
                }
                else
                {
                    nextAt = leaveAt;
                    sequenceLength = leaveLength;
                    isEnter = false;
                }

                if (nextAt > 0)
                    this.PushBytes(Encoding.UTF8.GetBytes(text.Substring(0, nextAt)));

                this.PushBytes(Encoding.UTF8.GetBytes(text.Substring(nextAt, sequenceLength)));
                if (isEnter)
                    this.SyncAlternateEnter();
                else
                    this.SyncAlternateLeave();

                text = text.Substring(nextAt + sequenceLength);
            }
        }

        internal void Push(byte[] data, int offset, int count)
        {
            if (data == null || count <= 0)
                return;

            if (offset == 0 && count == data.Length)
            {
                this.Push(Encoding.UTF8.GetString(data));
                return;
            }

            var slice = new byte[count];
            Buffer.BlockCopy(data, offset, slice, 0, count);
            this.Push(Encoding.UTF8.GetString(slice));
        }

        private void PushBytes(byte[] data)
        {
            this.consumer.Push(data);
            this.RefreshAlternateScreenFlag();
        }

        private static int FindEarliestSequence(string text, string[] sequences, out int sequenceLength)
        {
            sequenceLength = 0;
            int earliest = -1;
            for (int i = 0; i < sequences.Length; i++)
            {
                string sequence = sequences[i];
                int index = text.IndexOf(sequence, StringComparison.Ordinal);
                if (index < 0 || (earliest >= 0 && index >= earliest))
                    continue;

                earliest = index;
                sequenceLength = sequence.Length;
            }

            return earliest;
        }

        private void SyncAlternateEnter()
        {
            this.controller.EnableAlternateBuffer();
            this.ClearAlternateBufferStorage();
            this.controller.SetCursorPosition(1, 1);
            this.RefreshAlternateScreenFlag();
        }

        private void SyncAlternateLeave()
        {
            this.controller.EnableNormalBuffer();
            this.RefreshAlternateScreenFlag();
        }

        private void ClearAlternateBufferStorage()
        {
            if (AlternativeBufferField == null)
                return;

            var lines = AlternativeBufferField.GetValue(this.controller) as TerminalLines;
            lines?.Clear();
        }

        private void RefreshAlternateScreenFlag()
        {
            if (ActiveBufferField == null)
                return;

            object value = ActiveBufferField.GetValue(this.controller);
            this.alternateScreenActive = value is EActiveBuffer buffer && buffer == EActiveBuffer.Alternative;
        }

        internal bool ConsumeChangedFlag()
        {
            if (!this.controller.Changed)
                return false;

            this.controller.ClearChanges();
            return true;
        }

        internal string GetScreenText()
        {
            return this.controller.GetScreenText();
        }

        internal string GetScreenTextForTest()
        {
            return this.GetScreenText();
        }
    }
}
