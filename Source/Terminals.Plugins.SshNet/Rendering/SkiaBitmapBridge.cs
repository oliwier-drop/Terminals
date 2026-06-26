// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using SkiaSharp;

namespace Terminals.Plugins.SshNet.Rendering
{
    internal static class SkiaBitmapBridge
    {
        internal static void PaintRegion(Bitmap bitmap, Rectangle region, Action<SKCanvas> paint)
        {
            if (bitmap == null || paint == null || region.Width <= 0 || region.Height <= 0)
                return;

            var pixelFormat = bitmap.PixelFormat;
            if (pixelFormat != PixelFormat.Format32bppArgb
                && pixelFormat != PixelFormat.Format32bppPArgb
                && pixelFormat != PixelFormat.Format32bppRgb)
            {
                PaintRegionViaCopy(bitmap, region, paint);
                return;
            }

            var bmpData = bitmap.LockBits(
                region,
                ImageLockMode.ReadWrite,
                PixelFormat.Format32bppPArgb);
            try
            {
                var info = new SKImageInfo(
                    region.Width,
                    region.Height,
                    SKColorType.Bgra8888,
                    SKAlphaType.Premul);
                using (var surface = SKSurface.Create(info, bmpData.Scan0, bmpData.Stride))
                {
                    if (surface == null)
                    {
                        PaintRegionViaCopy(bitmap, region, paint);
                        return;
                    }

                    SKCanvas canvas = surface.Canvas;
                    canvas.Save();
                    canvas.Translate(-region.Left, -region.Top);
                    paint(canvas);
                    canvas.Restore();
                    canvas.Flush();
                }
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }
        }

        internal static void PaintFull(Bitmap bitmap, Action<SKCanvas> paint)
        {
            if (bitmap == null)
                return;

            PaintRegion(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height), paint);
        }

        private static void PaintRegionViaCopy(Bitmap bitmap, Rectangle region, Action<SKCanvas> paint)
        {
            var info = new SKImageInfo(region.Width, region.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var skBitmap = new SKBitmap(info))
            {
                using (var temp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppPArgb))
                using (var gdi = Graphics.FromImage(temp))
                {
                    gdi.DrawImage(bitmap, new Rectangle(0, 0, region.Width, region.Height), region, GraphicsUnit.Pixel);
                    CopyGdiToSkia(temp, skBitmap);
                }

                using (var canvas = new SKCanvas(skBitmap))
                {
                    canvas.Save();
                    canvas.Translate(-region.Left, -region.Top);
                    paint(canvas);
                    canvas.Restore();
                    canvas.Flush();
                }

                using (var temp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppPArgb))
                {
                    CopySkiaToGdi(skBitmap, temp);
                    using (var gdi = Graphics.FromImage(bitmap))
                        gdi.DrawImage(temp, region.Location);
                }
            }
        }

        private static void CopyGdiToSkia(Bitmap source, SKBitmap destination)
        {
            var data = source.LockBits(
                new Rectangle(0, 0, source.Width, source.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppPArgb);
            try
            {
                destination.SetPixels(data.Scan0);
            }
            finally
            {
                source.UnlockBits(data);
            }
        }

        private static void CopySkiaToGdi(SKBitmap source, Bitmap destination)
        {
            SKPixmap pixmap = source.PeekPixels();
            if (pixmap == null)
                return;

            var data = destination.LockBits(
                new Rectangle(0, 0, destination.Width, destination.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppPArgb);
            try
            {
                pixmap.ReadPixels(
                    new SKImageInfo(destination.Width, destination.Height, SKColorType.Bgra8888, SKAlphaType.Premul),
                    data.Scan0,
                    data.Stride,
                    0,
                    0);
            }
            finally
            {
                destination.UnlockBits(data);
            }
        }
    }
}
