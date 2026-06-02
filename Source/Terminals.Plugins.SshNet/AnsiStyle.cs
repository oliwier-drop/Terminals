using System.Drawing;

namespace Terminals.Plugins.SshNet
{
    internal struct AnsiStyle
    {
        internal Color ForeColor;
        internal Color BackColor;
        internal bool Bold;

        internal static AnsiStyle Default()
        {
            return new AnsiStyle
            {
                ForeColor = Color.Gainsboro,
                BackColor = Color.Black,
                Bold = false
            };
        }

        internal AnsiStyle Clone()
        {
            return new AnsiStyle
            {
                ForeColor = this.ForeColor,
                BackColor = this.BackColor,
                Bold = this.Bold
            };
        }
    }
}
