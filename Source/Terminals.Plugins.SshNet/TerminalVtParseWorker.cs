// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Terminals.Plugins.SshNet.Rendering;

namespace Terminals.Plugins.SshNet
{
    /// <summary>Drains pending ANSI chunks and parses VT off the UI thread.</summary>
    internal sealed class TerminalVtParseWorker : IDisposable
    {
        private readonly SshVtSession session;
        private readonly Control invokeTarget;
        private readonly Func<int> getCharBudget;
        private readonly Func<List<string>> dequeueChunks;
        private readonly Func<bool> hasPendingChunks;
        private readonly Action<bool> requestRender;

        private readonly AutoResetEvent workSignal = new AutoResetEvent(false);
        private readonly Thread workerThread;
        private volatile bool stopRequested;
        private volatile bool disposed;

        internal TerminalVtParseWorker(
            SshVtSession session,
            Control invokeTarget,
            Func<int> getCharBudget,
            Func<List<string>> dequeueChunks,
            Func<bool> hasPendingChunks,
            Action<bool> requestRender)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.invokeTarget = invokeTarget ?? throw new ArgumentNullException(nameof(invokeTarget));
            this.getCharBudget = getCharBudget ?? throw new ArgumentNullException(nameof(getCharBudget));
            this.dequeueChunks = dequeueChunks ?? throw new ArgumentNullException(nameof(dequeueChunks));
            this.hasPendingChunks = hasPendingChunks ?? throw new ArgumentNullException(nameof(hasPendingChunks));
            this.requestRender = requestRender ?? throw new ArgumentNullException(nameof(requestRender));

            this.workerThread = new Thread(this.WorkerLoop)
            {
                IsBackground = true,
                Name = "SshTerminalVtParse",
                Priority = ThreadPriority.AboveNormal
            };
            this.workerThread.Start();
        }

        internal void SignalProcess()
        {
            if (this.disposed || this.stopRequested)
                return;

            this.workSignal.Set();
        }

        internal void DrainAllSynchronously()
        {
            if (this.disposed)
                return;

            bool forceFullRepaint = false;
            while (!this.stopRequested && this.hasPendingChunks())
                forceFullRepaint |= this.ProcessOneBatch(postRender: false);

            if (this.disposed || this.stopRequested)
                return;

            if (!this.invokeTarget.IsHandleCreated)
                return;

            bool fullRepaint = forceFullRepaint;
            if (this.invokeTarget.InvokeRequired)
                this.invokeTarget.Invoke(new Action(() => this.requestRender(fullRepaint)));
            else
                this.requestRender(fullRepaint);
        }

        public void Dispose()
        {
            if (this.disposed)
                return;

            this.disposed = true;
            this.stopRequested = true;
            this.workSignal.Set();
            if (this.workerThread.IsAlive)
                this.workerThread.Join(TimeSpan.FromSeconds(2));
            this.workSignal.Dispose();
        }

        private void WorkerLoop()
        {
            while (!this.stopRequested)
            {
                this.workSignal.WaitOne();
                while (!this.stopRequested && this.hasPendingChunks())
                    this.ProcessOneBatch(postRender: true);
            }
        }

        private bool ProcessOneBatch(bool postRender)
        {
            List<string> chunks = this.dequeueChunks();
            if (chunks == null || chunks.Count == 0)
                return false;

            bool forceFullRepaint = false;
            lock (this.session.SyncRoot)
            {
                foreach (string chunk in chunks)
                {
                    if (string.IsNullOrEmpty(chunk))
                        continue;

                    if (TerminalRenderPipeline.ChunkRequiresFullRepaint(chunk))
                        forceFullRepaint = true;
                    this.session.PushCore(chunk);
                }
            }

            if (!postRender || this.disposed || this.stopRequested)
                return forceFullRepaint;

            if (this.invokeTarget.IsDisposed)
                return forceFullRepaint;

            bool fullRepaint = forceFullRepaint;
            if (this.invokeTarget.InvokeRequired)
                this.invokeTarget.BeginInvoke(new Action(() => this.PostRender(fullRepaint)));
            else
                this.PostRender(fullRepaint);

            return forceFullRepaint;
        }

        private void PostRender(bool forceFullRepaint)
        {
            if (this.disposed || this.stopRequested || this.invokeTarget.IsDisposed)
                return;

            this.requestRender(forceFullRepaint);
        }
    }
}
