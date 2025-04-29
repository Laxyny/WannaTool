using Microsoft.Toolkit.Uwp.Notifications;

namespace WannaTool
{
    public static class Utils
    {
        public static void ShowToast(string title, string message, bool silent = false)
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();
        }
    }
}