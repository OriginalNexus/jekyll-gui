﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace jekyll_gui.Forms
{
	public partial class MainForm : Form
	{

		private string projectPath = "";
		private ConsoleTask serveTask = new ConsoleTask();
		private ConsoleTask newTask = new ConsoleTask();
		private ConsoleTask buildTask = new ConsoleTask();

		public MainForm()
		{
			InitializeComponent();

			ConsoleTask.SetForm(this);
			ConsoleTask.SetConsole(consoleTextBox);
			serveTask.AddTaskCompleteEventHandler(serve_TaskComplete);
			updateJekyllTasks();
		}


		private void updateJekyllTasks()
		{
			JekyllEnv.IPAddres = getLocalIPAddress();
			JekyllEnv.PortNumber = (uint) portNumericBox.Value;
			JekyllEnv.WorkingDir = projectPath;
			JekyllEnv.SetJekyllConsoleTask(serveTask, JekyllEnv.JekyllCommand.SERVE_SITE);
			JekyllEnv.SetJekyllConsoleTask(newTask, JekyllEnv.JekyllCommand.CREATE_SITE);
			JekyllEnv.SetJekyllConsoleTask(buildTask, JekyllEnv.JekyllCommand.BUILD_SITE);
		}

		private void startServer()
		{
			if (newTask.IsRunning) return;

			hostLb.Text = "http:\\\\" + getLocalIPAddress() + ":" + portNumericBox.Value;

			JekyllEnv.SetJekyllConsoleTask(serveTask, JekyllEnv.JekyllCommand.SERVE_SITE);
			serveTask.RunTaskAsync();

			toggleServerBtn.Text = "Stop Server";
			toggleServerBtn.ForeColor = System.Drawing.Color.DarkRed;
			toggleServerBtn.Enabled = true;
			portPanel.Visible = portPanel.Enabled = false;
			serverStatusPanel.Enabled = serverStatusPanel.Visible = true;
			cleanMenuItem.Enabled = buildMenuItem.Enabled = exportMenuItem.Enabled = rebuildMenuItem.Enabled = false;
			cleanMenuItem.ToolTipText = buildMenuItem.ToolTipText = exportMenuItem.ToolTipText = rebuildMenuItem.ToolTipText = "Stop server first";
		}

		private void stopServer()
		{
			toggleServerBtn.Enabled = false;
			serveTask.StopTask();

			toggleServerBtn.Text = "Start Server";
			toggleServerBtn.ForeColor = System.Drawing.Color.DarkGreen;
			toggleServerBtn.Enabled = true;
			serverStatusPanel.Enabled = serverStatusPanel.Visible = false;
			portPanel.Visible = portPanel.Enabled = true;
			cleanMenuItem.Enabled = buildMenuItem.Enabled = exportMenuItem.Enabled = rebuildMenuItem.Enabled = true;
			cleanMenuItem.ToolTipText = buildMenuItem.ToolTipText = exportMenuItem.ToolTipText = rebuildMenuItem.ToolTipText = null;
			if (!projectPanel.Visible) exportMenuItem.Enabled = false;
		}

		private void toggleServer()
		{
			if (serveTask.IsRunning)
				stopServer();
			else
				startServer();
		}

		private void newProject()
		{
			if (projectBrowserDialog.ShowDialog() == DialogResult.OK) {
				string path = projectBrowserDialog.SelectedPath;
				if (Directory.GetFileSystemEntries(path).Length != 0) {
					DialogResult result = MessageBox.Show("Selected folder is not empty. Proceed anyways?", "Warning", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
					if (result == DialogResult.Cancel) {
						return;
					}
					if (result == DialogResult.No) {
						newProject();
						return;
					}
				}

				// Run "jekyll new" in that folder
				openProject(path);
				newTask.RunTaskSync();
			}
		}

		private void openProject(string path)
		{
			if (path == null) {
				if (projectBrowserDialog.ShowDialog() == DialogResult.OK) {
					path = projectBrowserDialog.SelectedPath;
				}
				else {
					return;
				}
			}
			closeProject();
			projectNameLb.Text = path.Substring(path.LastIndexOfAny(new char[] { '\\', '/' }) + 1);
			projectPathLb.Text = projectPath = path;
			projectPanel.Visible = projectPanel.Enabled = projectMenu.Enabled = exportMenuItem.Enabled = true;
			foreach (ToolStripItem m in projectMenu.DropDownItems) {
				if (m is ToolStripMenuItem) m.Enabled = true;
			}
			updateJekyllTasks();
		}

		private void exportProject()
		{
			string SourcePath = projectPath + @"\_site";
			if (!Directory.Exists(SourcePath)) {
				buildTask.RunTaskSync();
			}

			if (exportBrowserDialog.ShowDialog() == DialogResult.OK) {
				string DestinationPath = exportBrowserDialog.SelectedPath;
				if (Directory.GetFileSystemEntries(DestinationPath).Length != 0) {
					DialogResult result = MessageBox.Show("Selected folder is not empty. Proceed anyways?", "Warning", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
					if (result == DialogResult.Cancel) {
						return;
					}
					if (result == DialogResult.No) {
						exportProject();
						return;
					}
				}

				consoleTextBox.Clear();
				consoleTextBox.AppendText("Copying files...");
				consoleTextBox.AppendText(Environment.NewLine);

				// Code inspired from http://stackoverflow.com/a/3822913/3680746

				// Create all of the directories
				foreach (string dirPath in Directory.GetDirectories(SourcePath, "*", SearchOption.AllDirectories)) {
					Directory.CreateDirectory(dirPath.Replace(SourcePath, DestinationPath));
					consoleTextBox.AppendText("Creating " + dirPath.Replace(SourcePath, DestinationPath));
					consoleTextBox.AppendText(Environment.NewLine);
				}

				// Copy all the files & replaces any files with the same name
				foreach (string newPath in Directory.GetFiles(SourcePath, "*.*", SearchOption.AllDirectories)) {
					consoleTextBox.AppendText("Copying " + newPath);
					File.Copy(newPath, newPath.Replace(SourcePath, DestinationPath), true);
					consoleTextBox.AppendText(Environment.NewLine);
				}


				consoleTextBox.AppendText("Successfully exported site to " + DestinationPath);
				consoleTextBox.AppendText(Environment.NewLine);
			}
		}

		private void cleanProject()
		{
			string[] paths = { @"\_site", @"\.sass-cache" };
			foreach (string path in paths) {
				if (Directory.Exists(projectPath + path)) {
					Directory.Delete(projectPath + path, true);
				}
			}

			consoleTextBox.Clear();
		}

		private void closeProject()
		{
			stopServer();
			projectPanel.Visible = projectPanel.Enabled = projectMenu.Enabled = exportMenuItem.Enabled = false;
			foreach (ToolStripItem m in projectMenu.DropDownItems) {
				if (m is ToolStripMenuItem) m.Enabled = false;
			}
			consoleTextBox.Text = "";
			projectNameLb.Text = projectPathLb.Text = projectPath = "";
		}

		private static string getLocalIPAddress()
		{
			var host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (var ip in host.AddressList) {
				if (ip.AddressFamily == AddressFamily.InterNetwork) {
					return ip.ToString();
				}
			}
			return "localhost";
		}


		#region Event Handlers
		private void MainForm_Load(object sender, EventArgs e)
		{
			// Set Icon
			Icon = Properties.Resources.jekyll_icon;
			if (!JekyllEnv.InstallJekyllEnvironment()) Close();
			closeProject();
		}


		private void newProjectMenuItem_Click(object sender, EventArgs e)
		{
			newProject();
		}

		private void openMenuItem_Click(object sender, EventArgs e)
		{
			openProject(null);
		}

		private void exportMenuItem_Click(object sender, EventArgs e)
		{
			exportProject();
		}

		private void closeMenuItem_Click(object sender, EventArgs e)
		{
			closeProject();
		}

		private void moreThemesMenuItem_Click(object sender, EventArgs e)
		{
			Process.Start(@"https://github.com/jekyll/jekyll/wiki/Themes");
		}

		private void exitMenuItem_Click(object sender, EventArgs e)
		{
			Close();
		}


		private void toggleServerMenuItem_Click(object sender, EventArgs e)
		{
			toggleServerBtn.PerformClick();
		}

		private void buildMenuItem_Click(object sender, EventArgs e)
		{
			if (!buildTask.IsRunning) buildTask.RunTaskSync();
		}

		private void rebuildMenuItem_Click(object sender, EventArgs e)
		{
			cleanMenuItem_Click(sender, e);
			buildMenuItem_Click(sender, e);
		}

		private void cleanMenuItem_Click(object sender, EventArgs e)
		{
			cleanProject();
		}


		private void jekyllMenuItem_Click(object sender, EventArgs e)
		{
			Process.Start(@"https://jekyllrb.com/docs/home/");
		}

		private void kramdownMenuItem_Click(object sender, EventArgs e)
		{
			Process.Start(@"http://kramdown.gettalong.org/syntax.html");
		}

		private void webdevMenuItem_Click(object sender, EventArgs e)
		{
			Process.Start(@"http://www.w3schools.com/");
		}

		private void toporSeparator_Click(object sender, EventArgs e)
		{
			MessageBox.Show("This product was possible thanks to the power of Nexus. For more info and awesomeness, a website will appear.", "Nexus Power", MessageBoxButtons.OK);
			Process.Start(@"http://topor.io");
		}

		private void aboutMenuItem_Click(object sender, EventArgs e)
		{
			Form f = new AboutForm();
			f.ShowDialog();
		}


		private void consoleMenuStrip_Opening(object sender, CancelEventArgs e)
		{
			copyConsoleMenuItem.Visible = (consoleTextBox.SelectionLength == 0) ? false : true;
			e.Cancel = (consoleTextBox.TextLength == 0) ? true : false;
		}

		private void copyConsoleMenuItem_Click(object sender, EventArgs e)
		{
			Clipboard.SetText(consoleTextBox.SelectedText);
		}

		private void copyAllConsoleMenuItem_Click(object sender, EventArgs e)
		{
			Clipboard.SetText(consoleTextBox.Text);
		}

		private void clearConsoleMenuItem_Click(object sender, EventArgs e)
		{
			consoleTextBox.Clear();
		}


		private void projectPathLb_Click(object sender, EventArgs e)
		{
			if (Directory.Exists(projectPath)) {
				Process.Start(projectPath);
			}
			else {
				MessageBox.Show("Path not found. Please make sure the project exists.", "Jekyll GUI", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void hostLb_Click(object sender, EventArgs e)
		{
			Process.Start(hostLb.Text);
		}

		private void toggleServerBtn_Click(object sender, EventArgs e)
		{
			toggleServer();
		}

		private void portNumericBox_ValueChanged(object sender, EventArgs e)
		{
			JekyllEnv.PortNumber = (uint) portNumericBox.Value;
		}

		private void portNumericBox_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Enter) {
				startServer();
			}
		}

		private void serve_TaskComplete(object sender, RunWorkerCompletedEventArgs e)
		{
			stopServer();
		}
		#endregion
	}
}
