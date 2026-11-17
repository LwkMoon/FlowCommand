using System;
using System.IO;
using Windows.Storage;

namespace FlowCommand.Platform
{
    /// <summary>
    /// Where FlowCommand keeps its files.
    /// Zip/portable install: %APPDATA%\FlowCommand (settings) and %LOCALAPPDATA%\FlowCommand\logs (log).
    /// Microsoft Store (MSIX) install: the package's own LocalState folder. Using that real path on purpose, because
    /// Windows silently redirects %APPDATA% writes of packaged apps, which would make "Open config folder" show an empty folder.
    /// Everything in the packaged location is removed by Windows when the app is uninstalled.
    /// </summary>
    internal static class AppPaths
    {
        private static string? _packagedRoot;

        private static string PackagedRoot => _packagedRoot ??= ApplicationData.Current.LocalFolder.Path;

        public static string ConfigDir => PackageIdentity.IsPackaged
            ? Path.Combine(PackagedRoot, "config")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlowCommand");

        public static string ConfigFile => Path.Combine(ConfigDir, "config.json");
        public static string SecretsDir => Path.Combine(ConfigDir, "secrets");

        public static string LogDir => PackageIdentity.IsPackaged
            ? Path.Combine(PackagedRoot, "logs")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlowCommand", "logs");

        public static string LogFile => Path.Combine(LogDir, "flowcommand.log");
    }
}
