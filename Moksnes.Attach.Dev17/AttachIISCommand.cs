using System;
using System.ComponentModel.Design;
using System.Security.Principal;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Moksnes.Attach
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class AttachIISCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("ed7e0d92-e842-414d-b6c6-66daad0a2de1");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        /// <summary>
        /// Initializes a new instance of the <see cref="AttachIISCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private AttachIISCommand(Package package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static AttachIISCommand Instance
        {
            get;
            private set;
        }

        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            // Switch to the main thread - the call to AddCommand in Command1's constructor requires
            // the UI thread.

            IServiceProvider serviceProvider = package;
            OleMenuCommandService commandService = serviceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

            Instance = new AttachIISCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string message = "You are not running Visual Studio as an administrator.\nThis is required for attaching to IIS.";
            string title = "Attach To IIS";
            bool isAdministrator = IsAdministrator();

            var dte = (DTE)ServiceProvider.GetService(typeof(DTE));
            foreach (Process process in dte.Debugger.LocalProcesses)
            {
                if (process.Name.EndsWith("w3wp.exe"))
                {
                    try
                    {
                        process.Attach();
                    }
                    catch (System.Runtime.InteropServices.COMException ex) when (ex.Message.Contains("insufficient privileges") && !isAdministrator)
                    {
                        VsShellUtilities.ShowMessageBox(
                            this.package,
                            message,
                            title,
                            OLEMSGICON.OLEMSGICON_INFO,
                            OLEMSGBUTTON.OLEMSGBUTTON_OK,
                            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                        return;
                    }
                    catch (Exception ex)
                    {
                        VsShellUtilities.ShowMessageBox(
                            this.package,
                            $"Unchandled error: {ex.Message}.",
                            title,
                            OLEMSGICON.OLEMSGICON_INFO,
                            OLEMSGBUTTON.OLEMSGBUTTON_OK,
                            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    }
                }
            }
        }

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
