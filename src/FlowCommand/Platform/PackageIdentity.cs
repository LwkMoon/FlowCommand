using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FlowCommand.Platform
{
    /// <summary>
    /// Tells whether FlowCommand is running from an MSIX package (Microsoft Store install) or as a plain program
    /// (zip download or "dotnet run"). Packaged and unpackaged copies store their data and startup entry differently.
    /// </summary>
    internal static class PackageIdentity
    {
        private const int AppModelErrorNoPackage = 15700;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

        private static bool? _packaged;

        public static bool IsPackaged
        {
            get
            {
                if (_packaged.HasValue) return _packaged.Value;
                try
                {
                    var length = 0;
                    _packaged = GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
                }
                catch (EntryPointNotFoundException) { _packaged = false; }   // Windows older than 8: no package model at all
                return _packaged.Value;
            }
        }
    }
}
