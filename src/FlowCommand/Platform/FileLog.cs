using System;
using System.IO;
using FlowCommand.Core.Pipeline;

namespace FlowCommand.Platform
{
    /// <summary>Local-only log. Transcripts are logged only if "logTranscripts" is enabled. Rotates at 1 MB.</summary>
    internal sealed class FileLog : ILog
    {
        private readonly string _path;
        private readonly object _gate = new object();

        public FileLog(string path) { _path = path; }

        public void Info(string message) => Write("INFO", message);
        public void Warn(string message) => Write("WARN", message);
        public void Error(string message, Exception? ex = null) =>
            Write("ERROR", ex == null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}");

        private void Write(string level, string message)
        {
            try
            {
                lock (_gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                    var fi = new FileInfo(_path);
                    if (fi.Exists && fi.Length > 1_000_000) File.Move(_path, _path + ".old", true);
                    File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
                }
            }
            catch { /* logging must never crash the app */ }
        }
    }
}
