using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using Terminals.Configuration;
using Terminals.Connections;
using Terminals.Data;
using Terminals.Services;

namespace Terminals.Forms
{
    internal partial class OptionDialog : Form
    {
        private readonly Settings settings = Settings.Instance;
        private UserControl currentPanel;

        public OptionDialog(IConnectionExtra terminal, IPersistence persistence)
        {
            DpiFormHelper.Apply(this);

            InitializeComponent();

            this.linkLabel1.Text = ForkBranding.IssuesPageUrl;
            this.linkLabel1.LinkClicked += (sender, args) => ExternalLinks.OpenIssuesPage();

            this.panelMasterPassword.Security = persistence.Security;
            MovePanelsFromTabsIntoControls();
            settings.ConfigurationChanged += new ConfigurationChangedHandler(this.SettingsConfigFileReloaded);
            LoadSettings();
            
            this.SetFormSize();
            UpdateLookAndFeel(terminal);
        }

        private void SettingsConfigFileReloaded(ConfigurationChangedEventArgs args)
        {
            LoadSettings();
        }

        private void UpdateLookAndFeel(IConnectionExtra terminal)
        {
            // Update the old treeview theme to the new theme
            Native.Methods.SetWindowTheme(this.OptionsTreeView.Handle, "Explorer", null);

            this.panelConnections.CurrentTerminal = terminal;
            this.currentPanel = this.panelStartupShutdown;
            this.OptionsTreeView.SelectedNode = this.OptionsTreeView.Nodes[0];
            this.OptionsTreeView.Select();

            this.DrawBottomLine();
        }

        private void SetFormSize()
        {
            const int designClientWidth = 850;
            const int designClientHeight = 462;
            int contentRight = Math.Max(
                this.btnCancel.Right,
                this.OptionTitelLabel.Right) + 12;
            int contentBottom = Math.Max(
                this.btnCancel.Bottom,
                this.linkLabel1.Bottom) + 12;
            int clientWidth = Math.Max(designClientWidth, contentRight);
            int clientHeight = Math.Max(designClientHeight, contentBottom);
            this.ClientSize = new Size(clientWidth, clientHeight);
        }

        /// <summary>
        /// Hide tabpage control, only used in design time
        /// </summary>
        private void MovePanelsFromTabsIntoControls()
        {
            this.tabCtrlOptionPanels.Hide();
            this.CollectOptionPanelControls();
        }

        /// <summary>
        /// Get all the panel control from the tabpages 
        /// and add them to the form controls collection and hide the controls
        /// </summary>
        private void CollectOptionPanelControls()
        {
            foreach (TabPage tp in this.tabCtrlOptionPanels.TabPages)
            {
                foreach (Control ctrl in tp.Controls)
                {
                    if (ctrl is UserControl userControl)
                    {
                        userControl.AutoScaleMode = AutoScaleMode.Inherit;
                        userControl.AutoScroll = true;
                        userControl.Hide();
                        this.Controls.Add(userControl);
                    }
                }
            }
        }

        private void DrawBottomLine()
        {
            Label lbl = new Label();
            lbl.AutoSize = false;
            lbl.BorderStyle = BorderStyle.Fixed3D;
            lbl.SetBounds(
                this.OptionTitelLabel.Left,
                this.OptionsTreeView.Top + this.OptionsTreeView.Height - 1,
                this.OptionTitelLabel.Width,
                2);
            this.Controls.Add(lbl);
            lbl.Show();
        }

        private void OptionsTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            try
            {
                this.currentPanel.Hide();
                SelectNewPanel();
                UpdatePanelPosition();
                this.currentPanel.Show();
                this.OptionTitelLabel.Text = this.OptionsTreeView.SelectedNode.Name.Replace("&", "&&");
                UpdateTreeNodeState(e);
            }
            catch (Exception ex)
            {
                Logging.Info(ex);
            }
        }

        private static void UpdateTreeNodeState(TreeViewEventArgs e)
        {
            if (e.Node.GetNodeCount(true) > 0)
            {
                switch (e.Action)
                {
                    case TreeViewAction.ByKeyboard:
                    case TreeViewAction.ByMouse:
                        if (e.Node.IsExpanded)
                            e.Node.Collapse();
                        else
                            e.Node.Expand();
                        break;
                }
            }
        }

        private void SelectNewPanel()
        {
            string panelName = "panel" + this.OptionsTreeView.SelectedNode.Tag;
            System.Diagnostics.Debug.WriteLine("Selected panel: " + panelName);
            this.currentPanel = this.Controls[panelName] as UserControl;
        }

        private void UpdatePanelPosition()
        {
            int x = this.OptionTitelLabel.Left;
            int y = this.OptionTitelLabel.Top + this.OptionTitelLabel.Height + 3;
            int right = this.ClientSize.Width - 12;
            int bottom = this.btnOk.Top - 8;
            int width = Math.Max(200, right - x);
            int height = Math.Max(200, bottom - y);
            this.currentPanel.Location = new Point(x, y);
            this.currentPanel.Size = new Size(width, height);
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            try
            {
                settings.StartDelayedUpdate();
                SaveAllPanels();
            }
            catch (Exception exception)
            {
                Logging.Error("Error saving application settings.", exception);
                MessageBox.Show(String.Format("Error saving settings.\r\n{0}", exception.Message));
            }
            finally
            {
                settings.SaveAndFinishDelayedUpdate();
            }
        }

        private void SaveAllPanels()
        {
            foreach (IOptionPanel optionPanel in FindOptionPanels())
            {
                optionPanel.SaveSettings();
            }
        }

        private void LoadSettings()
        {
            foreach (IOptionPanel optionPanel in FindOptionPanels())
            {
                optionPanel.LoadSettings();
            }
        }

        private IEnumerable<IOptionPanel> FindOptionPanels()
        {
            return this.Controls
                .Cast<Control>()
                .OfType<IOptionPanel>();
        }

        private void OptionDialog_FormClosed(object sender, FormClosedEventArgs e)
        {
            settings.ConfigurationChanged -= SettingsConfigFileReloaded;
        }
    }
}