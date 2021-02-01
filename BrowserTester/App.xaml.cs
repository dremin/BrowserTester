using System.Windows;
using ManagedShell.Common.Logging;
using ManagedShell.Common.Logging.Observers;

namespace BrowserTester
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            ShellLogger.Severity = LogSeverity.Debug;
            ShellLogger.Attach(new ConsoleLog());
        }
    }
}
