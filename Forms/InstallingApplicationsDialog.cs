using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Windows.Forms;
using CDBurnerXP;
using CDBurnerXP.Forms;
using Ketarin.Localization;

namespace Ketarin.Forms
{
    /// <summary>
    /// Represents a dialog that shows the progress of installing applications.
    /// </summary>
    public partial class InstallingApplicationsDialog : PersistentForm
    {
        private bool expanded;
        private readonly List<LogItem> logItems = new List<LogItem>();
        private int installCounter;

        #region LogItem

        private enum LogItemType
        {
            Info,
            Warning, 
            Error
        }

        private class LogItem
        {
            public string Message { get; set; }
            public LogItemType Type { get; set; }
            public DateTime Time { get; set; }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets if the dialog should be closed after finishing.
        /// </summary>
        public bool AutoClose
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets whether or not the applications should be updated before installing.
        /// </summary>
        public bool UpdateApplications
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the applications that are to be installed.
        /// </summary>
        public ApplicationJob[] Applications
        {
            get; set;
        }

        #endregion

        public InstallingApplicationsDialog()
        {
            InitializeComponent();

            CancelButton = bCancel;
        }

        protected override void OnLoad(EventArgs e)
        {
            if (DesignMode) return;
            
            // Aplicar textos localizados
            ApplyLocalization();
            
            bool expandedByDefault = Conversion.ToBoolean(Settings.GetValue(this, "Expanded", false));
            // Collapse dialog initially
            this.expanded = expandedByDefault;
            if (!this.expanded)
            {
                this.Height -= pnlExpanded.Height;
            }
            SetExpansionButton();
            lbShowHideDetails.ImageIndex = (expanded ? 0 : 3);

            colTime.ImageGetter = x => Convert.ToInt32(((LogItem) x).Type);

            base.OnLoad(e);

            // Since progress hardly be determined, show animated progress bar
            progressBar.Style = ProgressBarStyle.Marquee;
            bgwSetup.RunWorkerAsync();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            Settings.SetValue(this, "Expanded", this.expanded);
        }

        /// <summary>
        /// Updates the status label, thread safe.
        /// </summary>
        private void UpdateStatus(string text)
        {
            if (InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate() { UpdateStatus(text); });
            }

            this.lblSetupStatus.Text = text;
        }

        /// <summary>
        /// Inserts an item into the log, thread safe.
        /// </summary>
        private void LogInfo(string text, LogItemType type)
        {
            if (InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate() { LogInfo(text, type); });
                return;
            }

            logItems.Add(new LogItem() { Message = text, Type = type, Time = DateTime.Now });
            olvLog.SetObjects(logItems);
        }

        private void bgwSetup_DoWork(object sender, DoWorkEventArgs e)
        {
            int count = 1;

            foreach (ApplicationJob job in this.Applications)
            {
                try
                {
                    UpdateAndInstallApp(e, job, ref count);
                }
                catch (Exception ex)
                {
                    LogInfo(job.Name + ": Setup failed (" + ex.Message + ")", LogItemType.Error);
                }
            }
        }

        private void SetExpansionButton()
        {
            pnlExpanded.Visible = this.expanded;
            string hideDetails = LocalizationManager.GetString("HideDetails", "Hide details");
            string showDetails = LocalizationManager.GetString("ShowDetails", "Show details");
            lbShowHideDetails.Text = (this.expanded ? "        " + "&" + hideDetails : "        " + "&" + showDetails);
        }

