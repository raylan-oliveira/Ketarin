using System;
using System.Diagnostics;
using System.Xml.Serialization;
using Ketarin.Localization;

namespace Ketarin
{
    /// <summary>
    /// Represents an instruction that starts a process.
    /// </summary>
    [Serializable()]
    public class StartProcessInstruction : SetupInstruction
    {
        private SerializableDictionary<string, string> environmentVariables = new SerializableDictionary<string, string>();
        private bool waitForExit = true;

        #region Properties

        /// <summary>
        /// Gets a list of environment variables to override, including the values to use.
        /// </summary>
        [XmlElement("EnvironmentVariables")]
        public SerializableDictionary<string, string> EnvironmentVariables
        {
            get
            {
                return this.environmentVariables;
            }
            set
            {
                // For serializer
                this.environmentVariables = value;
            }
        }

        public override string Name
        {
            get
            {
                return "Start process";
            }
        }

        /// <summary>
        /// File name to be executed.
        /// </summary>
        public string FileName
        {
            get; set;
        }

        /// <summary>
        /// Parameters for file execution.
        /// </summary>
        public string Parameters
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets whether or not to wait for the process to complete.
        /// </summary>
        public bool WaitForExit
        {
            get { return this.waitForExit; }
            set { this.waitForExit = value; }
        }

        #endregion

        #region ISetupCommand Member

        public override void Execute()
        {
            string fileName = Application.Variables.ReplaceAllInString(FileName);
            string parameters = Application.Variables.ReplaceAllInString(Parameters);

            fileName = Environment.ExpandEnvironmentVariables(fileName);
            parameters = Environment.ExpandEnvironmentVariables(parameters);

            ProcessStartInfo startInfo = new ProcessStartInfo(fileName, parameters);

            foreach (var variable in EnvironmentVariables)
            {
                if (!string.IsNullOrEmpty(variable.Value))
                {
                    startInfo.EnvironmentVariables[variable.Key] = variable.Value;
                }
            }

            startInfo.CreateNoWindow = true;
            Process proc = Process.Start(startInfo);
            if (this.WaitForExit && proc != null)
            {
                proc.WaitForExit();

                if (this.WaitForExit && proc.ExitCode != 0)
                {
                    string friendlyMessage = GetFriendlyErrorMessage(proc.ExitCode);
                    string errorMessage = LocalizationManager.GetString("ProcessExitedWithErrorCode", "Process exited with error code {0}");
                    
                    if (!string.IsNullOrEmpty(friendlyMessage))
                    {
                        errorMessage += " - " + friendlyMessage;
                    }
                    
                    throw new ApplicationException(string.Format(errorMessage, proc.ExitCode));
                }
            }
        }

        /// <summary>
        /// Mapeia códigos de erro comuns para mensagens amigáveis
        /// </summary>
        /// <param name="exitCode">Código de saída do processo</param>
        /// <returns>Mensagem amigável explicando o erro</returns>
        

        private string GetFriendlyErrorMessage(int exitCode)
        {
            switch (exitCode)
            {
                case 1603:
                    return LocalizationManager.GetString("PermissionError", "Permission error - Run as administrator");
                case 1602:
                    return LocalizationManager.GetString("MSIInstallationCancelled", "MSI installation cancelled by user");
                case 1618:
                    return LocalizationManager.GetString("AnotherInstallationInProgress", "Another installation is already in progress");
                case 1625:
                    return LocalizationManager.GetString("InstallationBlocked", "Installation blocked by system policy");
                case 3010:
                    return LocalizationManager.GetString("SystemRebootRequired", "System reboot required");
                default:
                    return null;
            }
        }
        #endregion

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Parameters))
            {
                return this.FileName;
            }
            else
            {
                return string.Format("Start \"{0}\" with following parameters: {1}", FileName, Parameters);
            }
        }
    }
}
