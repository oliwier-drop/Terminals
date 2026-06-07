// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System.Collections.Generic;

namespace Terminals.Plugins.SshNet.Rendering
{
    internal sealed class TerminalRowDiffer
    {
        private const double FullRepaintRowFraction = 0.8;

        internal IList<int> GetDirtyRows(
            TerminalCellGrid previous,
            TerminalCellGrid current,
            TerminalRowDiffOptions options)
        {
            var dirty = new List<int>();
            if (current == null)
                return dirty;

            int rows = current.Rows;
            if (options.ForceFullRepaint
                || previous == null
                || previous.Columns != current.Columns
                || previous.Rows != current.Rows)
            {
                for (int row = 0; row < rows; row++)
                    dirty.Add(row);
                return dirty;
            }

            for (int row = 0; row < rows; row++)
            {
                if (previous.GetRowHash(row) != current.GetRowHash(row))
                    dirty.Add(row);
            }

            if (dirty.Count >= rows * FullRepaintRowFraction)
            {
                dirty.Clear();
                for (int row = 0; row < rows; row++)
                    dirty.Add(row);
            }

            return dirty;
        }
    }

    internal struct TerminalRowDiffOptions
    {
        internal bool ForceFullRepaint;
    }
}
