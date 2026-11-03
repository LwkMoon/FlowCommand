using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FlowCommand.Core.Pipeline;

namespace FlowCommand.Platform
{
    /// <summary>
    /// Secrets are encrypted with Windows DPAPI (current-user scope): readable only by this Windows user
    /// on this machine. They are never written to config.json or the log.
    /// </summary>
    internal sealed class DpapiSecretStore : ISecretStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FlowCommand.v1");
        private static readonly Regex SafeName = new Regex("^[a-z0-9_-]{1,40}$");
        private readonly string _dir;

        public DpapiSecretStore(string dir) { _dir = dir; }

        private string FileFor(string name)
        {
            if (!SafeName.IsMatch(name)) throw new ArgumentException("Invalid secret name.", nameof(name));
            return Path.Combine(_dir, name + ".secret");
        }

        public bool Has(string name) => File.Exists(FileFor(name));

        public string? Get(string name)
        {
            try
            {
                var f = FileFor(name);
                if (!File.Exists(f)) return null;
                var plain = ProtectedData.Unprotect(File.ReadAllBytes(f), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (CryptographicException) { return null; }
            catch (IOException) { return null; }
        }

        public void Set(string name, string value)
        {
            Directory.CreateDirectory(_dir);
            var cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FileFor(name), cipher);
        }

        public void Delete(string name)
        {
            var f = FileFor(name);
            if (File.Exists(f)) File.Delete(f);
        }
    }
}
