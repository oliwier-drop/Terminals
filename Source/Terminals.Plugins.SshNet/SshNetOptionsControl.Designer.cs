// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
namespace Terminals.Plugins.SshNet
{
    partial class SshNetOptionsControl
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.checkBoxCompression = new System.Windows.Forms.CheckBox();
            this.keysButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // checkBoxCompression
            //
            this.checkBoxCompression.AutoSize = true;
            this.checkBoxCompression.Location = new System.Drawing.Point(18, 18);
            this.checkBoxCompression.Name = "checkBoxCompression";
            this.checkBoxCompression.Size = new System.Drawing.Size(121, 17);
            this.checkBoxCompression.TabIndex = 0;
            this.checkBoxCompression.Text = "Enable compression";
            this.checkBoxCompression.UseVisualStyleBackColor = true;
            //
            // keysButton
            //
            this.keysButton.Location = new System.Drawing.Point(18, 48);
            this.keysButton.Name = "keysButton";
            this.keysButton.Size = new System.Drawing.Size(113, 23);
            this.keysButton.TabIndex = 1;
            this.keysButton.Text = "SSH key store";
            this.keysButton.UseVisualStyleBackColor = true;
            this.keysButton.Click += new System.EventHandler(this.KeysButton_Click);
            //
            // SshNetOptionsControl
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.keysButton);
            this.Controls.Add(this.checkBoxCompression);
            this.Name = "SshNetOptionsControl";
            this.Size = new System.Drawing.Size(320, 90);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.CheckBox checkBoxCompression;
        private System.Windows.Forms.Button keysButton;
    }
}
