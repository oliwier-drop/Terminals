// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Collections.Generic;
using System.Text;

namespace Terminals.Plugins.SshNet
{
    /// <summary>Tracks guarded type-ahead echo and removes the matching server echo later.</summary>
    internal sealed class SshLocalEchoController
    {
        private const int MaxPendingEchoes = 512;
        private const int PromptScanLength = 160;
        private readonly List<PendingEcho> pendingEchoes = new List<PendingEcho>();
        private bool alternateScreenActive;
        private bool passwordEntryActive;

        internal bool HasPendingEcho
        {
            get { return this.pendingEchoes.Count > 0; }
        }

        internal bool IsPasswordEntryActive(string screenText)
        {
            return this.passwordEntryActive || IsUnsafePrompt(screenText);
        }

        internal void RegisterPasswordKeySuppressor()
        {
            this.Enqueue(PendingEcho.ForPasswordKeyEcho());
        }

        internal void NotifyUserInput(string sentText)
        {
            if (string.IsNullOrEmpty(sentText))
                return;

            if (this.passwordEntryActive
                && (sentText == "\r" || sentText == "\n" || sentText == "\r\n"))
            {
                this.passwordEntryActive = false;
                this.ResetPendingEcho();
            }
        }

        internal bool TryCreatePrintableEcho(char keyChar, string screenText, bool cursorVisible, out string localEcho)
        {
            localEcho = null;
            if (!cursorVisible || this.alternateScreenActive || this.IsPasswordEntryActive(screenText))
                return false;

            if (char.IsControl(keyChar) || char.IsSurrogate(keyChar))
                return false;

            localEcho = keyChar.ToString();
            return true;
        }

        internal bool TryCreateBackspaceEcho(string sentText, string screenText, bool cursorVisible, out string localEcho)
        {
            localEcho = null;
            if ((sentText != "\b" && sentText != "\x7f")
                || !cursorVisible
                || this.alternateScreenActive
                || this.IsPasswordEntryActive(screenText))
            {
                return false;
            }

            if (!this.TryGetLastPrintablePending(out _))
                return false;

            localEcho = "\b \b";
            return true;
        }

        internal void CompleteBackspaceUndo(string sentText)
        {
            if (string.IsNullOrEmpty(sentText))
                return;

            if (this.pendingEchoes.Count > 0 && this.pendingEchoes[this.pendingEchoes.Count - 1].IsPrintableCharacter)
                this.pendingEchoes.RemoveAt(this.pendingEchoes.Count - 1);

            this.Enqueue(new PendingEcho(sentText, "\b \b"));
        }

        internal void RegisterPrintableEcho(string expectedEcho)
        {
            if (string.IsNullOrEmpty(expectedEcho))
                return;

            this.Enqueue(new PendingEcho(expectedEcho));
        }

        internal string FilterServerOutput(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            this.TrackTerminalMode(text);
            this.UpdatePasswordEntryState(text);

            if (this.pendingEchoes.Count == 0)
                return text;

            string remaining = text;
            int index = 0;
            while (remaining.Length > 0 && index < this.pendingEchoes.Count)
            {
                PendingEcho pending = this.pendingEchoes[index];
                if (pending.TryConsume(ref remaining))
                {
                    if (pending.IsConsumed)
                        this.pendingEchoes.RemoveAt(index);
                    else
                        index++;
                    continue;
                }

                this.ResetPendingEcho();
                return remaining;
            }

            return remaining;
        }

        internal void ResetPendingEcho()
        {
            this.pendingEchoes.Clear();
        }

        internal void Reset()
        {
            this.pendingEchoes.Clear();
            this.alternateScreenActive = false;
            this.passwordEntryActive = false;
        }

        private void UpdatePasswordEntryState(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (LooksLikePasswordPrompt(text))
                this.passwordEntryActive = true;
        }

        private void Enqueue(PendingEcho pending)
        {
            if (this.pendingEchoes.Count >= MaxPendingEchoes)
                this.pendingEchoes.RemoveAt(0);

            this.pendingEchoes.Add(pending);
        }

        private bool TryGetLastPrintablePending(out PendingEcho pending)
        {
            pending = null;
            if (this.pendingEchoes.Count == 0)
                return false;

            PendingEcho last = this.pendingEchoes[this.pendingEchoes.Count - 1];
            if (!last.IsPrintableCharacter)
                return false;

            pending = last;
            return true;
        }

        private void TrackTerminalMode(string text)
        {
            if (ContainsAlternateScreenMode(text, 'h'))
            {
                this.alternateScreenActive = true;
                this.ResetPendingEcho();
            }

            if (ContainsAlternateScreenMode(text, 'l'))
            {
                this.alternateScreenActive = false;
                this.ResetPendingEcho();
            }

            if (text.IndexOf("\x1b[2J", StringComparison.Ordinal) >= 0
                || text.IndexOf("\x1b[3J", StringComparison.Ordinal) >= 0)
            {
                this.ResetPendingEcho();
            }
        }

