﻿using CFixer;
using CFixer.Properties;
using CFixer.Views;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CrapFixer
{
    public partial class MainForm : Form
    {
        private NavigationManager _navigationManager;
        private NavigationHandler _navigationHandler;
        private LogActions _logActions;
        private LogActionsController _logActionsController;
        private readonly AppManagerService _appManager = new AppManagerService();

        public MainForm()
        {
            InitializeComponent();

            // Set up the main navigation manager and logger
            _navigationManager = new NavigationManager(panelContainer);
            Logger.OutputBox = rtbLogger;

            // Set up log actions controller
            _logActions = new LogActions(rtbLogger);
            _logActionsController = new LogActionsController(comboLogActions, _logActions);

            // Load features and plugins, and restore INI state if enabled
            FeatureNodeManager.LoadFeatures(treeFeatures);
            PluginManager.LoadPlugins(treeFeatures);
            if (IniStateManager.IsViewSettingEnabled("SETTINGS", "checkSaveToINI"))
            {
                IniStateManager.Load(treeFeatures, this);
            }
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            // Set the default active button
            _navigationHandler = new NavigationHandler(btnHome, btnSettings);
            _navigationHandler.NavigationButtonClicked += button =>
            {
                if (button == btnHome)
                {
                    _navigationManager.GoToMain();
                }
                else if (button == btnSettings)
                {
                    _navigationManager.SwitchView(new OptionsView());
                }
            };

            // Set up link label for update check
            linkUpdateCheck.LinkClicked += (_, __) =>
             Process.Start($"https://builtbybel.github.io/CrapFixer/update-check.html?version={Program.GetAppVersion()}");

            // Set up log actions controller
            _logActionsController = new LogActionsController(comboLogActions, _logActions);

            // Set up version labels
            async Task SetVersionLabels()
            {
                lblVersionInfo.Text = $"v{Program.GetAppVersion()} ";
                lblOSInfo.Text = await OSHelper.OSHelper.GetWindowsVersion();
            }
            _ = SetVersionLabels();
        }

        private async void btnAnalyze_Click(object sender, EventArgs e)
        {
            rtbLogger.Clear();

            // Analyze features
            await FeatureNodeManager.AnalyzeAll(treeFeatures.Nodes);

            // Analyze plugins
            foreach (TreeNode node in treeFeatures.Nodes)
            {
                PluginManager.AnalyzeAll(node);
            }

            // Analyze apps
            await AnalyzeApps();

            // Show log actions combo box
            comboLogActions.Visible = true;
        }

        /// <summary>
        /// Analyzes the apps and logs the results.
        /// </summary>
        /// <returns></returns>
        private async Task AnalyzeApps()
        {
            checkedListBoxApps.Items.Clear();

            // Try loading external bloatware list from file
            string[] predefined = _appManager.LoadExternalBloatwareList();

            if (predefined.Length == 0)
            {
                // Fallback to internal resource if external file not found or empty
                predefined = Resources.PredefinedApps?
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).ToArray() ?? Array.Empty<string>();

                // Logger.Log("Using built-in bloatware list.", LogLevel.Info);
            }
            else
            {
                Logger.Log("Using external bloatware list via CFEnhancer", LogLevel.Info);
            }

            // Analyze installed apps against the predefined list
            var apps = await _appManager.AnalyzeAndLogAppsAsync(predefined);

            foreach (var app in apps)
            {
                checkedListBoxApps.Items.Add(app.FullName);
            }

            if (!apps.Any())
            {
                Logger.Log("✅ No bloatware apps found.", LogLevel.Info);
            }
        }

        private async void btnFix_Click(object sender, EventArgs e)
        {
            rtbLogger.Clear();

            // Fix all features
            foreach (TreeNode node in treeFeatures.Nodes)
                await FeatureNodeManager.FixChecked(node);

            // Fix all plugins
            foreach (TreeNode node in treeFeatures.Nodes)
                await PluginManager.FixChecked(node);

            // Fix selected Store apps
            var selectedApps = checkedListBoxApps.CheckedItems.Cast<string>().ToList();
            if (selectedApps.Count == 0)
                return;

            var appService = new AppManagerService();
            var removedApps = await appService.UninstallSelectedAppsAsync(selectedApps);

            // Update UI after uninstall
            foreach (var app in removedApps)
            {
                checkedListBoxApps.Items.Remove(app);
            }
        }

        private void btnRestore_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
           "⚠️ This will restore all selected features to their original state.\n" +
           "Changes made by previous configurations may be reverted.\n\n" +
           "Are you sure you want to proceed?",
           "Restore Selected Features",
           MessageBoxButtons.YesNo,
           MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                rtbLogger.Clear();
                foreach (TreeNode node in treeFeatures.Nodes)
                    FeatureNodeManager.RestoreChecked(node);

                Logger.Log("↩️ All selected features have been restored.", LogLevel.Info);
            }
        }

        private async void analyzeMarkedFeatureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (treeFeatures.SelectedNode is TreeNode selectedNode)
            {
                if (PluginManager.IsPluginNode(selectedNode))
                    await PluginManager.AnalyzePlugin(selectedNode);
                else
                    Logger.Log($"🔎 Analyzing Feature: {selectedNode.Text}", LogLevel.Info);
                FeatureNodeManager.AnalyzeFeature(selectedNode);
            }
        }

        private async void previewMarkedFeatureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (treeFeatures.SelectedNode is TreeNode selectedNode)
            {
                if (PluginManager.IsPluginNode(selectedNode))
                {
                    await PluginManager.AnalyzePlugin(selectedNode);
                }
                else
                {
                    FeatureNodeManager.PreviewChanges(selectedNode);
                }
            }
        }

        private async void fixMarkedFeatureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (treeFeatures.SelectedNode is TreeNode selectedNode)
            {
                if (PluginManager.IsPluginNode(selectedNode))
                    await PluginManager.FixPlugin(selectedNode);
                else
                    Logger.Log($"🔧 Fixing Feature: {selectedNode.Text}", LogLevel.Info);
                await FeatureNodeManager.FixFeature(selectedNode);
            }
        }

        private void restoreMarkedFeatureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (treeFeatures.SelectedNode is TreeNode selectedNode)
            {
                if (PluginManager.IsPluginNode(selectedNode))
                    PluginManager.RestorePlugin(selectedNode);
                else
                    Logger.Log($"↩️ Restoring Feature: {selectedNode.Text}", LogLevel.Info);
                FeatureNodeManager.RestoreFeature(selectedNode);
            }
        }

        private void helpMarkedFeatureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (treeFeatures.SelectedNode is TreeNode selectedNode)
            {
                FeatureNodeManager.ShowHelp(selectedNode);

                // Prompt for online search
                var result = MessageBox.Show(
                    "Would you like to search online for more information about this feature?",
                    "Online Help",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    FeatureNodeManager.ShowHelpOnline(selectedNode);
                }
            }
        }

        /// <summary>
        /// Checks or unchecks all child nodes when a parent node is checked/unchecked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeFeatures_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (e.Action != TreeViewAction.Unknown)
            {
                foreach (TreeNode child in e.Node.Nodes)
                    child.Checked = e.Node.Checked;
            }
        }

        private void treeFeatures_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Get the node under the mouse cursor
                TreeNode nodeUnderMouse = treeFeatures.GetNodeAt(e.X, e.Y);

                if (nodeUnderMouse != null)
                {
                    treeFeatures.SelectedNode = nodeUnderMouse;

                    // Show the context menu at the mouse position
                    contextMenuStrip.Show(treeFeatures, e.Location);
                }
            }
        }

        /// <summary>
        /// Handles the link click event for selecting or deselecting all items in the list.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private bool treeChecked = false;

        private void linkSelection_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (tabControl.SelectedTab == Windows)
            {
                foreach (TreeNode node in treeFeatures.Nodes)
                {
                    node.Checked = treeChecked;
                    foreach (TreeNode child in node.Nodes)
                    {
                        child.Checked = treeChecked;
                        foreach (TreeNode grandChild in child.Nodes)
                            grandChild.Checked = treeChecked;
                    }
                }

                treeChecked = !treeChecked;
            }
            else if (tabControl.SelectedTab == Apps)
            {
                bool shouldCheck = checkedListBoxApps.Items.Cast<object>()
                    .Any(item => checkedListBoxApps.GetItemChecked(checkedListBoxApps.Items.IndexOf(item)) == false);

                for (int i = 0; i < checkedListBoxApps.Items.Count; i++)
                {
                    checkedListBoxApps.SetItemChecked(i, shouldCheck);
                }
            }
        }

        private void panelHeader_Paint(object sender, PaintEventArgs e)
        {
            Panel panel = sender as Panel;
            Graphics g = e.Graphics;

            // Fill background
            g.Clear(Color.FromArgb(77, 77, 77));

            // Draw a single magenta line at the bottom
            using (Pen pen = new Pen(Color.FromArgb(100, 255, 0, 255))) // Magenta with transparency
            {
                int y = panel.Height - 1; // Bottom-most pixel
                g.DrawLine(pen, 0, y, panel.Width, y);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (IniStateManager.IsViewSettingEnabled("SETTINGS", "checkSaveToINI"))
            {
                IniStateManager.Save(treeFeatures, this);
            }
        }
    }
}