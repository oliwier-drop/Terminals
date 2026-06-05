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
            this.labelProfile = new System.Windows.Forms.Label();
            this.comboBoxProfile = new System.Windows.Forms.ComboBox();
            this.checkBoxCompression = new System.Windows.Forms.CheckBox();
            this.keysButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // labelProfile
            //
            this.labelProfile.AutoSize = true;
            this.labelProfile.Location = new System.Drawing.Point(15, 18);
            this.labelProfile.Name = "labelProfile";
            this.labelProfile.Size = new System.Drawing.Size(93, 13);
            this.labelProfile.TabIndex = 0;
            this.labelProfile.Text = "Connection profile";
            //
            // comboBoxProfile
            //
            this.comboBoxProfile.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxProfile.FormattingEnabled = true;
            this.comboBoxProfile.Items.AddRange(new object[]
            {
                "Server (Linux / OpenSSH)",
                "Network device (switch / router)"
            });
            this.comboBoxProfile.Location = new System.Drawing.Point(18, 34);
            this.comboBoxProfile.Name = "comboBoxProfile";
            this.comboBoxProfile.Size = new System.Drawing.Size(280, 21);
            this.comboBoxProfile.TabIndex = 1;
            this.comboBoxProfile.SelectedIndexChanged += new System.EventHandler(this.ComboBoxProfile_SelectedIndexChanged);
            //
            // checkBoxCompression
            //
            this.checkBoxCompression.AutoSize = true;
            this.checkBoxCompression.Location = new System.Drawing.Point(18, 68);
            this.checkBoxCompression.Name = "checkBoxCompression";
            this.checkBoxCompression.Size = new System.Drawing.Size(121, 17);
            this.checkBoxCompression.TabIndex = 2;
            this.checkBoxCompression.Text = "Enable compression";
            this.checkBoxCompression.UseVisualStyleBackColor = true;
            //
            // keysButton
            //
            this.keysButton.Location = new System.Drawing.Point(18, 98);
            this.keysButton.Name = "keysButton";
            this.keysButton.Size = new System.Drawing.Size(113, 23);
            this.keysButton.TabIndex = 3;
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
            this.Controls.Add(this.comboBoxProfile);
            this.Controls.Add(this.labelProfile);
            this.Name = "SshNetOptionsControl";
            this.Size = new System.Drawing.Size(320, 140);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label labelProfile;
        private System.Windows.Forms.ComboBox comboBoxProfile;
        private System.Windows.Forms.CheckBox checkBoxCompression;
        private System.Windows.Forms.Button keysButton;
    }
}
