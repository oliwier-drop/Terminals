using System;
using System.Text;
using VtNetCore.VirtualTerminal;
using VtNetCore.XTermParser;

namespace Terminals.Plugins.SshNet
{
    /// <summary>Wraps VtNetCore parser and virtual terminal buffer for one SSH session.</summary>
    internal sealed class SshVtSession
    {
        private readonly VirtualTerminalController controller;
        private readonly DataConsumer consumer;

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

        internal void Resize(int columns, int rows)
        {
            if (columns < 1)
                columns = 80;
            if (rows < 1)
                rows = 24;

            this.controller.ResizeView(columns, rows);
        }

        internal void Push(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            this.consumer.Push(Encoding.UTF8.GetBytes(text));
        }

        internal void Push(byte[] data, int offset, int count)
        {
            if (data == null || count <= 0)
                return;

            if (offset == 0 && count == data.Length)
            {
                this.consumer.Push(data);
                return;
            }

            var slice = new byte[count];
            Buffer.BlockCopy(data, offset, slice, 0, count);
            this.consumer.Push(slice);
        }

        internal bool ConsumeChangedFlag()
        {
            if (!this.controller.Changed)
                return false;

            this.controller.ClearChanges();
            return true;
        }

        internal string GetScreenTextForTest()
        {
            return this.controller.GetScreenText();
        }
    }
}
