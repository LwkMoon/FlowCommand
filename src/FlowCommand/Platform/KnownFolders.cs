using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FlowCommand.Platform
{
    internal static class KnownFolders
    {
        private static readonly Guid FolderDownloads = new Guid("374DE290-123F-4565-9164-39C4925E467B");

        [DllImport("shell32.dll")]
        private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint flags, IntPtr token, out IntPtr path);
        [DllImport("ole32.dll")]
        private static extern void CoTaskMemFree(IntPtr ptr);

        public static string? Resolve(string key)
        {
            switch (key)
            {
                case "downloads": return Downloads();
                case "documents": return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                case "desktop": return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                case "pictures": return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                case "music": return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                case "videos": return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                case "home": return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                case "screenshots": return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
                case "onedrive": return Environment.GetEnvironmentVariable("OneDrive");
                case "appdata": return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                case "temp": return Path.GetTempPath().TrimEnd('\\');
                case "startup": return Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                default: return null;
            }
        }

        private static string Downloads()
        {
            try
            {
                if (SHGetKnownFolderPath(FolderDownloads, 0, IntPtr.Zero, out var p) == 0 && p != IntPtr.Zero)
                {
                    try { return Marshal.PtrToStringUni(p) ?? Fallback(); }
                    finally { CoTaskMemFree(p); }
                }
            }
            catch { }
            return Fallback();
        }

        private static string Fallback() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }
}