        private void UpdateAndInstallApp(DoWorkEventArgs e, ApplicationJob job, ref int count)
        {
            // Check: Are actually some instructions defined?
            if (job.SetupInstructions.Count == 0)
            {
                string skippedMessage = LocalizationManager.GetString("SkippedNoInstructions", "Skipped since no setup instructions exist");
                LogInfo(job.Name + ": " + skippedMessage, LogItemType.Warning);
                return;
            }

            if (bgwSetup.CancellationPending) return;

            // Force update if no file exists
            if (this.UpdateApplications || !job.FileExists)
            {
                string updatingMessage = LocalizationManager.GetString("UpdatingApplication", "Updating application {0} of {1}: {2}");
                UpdateStatus(string.Format(updatingMessage, count, this.Applications.Length, job.Name));

                Updater updater = new Updater {IgnoreCheckForUpdatesOnly = true};
                updater.BeginUpdate(new[] { job }, false, false);

                // Wait until finished
                while (updater.IsBusy)
                {
                    updater.ProgressChanged += this.updater_ProgressChanged;

                    if (bgwSetup.CancellationPending)
                    {
                        updater.Cancel();
                        return;
                    }
                    Thread.Sleep(500);
                }

                this.Invoke((MethodInvoker)delegate()
                {
                    progressBar.Style = ProgressBarStyle.Marquee;
                }); 

                // Did update fail? Install if {file} is present.
                if (updater.Errors.Length > 0)
                {
                    if (job.FileExists)
                    {
                        string updateFailedMessage = LocalizationManager.GetString("UpdateFailedInstallingPrevious", "Update failed, installing previously available version");
                        LogInfo(job.Name + ": " + updateFailedMessage, LogItemType.Warning);
                    }
                    else
                    {
                        string updateFailedNoFileMessage = LocalizationManager.GetString("UpdateFailedNoFile", "Update failed, no file available to install");
                        LogInfo(job.Name + ": " + updateFailedNoFileMessage, LogItemType.Error);
                        return;
                    }
                }
            }

            // Verificar novamente se o arquivo existe após o update
            if (!job.FileExists)
            {
                string noFileMessage = LocalizationManager.GetString("NoFileAvailable", "No file available for installation");
                LogInfo(job.Name + ": " + noFileMessage, LogItemType.Error);
                return;
            }

            string installingMessage = LocalizationManager.GetString("InstallingApplicationProgress", "Installing application {0} of {1}: {2}");
            UpdateStatus(string.Format(installingMessage, count, this.Applications.Length, job.Name));

            try
            {
                job.Install(bgwSetup);
                string installedSuccessMessage = LocalizationManager.GetString("InstalledSuccessfully", "Installed successfully");
                LogInfo(job.Name + ": " + installedSuccessMessage, LogItemType.Info);
                this.installCounter++;
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                string tip = GetErrorTip(ex.Message);
                
                if (!string.IsNullOrEmpty(tip))
                {
                    errorMessage += "\n\n" + tip;
                }
                
                string setupFailedMessage = LocalizationManager.GetString("SetupFailed", "Setup failed");
                LogInfo(job.Name + ": " + setupFailedMessage + " - " + errorMessage, LogItemType.Error);
                return;
            }

            count++;
        }

        private void bgwSetup_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string completionMessage = LocalizationManager.GetString("ApplicationsInstalledSuccessfully", "{0} of {1} applications installed successfully.");
            UpdateStatus(string.Format(completionMessage, this.installCounter, this.Applications.Length));
            progressBar.Style = ProgressBarStyle.Blocks;
            progressBar.Value = 100;
            bCancel.Enabled = true;
            bCancel.Text = LocalizationManager.GetString("Close", "Close");

            if (this.AutoClose)
            {
                this.Close();
            }
        }

        private void bCancel_Click(object sender, EventArgs e)
        {
            if (bgwSetup.IsBusy)
            {
                bgwSetup.CancelAsync();
                bCancel.Enabled = false;
                // Do not close yet!
                DialogResult = DialogResult.None;
            }
            else
            {
                this.Close();
            }
        }

        #region Expansion 

        private void lbDetails_MouseEnter(object sender, EventArgs e)
        {
            lbShowHideDetails.ImageIndex = (expanded ? 1 : 4);
        }

        private void lbDetails_MouseLeave(object sender, EventArgs e)
        {
            lbShowHideDetails.ImageIndex = (expanded ? 0 : 3);
        }

        private void lbDetails_MouseUp(object sender, MouseEventArgs e)
        {
            lbShowHideDetails.ImageIndex = (expanded ? 1 : 4);
        }

        private void lbDetails_Click(object sender, EventArgs e)
        {
            this.expanded = !this.expanded;
            SetExpansionButton();
            if (this.expanded)
                this.Height += pnlExpanded.Height;
            else
                this.Height -= pnlExpanded.Height;
        }

        #endregion

        private void ApplyLocalization()
        {
            // Título do formulário
            this.Text = LocalizationManager.GetString("InstallingApplications", "Installing Applications");
            
            // Botão
            bCancel.Text = LocalizationManager.GetString("Cancel", "Cancel");
        }

        private string GetErrorTip(string errorMessage)
        {
            if (errorMessage.Contains("blocked") || errorMessage.Contains("access denied"))
            {
                return LocalizationManager.GetString("ErrorTipAccessDenied", "Tip: Try running Ketarin as administrator or check if the file is being used by another application.");
            }
            
            if (errorMessage.Contains("network") || errorMessage.Contains("timeout"))
            {
                return LocalizationManager.GetString("ErrorTipNetwork", "Tip: Check your internet connection and try again.");
            }
            
            return string.Empty;
        }

        private void updater_ProgressChanged(object sender, EventArgs e)
        {
            // Este método é chamado quando o progresso do updater muda
            // Atualmente não há implementação específica necessária
        }
    }
}
