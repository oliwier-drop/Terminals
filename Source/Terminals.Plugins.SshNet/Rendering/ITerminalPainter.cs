// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using SkiaSharp;

namespace Terminals.Plugins.SshNet.Rendering
{
    internal interface ITerminalPainter
    {
        int CellWidth { get; }

        int CellHeight { get; }

        void ConfigureCanvas(SKCanvas canvas);

        void PaintRow(SKCanvas canvas, TerminalCellGrid grid, int gridRow, int destinationY);

        void PaintSelectionCell(SKCanvas canvas, TerminalCell cell, int x, int y);
    }
}
