using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

namespace TabControl
{
    internal class TabControlCloseButton
    {
        #region Fields

        private Rectangle crossRect = Rectangle.Empty;
        private bool isMouseOver = false;
        private ToolStripProfessionalRenderer renderer;

        #endregion

        #region Props

        public bool IsMouseOver
        {
            get { return isMouseOver; }
            set { isMouseOver = value; }
        }

        public Rectangle Rect
        {
            get { return crossRect; }
            set { crossRect = value; }
        }

        #endregion

        #region Ctor

        internal TabControlCloseButton(ToolStripProfessionalRenderer renderer)
        {
            this.renderer = renderer;
        }

        #endregion

        #region Methods

        public void DrawCross(Graphics g)
        {
            DrawCross(g, crossRect, isMouseOver);
        }

        public void DrawCross(Graphics g, Rectangle rect, bool mouseOver)
        {
            if (mouseOver)
            {
                Color fill = renderer.ColorTable.ButtonSelectedHighlight;

                g.FillRectangle(new SolidBrush(fill), rect);

                Rectangle borderRect = rect;

                borderRect.Width--;
                borderRect.Height--;

                g.DrawRectangle(SystemPens.Highlight, borderRect);
            }

            using (Pen pen = new Pen(Color.Black, 1f))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                g.DrawLine(pen, rect.Left + 3, rect.Top + 4,
                    rect.Right - 6, rect.Bottom - 4);
                g.DrawLine(pen, rect.Left + 4, rect.Top + 4,
                    rect.Right - 5, rect.Bottom - 4);

                g.DrawLine(pen, rect.Right - 6, rect.Top + 4,
                    rect.Left + 3, rect.Bottom - 4);
                g.DrawLine(pen, rect.Right - 5, rect.Top + 4,
                    rect.Left + 4, rect.Bottom - 4);
            }
        }

        #endregion
    }
}
