using System;
using Microsoft.Win32;

namespace FlowCommand.Platform
{
    /// <summary>Maps the browser Windows uses for https links to a built-in application id.</summary>
    internal static class DefaultBrowserDetector
    {
        private const string UserChoiceKey =
            @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice";

        public static string? Detect()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(UserChoiceKey))
            {
                var progId = key?.GetValue("ProgId") as string;
                if (string.IsNullOrEmpty(progId)) return null;

                bool Is(string prefix) => progId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
                if (Is("ChromeHTML")) return "chrome";
                if (Is("MSEdgeHTM")) return "edge";
                if (Is("FirefoxURL")) return "firefox";
                if (Is("BraveHTML")) return "brave";
                if (Is("VivaldiHTM")) return "vivaldi";
                if (progId.IndexOf("OperaGX", StringComparison.OrdinalIgnoreCase) >= 0) return "operagx";
                if (Is("Opera")) return "opera";
                return null;
            }
        }
    }
}
