using Microsoft.Toolkit.Uwp.Notifications;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Diagnostics;

namespace WannaTool
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                var args = ToastArguments.Parse(toastArgs.Argument);
                if (args["action"] == "openScreenshot"
                    && args.TryGetValue("path", out var path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
            };

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Indexer.Dispose();
            base.OnExit(e);
        }
    }

}
