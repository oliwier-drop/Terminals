using System.Drawing;
using System.Windows.Forms;

namespace Terminals.Forms
{
    /// <summary>Aligns child dialogs with MainForm PerMonitorV2 + AutoScaleMode.Dpi.</summary>
    internal static class DpiFormHelper
    {
        internal static void Apply(Form form)
        {
            if (form == null)
                return;

            form.Font = SystemFonts.IconTitleFont;
            form.AutoScaleDimensions = new SizeF(96F, 96F);
            form.AutoScaleMode = AutoScaleMode.Dpi;
        }

        internal static void InheritChildren(Control root)
        {
            if (root == null)
                return;

            if (root is ContainerControl container)
                container.AutoScaleMode = AutoScaleMode.Inherit;

            foreach (Control child in root.Controls)
                InheritChildren(child);
        }
    }
}
