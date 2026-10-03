using System.Diagnostics;

namespace AITool
{
    public static class ShellLauncher
    {
        /// <summary>
        /// Opens a URL, document, or folder with its associated program. On .NET Core Process.Start(string)
        /// defaults to UseShellExecute = false, which throws for anything that isn't an executable.
        /// </summary>
        public static void Open(string Target)
        {
            using Process prc = Process.Start(new ProcessStartInfo(Target) { UseShellExecute = true });
        }
    }
}
