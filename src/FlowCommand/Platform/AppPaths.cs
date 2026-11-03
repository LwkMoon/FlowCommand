using System;
using System.IO;

namespace FlowCommand.Platform
{
    internal static class AppPaths
    {
        public static string ConfigDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlowCommand");
        public static string ConfigFile => Path.Combine(ConfigDir, "config.json");
        public static string SecretsDir => Path.Combine(ConfigDir, "secrets");
        public static string LogDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlowCommand", "logs");
        public static string LogFile => Path.Combine(LogDir, "flowcommand.log");
    }
}