        private static bool ContainsAlternateScreenMode(string text, char command)
        {
            return text.IndexOf("\x1b[?1049" + command, StringComparison.Ordinal) >= 0
                || text.IndexOf("\x1b[?1047" + command, StringComparison.Ordinal) >= 0
                || text.IndexOf("\x1b[?47" + command, StringComparison.Ordinal) >= 0;
        }

        private static bool IsUnsafePrompt(string screenText)
        {
            if (string.IsNullOrEmpty(screenText))
                return false;

            string tail = screenText.Length > PromptScanLength
                ? screenText.Substring(screenText.Length - PromptScanLength)
                : screenText;
            int lastNewline = Math.Max(tail.LastIndexOf('\n'), tail.LastIndexOf('\r'));
            if (lastNewline >= 0 && lastNewline < tail.Length - 1)
                tail = tail.Substring(lastNewline + 1);

            tail = StripSimpleAnsi(tail).Trim().ToLowerInvariant();
            if (tail.Length == 0)
                return false;

            return LooksLikePasswordPrompt(tail);
        }

        private static bool LooksLikePasswordPrompt(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            string normalized = StripSimpleAnsi(text).ToLowerInvariant();
            return normalized.IndexOf("password", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("passphrase", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("passwd", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("passwort", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("hasło", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("haslo", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("secret", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("token", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("credential", StringComparison.Ordinal) >= 0
                || ContainsWord(normalized, "pin");
        }

        private static string StripSimpleAnsi(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length;)
            {
                if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '[')
                {
                    int j = i + 2;
                    while (j < text.Length && !char.IsLetter(text[j]))
                        j++;

                    if (j < text.Length)
                        j++;

                    i = j;
                    continue;
                }

                sb.Append(text[i]);
                i++;
            }

            return sb.ToString();
        }

        private static bool ContainsWord(string text, string word)
        {
            int index = text.IndexOf(word, StringComparison.Ordinal);
            while (index >= 0)
            {
                int before = index - 1;
                int after = index + word.Length;
                bool startsAtBoundary = before < 0 || !char.IsLetterOrDigit(text[before]);
                bool endsAtBoundary = after >= text.Length || !char.IsLetterOrDigit(text[after]);
                if (startsAtBoundary && endsAtBoundary)
                    return true;

                index = text.IndexOf(word, index + 1, StringComparison.Ordinal);
            }

            return false;
        }

        private sealed class PendingEcho
        {
            private string primary;
            private string alternate;
            private bool consumeNextPrintable;

            internal PendingEcho(string primary)
                : this(primary, null)
            {
            }

            internal PendingEcho(string primary, string alternate)
            {
                this.primary = primary ?? string.Empty;
                this.alternate = alternate;
            }

            private PendingEcho(bool consumeNextPrintable)
            {
                this.consumeNextPrintable = consumeNextPrintable;
            }

            internal static PendingEcho ForPasswordKeyEcho()
            {
                return new PendingEcho(consumeNextPrintable: true);
            }

            internal bool IsConsumed
            {
                get
                {
                    if (this.consumeNextPrintable)
                        return false;

                    return (this.primary ?? string.Empty).Length == 0;
                }
            }

            internal bool IsPrintableCharacter
            {
                get
                {
                    return this.alternate == null
                        && this.primary.Length == 1
                        && this.primary[0] >= ' '
                        && this.primary[0] != '\x7f';
                }
            }

            internal bool TryConsume(ref string text)
            {
                if (this.consumeNextPrintable)
                {
                    if (text.Length == 0)
                        return true;

                    char codePoint = text[0];
                    if ((codePoint >= 0x20 && codePoint < 0x7f) || codePoint == '\x7f' || codePoint == '\b')
                    {
                        text = text.Substring(1);
                        this.consumeNextPrintable = false;
                    }

                    return true;
                }

                if (this.alternate != null)
                {
                    string alternateCandidate = this.alternate;
                    if (TryConsumeCandidate(ref text, ref alternateCandidate))
                    {
                        this.primary = alternateCandidate;
                        this.alternate = null;
                        return true;
                    }
                }

                return TryConsumeCandidate(ref text, ref this.primary);
            }

            private static bool TryConsumeCandidate(ref string text, ref string candidate)
            {
                if (candidate.Length == 0)
                    return true;

                if (text.Length >= candidate.Length)
                {
                    if (!text.StartsWith(candidate, StringComparison.Ordinal))
                        return false;

                    text = text.Substring(candidate.Length);
                    candidate = string.Empty;
                    return true;
                }

                if (!candidate.StartsWith(text, StringComparison.Ordinal))
                    return false;

                candidate = candidate.Substring(text.Length);
                text = string.Empty;
                return true;
            }
        }
    }
}
